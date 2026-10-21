import sys, pathlib
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT.parents[1]/"RemoteWork.Server"))

import os, secrets, tempfile, hashlib, base64, json, time, subprocess, threading, socket
from datetime import date, timedelta
from sqlalchemy import select, func
from sqlalchemy.schema import CreateTable, CreateIndex
from sqlalchemy.dialects import mysql
from werkzeug.security import generate_password_hash
from remotework import create_app
from remotework.models import Base, User, AdminGuard, SchemaVersion, utcnow, AuditEvent
from remotework.db import transaction, get_db
from remotework.features.tasks.models import TaskRecord, TaskAssignment, TaskManager
from remotework.features import results as feature
from remotework.write_requests import WriteReceipt

COUNT=0
def check(ok,label):
    global COUNT
    if not ok: raise AssertionError(label)
    COUNT+=1

def run(database_url, storage, run_client=False):
    app=create_app(dict(TESTING=True,DATABASE_URL=database_url,SECRET_KEY=secrets.token_hex(32),
        ENABLE_TASK_QUERY_DRAFT=True,ENABLE_COMMON_WRITE_DRAFT=True,
        ENABLE_TASK_CREATE_DRAFT=True,ENABLE_TASK_START_DRAFT=True,ENABLE_RESULTS_DRAFT=True,
        RESULTS_CONTEXT_MODE="local-development",RESULTS_STORAGE_ROOT=str(storage),
        COOKIE_SECURE=False))
    from sqlalchemy.exc import SQLAlchemyError
    prior_db_error=app.error_handler_spec[None][None][SQLAlchemyError]
    def debug_owned_sql(error):
        print("OWNED TEST SQL:",str(error.orig),flush=True)
        return prior_db_error(error)
    app.register_error_handler(SQLAlchemyError,debug_owned_sql)
    engine=app.extensions["db_engine"]
    if run_client:
        from flask import request, jsonify
        @app.after_request
        def simulate_one_lost_success(response):
            if (request.path.endswith('/submissions') and request.method=='POST' and
                request.is_json and request.get_json().get('description')=='C# response loss' and
                not app.config.get('TEST_LOSS_SENT')):
                app.config['TEST_LOSS_SENT']=True
                lost=jsonify(ok=False,error={'code':'TEST_LOST_RESPONSE','message':'TEST response unavailable'})
                lost.status_code=502
                return lost
            return response
    global ENGINE_FOR_CLEANUP
    ENGINE_FOR_CLEANUP=engine
    ddl="-- Member 3 result tables only. No seed, ALTER, DROP or global schema-version change.\n\n"
    for table in Base.metadata.sorted_tables:
        if table in feature.RESULT_TABLES:
            ddl+=str(CreateTable(table).compile(dialect=mysql.dialect()))+";\n"
            for index in sorted(table.indexes,key=lambda x:x.name):
                ddl+=str(CreateIndex(index).compile(dialect=mysql.dialect()))+";\n"
    if engine.dialect.name=="mysql":
        Base.metadata.create_all(engine,tables=[t for t in Base.metadata.sorted_tables if t not in feature.RESULT_TABLES])
        with engine.begin() as conn:
            for statement in ddl.split(";"):
                if statement.strip(): conn.exec_driver_sql(statement)
        check(True,"raw 006 SQL executed in owned MySQL")
    else:
        Base.metadata.create_all(engine)
    password=secrets.token_urlsafe(24)
    ids={}
    with app.app_context(), transaction() as db:
        db.add_all([AdminGuard(id=1),SchemaVersion(version=1)])
        for login,role,department in [("manager","admin","Dev"),("worker","employee","Dev"),
                                      ("upper","admin","Dev"),("outsider","employee","Other")]:
            user=User(login_id=login,password_hash=generate_password_hash(password,method="pbkdf2:sha256"),
                display_name="TEST-"+login,employee_no="TEST-"+login,department=department,role=role,is_active=True)
            db.add(user);db.flush();ids[login]=user.id
        today=date.today()
        for n in range(1,11):
            owner=ids["manager"] if n==3 else ids["outsider"] if n==6 else ids["worker"]
            progressed=n in (4,5,6,7,8,9,10)
            due=today-timedelta(days=1) if n==4 else today+timedelta(days=3)
            task=TaskRecord(task_id=n,title="TEST task "+str(n),description="TEST description",
                task_status="in_progress" if progressed else "not_started",
                start_date=today-timedelta(days=3),due_date=due,current_assignment_revision=1,
                row_version=1,created_by=ids["manager"],created_at=utcnow(),updated_at=utcnow())
            db.add(task);db.flush()
            db.add(TaskAssignment(task_id=n,revision=1,assignee_id=owner,
                assigned_by=ids["manager"],assigned_at=utcnow(),
                accepted_at=utcnow() if progressed else None))
            db.add(TaskManager(task_id=n,user_id=ids["manager"]))
    runner=app.test_cli_runner()
    check(runner.invoke(args=["results","init-results"]).exit_code==0,"own table initializer")
    check(runner.invoke(args=["results","set-department-reviewer","--reviewer-login","manager"]).exit_code==0,"explicit department reviewer")
    check(runner.invoke(args=["results","set-upper-reviewer","--submitter-login","manager","--reviewer-login","upper"]).exit_code==0,"explicit upper reviewer")
    check(runner.invoke(args=["results","set-upper-reviewer","--submitter-login","manager","--reviewer-login","manager"]).exit_code!=0,"reject self reviewer")
    clients={}
    headers={}
    for login in ids:
        client=app.test_client()
        response=client.post("/api/v1/auth/login",json={"login_id":login,"password":password})
        check(response.status_code==200,"login "+login)
        clients[login]=client
        headers[login]={"Authorization":"Bearer "+response.json["data"]["access_token"]}
    def call(login,method,path,body=None,status=200,key=None):
        h=dict(headers[login])
        if key: h["Idempotency-Key"]=key
        response=getattr(clients[login],method)("/api/v1/"+path,headers=h,json=body)
        error_code=response.json.get("error",{}).get("code","") if response.is_json else ""
        check(response.status_code==status,path+" status "+str(response.status_code)+" "+error_code)
        return response.json.get("data") if response.is_json else response.data
    def key(login,action,target):
        return call(login,"post","write-requests/keys",{"action":action,"target":str(target)},201)["request_key"]
    def start(task,action,path,version):
        if engine.dialect.name=="mysql" and action=="task.rework_started":
            with app.app_context():
                accepted=get_db().get(TaskAssignment,(task,1)).accepted_at
            if accepted is not None and accepted>utcnow():
                # Existing member2 DATETIME(0) may round its first accepted_at forward.
                # Test waits for that recorded instant; production is not modified/retried.
                time.sleep((accepted-utcnow()).total_seconds()+0.05)
        body={"expected_row_version":str(version),"expected_assignment_revision":1}
        return call("worker","post",f"tasks/{task}/{path}",body,key=key("worker",action,task))
    def submit(task,description,files=None):
        context=call("worker","get",f"results/tasks/{task}/context")
        body={"expected_row_version":context["row_version"],"expected_assignment_revision":1,
              "description":description,"attachment_ids":files or []}
        k=key("worker","submission.submitted",task)
        return call("worker","post",f"results/tasks/{task}/submissions",body,201,k),body,k
    def decide(login,submission,outcome,reason):
        context=call(login,"get",f"results/submissions/{submission}/context")
        body={"expected_row_version":context["row_version"],"expected_assignment_revision":1,
              "outcome":outcome,"reason":reason}
        k=key(login,"submission."+outcome,submission)
        return call(login,"post",f"results/submissions/{submission}/decisions",body,key=k)
    check(call("worker","get","results/tasks/1/submissions")["items"]==[],"no fabricated submissions")
    call("worker","get","results/tasks/1/context",status=409)
    call("outsider","get","results/tasks/1/submissions",status=403)
    check(start(1,"task.accepted","accept",1)["task_status"]=="in_progress","actual member2 accept")
    context=call("worker","get","results/tasks/1/context")
    check(context["next_round_no"]=="1" and context["reviewer_id"]==ids["manager"],"first round and configured reviewer")
    content=bytes(range(256))*240
    digest=hashlib.sha256(content).hexdigest()
    upload=call("worker","post","results/tasks/1/uploads/start",
                {"name":"test.any-format","size":len(content),"sha256":digest},201)
    attachment=upload["attachment_id"]
    call("worker","get",f"results/attachments/{attachment}/chunks?offset=0",status=404)
    for offset in range(0,len(content),16384):
        part=content[offset:offset+16384]
        body={"offset":offset,"data":base64.b64encode(part).decode()}
        progress=call("worker","post",f"results/uploads/{attachment}/chunks",body)
        check(progress["next_offset"]==offset+len(part),"chunk position")
        if offset==0:
            call("worker","post",f"results/uploads/{attachment}/chunks",body)
            other={"offset":0,"data":base64.b64encode(b"x"*len(part)).decode()}
            call("worker","post",f"results/uploads/{attachment}/chunks",other,status=409)
    call("worker","post",f"results/uploads/{attachment}/complete",{})
    saved,body,k=submit(1,"TEST first result",[attachment])
    check(saved["round_no"]=="1" and saved["task_status"]=="submitted","first submit")
    replay=call("worker","post","results/tasks/1/submissions",body,201,k)
    check(replay==saved,"same key returns same result")
    call("worker","post","results/tasks/1/submissions",{**body,"description":"changed"},409,k)
    other_key=key("worker","submission.submitted",1)
    call("worker","post","results/tasks/1/submissions",body,409,other_key)
    check(len(call("worker","get","results/tasks/1/submissions")["items"])==1,"no duplicate round")
    downloaded=bytearray()
    offset=0
    while True:
        chunk=call("manager","get",f"results/attachments/{attachment}/chunks?offset={offset}")
        downloaded.extend(base64.b64decode(chunk["data"]));offset=chunk["next_offset"]
        if chunk["eof"]: break
    check(bytes(downloaded)==content,"actual authenticated download bytes")
    # Current task managers may read files without gaining reviewer authority.
    call("upper","get",f"results/attachments/{attachment}/chunks?offset=0",status=403)
    with app.app_context(),transaction() as db:
        db.add(TaskManager(task_id=1,user_id=ids["upper"]))
    managed_chunk=call("upper","get",f"results/attachments/{attachment}/chunks?offset=0")
    check(base64.b64decode(managed_chunk["data"])==content[:16384],"current non-reviewer manager reads attachment bytes")
    call("upper","get",f"results/submissions/{saved['submission_id']}/context",status=403)
    with app.app_context(),transaction() as db:
        db.delete(db.get(TaskManager,(1,ids["upper"])))
    call("upper","get",f"results/attachments/{attachment}/chunks?offset=0",status=403)
    call("outsider","get",f"results/attachments/{attachment}/chunks?offset=0",status=403)
    call("worker","get",f"results/submissions/{saved['submission_id']}/context",status=403)
    decision_context=call("manager","get",f"results/submissions/{saved['submission_id']}/context")
    invalid={"expected_row_version":decision_context["row_version"],"expected_assignment_revision":1,
             "outcome":"returned","reason":" "}
    call("manager","post",f"results/submissions/{saved['submission_id']}/decisions",invalid,400,
         key("manager","submission.returned",saved["submission_id"]))
    returned=decide("manager",saved["submission_id"],"returned","TEST correction")
    check(returned["task_status"]=="returned","actual rejection")
    call("worker","get","results/tasks/1/context",status=409)
    with app.app_context():
        accepted=get_db().get(TaskAssignment,(1,1)).accepted_at
    start(1,"task.rework_started","rework-start",int(returned["row_version"]))
    second,_,_=submit(1,"TEST second result")
    approved=decide("manager",second["submission_id"],"approved","")
    check(approved["task_status"]=="approved" and second["round_no"]=="2","resubmit and approve")
    rows=call("worker","get","results/tasks/1/submissions")["items"]
    check(rows[-1]["description"]=="TEST first result" and rows[-1]["attachments"][0]["attachment_id"]==attachment,"past content and attachment preserved")
    with app.app_context():
        check(get_db().get(TaskAssignment,(1,1)).accepted_at==accepted,"first accepted time preserved")
    check(len(call("worker","get","results/tasks/1/history")["items"])==4,"one event per submit/decision")
    start(2,"task.accepted","accept",1)
    for round_no in range(1,7):
        result,_,_=submit(2,"TEST round "+str(round_no))
        check(result["round_no"]==str(round_no),"unbounded business round "+str(round_no))
        judged=decide("manager",result["submission_id"],"approved" if round_no==6 else "returned",
                     "" if round_no==6 else "TEST return")
        if round_no!=6:
            start(2,"task.rework_started","rework-start",int(judged["row_version"]))
    call("worker","get","results/tasks/4/context",status=409)
    check(runner.invoke(args=["results","set-extension-pending","--task-id","5","--pending"]).exit_code==0,"pending gate")
    call("worker","get","results/tasks/5/context",status=409)
    check(runner.invoke(args=["results","set-extension-pending","--task-id","5","--clear"]).exit_code==0,"clear gate")
    call("worker","get","results/tasks/5/context")
    call("outsider","get","results/tasks/6/context",status=409)
    call("worker","post","results/tasks/5/uploads/start",{"name":"too.big","size":25*1024*1024+1,"sha256":"0"*64},413)
    empty=call("worker","post","results/tasks/5/uploads/start",{"name":"empty.any","size":0,"sha256":hashlib.sha256(b"").hexdigest()},201)
    call("worker","post",f"results/uploads/{empty['attachment_id']}/complete",{})
    # Failure after event/audit insertion rolls all business records back.
    original_event=feature._event
    def fail_event(*args):
        original_event(*args)
        raise feature.AppError(503,"TEST_FAILURE","TEST rollback")
    context=call("worker","get","results/tasks/7/context")
    failed_body={"expected_row_version":context["row_version"],"expected_assignment_revision":1,"description":"TEST rollback","attachment_ids":[]}
    failed_key=key("worker","submission.submitted",7)
    feature._event=fail_event
    try: call("worker","post","results/tasks/7/submissions",failed_body,503,failed_key)
    finally: feature._event=original_event
    with app.app_context():
        db=get_db()
        check(db.get(TaskRecord,7).task_status=="in_progress","rollback task")
        check(db.scalar(select(func.count()).select_from(feature.Submission).where(feature.Submission.task_id==7))==0,"rollback submission")
        check(db.scalar(select(func.count()).select_from(feature.ResultEvent).where(feature.ResultEvent.task_id==7))==0,"rollback event")
    lookup=call("worker","post","write-requests/result",{"action":"submission.submitted","target":"7"},key=failed_key)
    check(lookup["state"]=="not_recorded","failure has no receipt")
    call("worker","post","results/tasks/7/submissions",failed_body,201,failed_key)
    # Upper reviewer mapping is real data, never self approval.
    admin_key=key("manager","task.accepted",3)
    call("manager","post","tasks/3/accept",{"expected_row_version":"1","expected_assignment_revision":1},key=admin_key)
    own=call("manager","get","results/tasks/3/context")
    check(own["reviewer_id"]==ids["upper"],"admin upper reviewer")
    own_body={"expected_row_version":own["row_version"],"expected_assignment_revision":1,"description":"TEST admin result","attachment_ids":[]}
    own_result=call("manager","post","results/tasks/3/submissions",own_body,201,key("manager","submission.submitted",3))
    call("manager","get",f"results/submissions/{own_result['submission_id']}/context",status=403)
    decide("upper",own_result["submission_id"],"approved","")
    # Expired unlinked file is cleaned; linked first round survives.
    with app.app_context(),transaction() as db:
        db.get(feature.Attachment,empty["attachment_id"]).expires_at=utcnow()-timedelta(days=1)
    check(runner.invoke(args=["results","cleanup-result-uploads"]).exit_code==0,"explicit unlinked cleanup")
    check((storage/attachment).is_file() and not (storage/empty["attachment_id"]).exists(),"submitted file survives cleanup")
    # Actual shared task audit + result audit, without fabricated display rows.
    activity=call("manager","get","results/tasks/1/activity")["items"]
    check(any(e["kind"]=="submitted" for e in activity),"activity includes actual submissions")
    check(any(e["kind"]=="approved" for e in activity),"activity includes actual approval")
    check(any(e["kind"]=="returned" for e in activity),"activity includes actual rejection")
    check(any(e["kind"]=="status_changed" for e in activity),"activity includes member2 accept/rework audit")
    check(len({e["event_id"] for e in activity})==len(activity),"activity no duplicated result audit")
    call("worker","get","results/tasks/1/activity",status=403)
    call("upper","get","results/tasks/1/activity",status=403)
    created=call("manager","post","tasks",{"title":"TEST real activity creation","description":"TEST only",
        "start_date":date.today().isoformat(),"due_date":(date.today()+timedelta(days=2)).isoformat(),
        "assignee_id":ids["worker"],"manager_user_ids":[ids["manager"]]},201,key("manager","task.created","new"))
    created_id=created["task_id"]
    events=call("manager","get",f"results/tasks/{created_id}/activity")["items"]
    check(len(events)==1 and events[0]["kind"]=="created","actual task creation audit in activity")
    check(call("manager","get",f"results/tasks/{created_id}/activity?before_event_id={events[0]['event_id']}")["items"]==[],"activity exclusive cursor")
    call("manager","get",f"results/tasks/{created_id}/activity?before_event_id=0",status=400)
    # Revoke original assignment in controlled test data only.
    with app.app_context(),transaction() as db:
        db.add(TaskAssignment(task_id=1,revision=2,assignee_id=ids["outsider"],
            assigned_by=ids["manager"],assigned_at=utcnow(),accepted_at=None))
        db.flush()
        task=db.get(TaskRecord,1);task.current_assignment_revision=2;task.row_version+=1;task.task_status="not_started"
    call("worker","get","results/tasks/1/submissions",status=403)
    # A changed assignee does not revoke a separate current-manager role.
    call("manager","get","results/tasks/1/activity")
    with app.app_context(),transaction() as db:
        db.delete(db.get(TaskManager,(1,ids["manager"])))
    call("manager","get","results/tasks/1/activity",status=403)
    with app.app_context(),transaction() as db:
        db.add(TaskManager(task_id=1,user_id=ids["manager"]))
    if engine.dialect.name=="mysql":
        def concurrent_submit(task_id,same_key):
            body={"expected_row_version":"1","expected_assignment_revision":1,
                  "description":"TEST concurrent","attachment_ids":[]}
            first_key=key("worker","submission.submitted",task_id)
            second_key=first_key if same_key else key("worker","submission.submitted",task_id)
            barrier=threading.Barrier(3)
            replies=[]
            def send(write_key):
                client=app.test_client()
                local_headers={**headers["worker"],"Idempotency-Key":write_key}
                barrier.wait(timeout=10)
                response=client.post(f"/api/v1/results/tasks/{task_id}/submissions",
                    json=body,headers=local_headers)
                replies.append((response.status_code,response.json))
            workers=[threading.Thread(target=send,args=(write_key,)) for write_key in (first_key,second_key)]
            for worker in workers: worker.start()
            barrier.wait(timeout=10)
            for worker in workers: worker.join(timeout=15)
            check(len(replies)==2 and not any(worker.is_alive() for worker in workers),"concurrent requests completed")
            check(sorted(r[0] for r in replies)==([201,201] if same_key else [201,409]),"concurrent same/different-key result")
            with app.app_context():
                db=get_db()
                check(db.scalar(select(func.count()).select_from(feature.Submission).where(feature.Submission.task_id==task_id))==1,"one concurrent submission")
                check(db.scalar(select(func.count()).select_from(feature.ResultEvent).where(feature.ResultEvent.task_id==task_id))==1,"one concurrent event")
                check(db.get(TaskRecord,task_id).row_version==2,"one concurrent version increment")
        concurrent_submit(9,True)
        concurrent_submit(10,False)
    if run_client:
        run_csharp(app,password,storage)
    print(f"PASS {COUNT} checks: {engine.dialect.name}, current source, Flask requests, explicit test-only users/tasks/files.")
    print("Original project/user DB untouched; shared DB/two-PC/device tests not run.")
    engine.dispose()
    return app

def run_csharp(app,password,storage):
    from waitress import create_server
    server=create_server(app,host="127.0.0.1",port=0,threads=4)
    thread=threading.Thread(target=server.run,daemon=True);thread.start()
    config=ROOT/"test_runtime.json"
    config.write_text(json.dumps({"server":"http://127.0.0.1:"+str(server.effective_port),
        "password":password,"storage":str(storage)}),encoding="utf-8")
    try:
        subprocess.run(["pwsh","-STA","-NoProfile","-File",str(ROOT/"verify_client.ps1"),str(config)],
                       check=True,timeout=45)
    finally:
        config.unlink(missing_ok=True)
        server.close()
        thread.join(timeout=3)

if __name__=="__main__":
    with tempfile.TemporaryDirectory(prefix="member3-results-",dir=ROOT) as temp:
        runtime=pathlib.Path(temp).resolve()
        if runtime.parent != ROOT.resolve() or not runtime.name.startswith("member3-results-"):
            raise SystemExit("Refusing an unexpected test cleanup target.")
        try:
            run("sqlite:///"+(runtime/"test.sqlite").as_posix(),runtime/"files",run_client="--client" in sys.argv)
        except Exception as error:
            import traceback
            spot=traceback.extract_tb(error.__traceback__)[-1]
            print("FAIL",type(error).__name__,str(error) if isinstance(error,(AssertionError,AttributeError)) else "",spot.filename,spot.lineno)
            raise SystemExit(1)
        finally:
            if "ENGINE_FOR_CLEANUP" in globals():
                ENGINE_FOR_CLEANUP.dispose()
