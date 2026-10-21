"use strict";
// Runs the original browser handlers with explicit DOM/HTTP doubles. No product
// database, browser, account, network or copied implementation is used here.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(process.argv[2] || path.join(__dirname,"../../RemoteWork.Server/remotework/static/results.js"),"utf8");
let checks = 0;
function check(value, message) { assert.ok(value,message); checks++; }
class Element {
  constructor(tag="div") {
    this.tagName=tag.toUpperCase();this.children=[];this.listeners={};this.dataset={};this.disabled=false;
    this.hidden=false;this.open=false;this.value="";this.files=[];this._text="";this.attributes={};
    this.classes=new Set();this.classList={toggle:(key,on)=>{if(on)this.classes.add(key);else this.classes.delete(key);},contains:key=>this.classes.has(key)};
  }
  set textContent(value) { this._text=String(value);this.children=[]; }
  get textContent() { return this._text+this.children.map(x=>x.textContent).join(""); }
  append(...nodes) { this.children.push(...nodes); }
  replaceChildren(...nodes) { this._text="";this.children=[...nodes]; }
  addEventListener(name,callback) { (this.listeners[name]??=[]).push(callback); }
  setAttribute(key,value) { this.attributes[key]=String(value);if(key==="hidden")this.hidden=true; }
  querySelectorAll(selector) {
    const all=[];for(const child of this.children){all.push(child,...child.querySelectorAll("*"));}
    if(selector==="*")return all;
    if(selector==="[data-close]")return all.filter(x=>x.dataset.close);
    if(selector==="dialog[open]")return all.filter(x=>x.tagName==="DIALOG"&&x.open);
    const tags=selector.split(",").map(x=>x.toUpperCase());return all.filter(x=>tags.includes(x.tagName));
  }
  showModal() { this.open=true; }
  close() { this.open=false; }
  remove() {}
  click() { return this.emit("click"); }
  emit(name) {
    const results=(this.listeners[name]||[]).map(callback=>callback({preventDefault(){}}));
    return Promise.all(results);
  }
}
const settle=async()=>{for(let i=0;i<8;i++)await new Promise(resolve=>setImmediate(resolve));};
const deferred=()=>{let resolve;const promise=new Promise(r=>resolve=r);return {promise,resolve};};
function response(data,status=200,code=null) {
  return {status,ok:status>=200&&status<300,redirected:false,headers:{get:()=>"SESSION"},
    json:async()=>code?{ok:false,error:{code,message:code}}:{ok:true,data}};
}
function harness({role="admin",state="submitted",write,lookup,initialTask=""}={}) {
  const ids=new Map(),root=new Element("section");
  root.dataset={page:"results",role,name:"TEST reviewer",session:"SESSION",csrf:"TEST-CSRF",taskId:initialTask};ids.set("results-page",root);
  const names=["message","pending-panel","pending-message","lookup","retry","reprepare","filters","status","search","list-title","list-count","tasks","detail","attachments","rounds","submit-open","rework-start","approve-open","return-open","submission-dialog","submission-form","submission-task","description","upload-files","pending-files","upload-message","submit-save","decision-dialog","decision-form","decision-title","decision-task","decision-result","decision-files","reason-label","reason","decision-message","decision-save","files-tab","rounds-tab","files-panel","rounds-panel"];
  const buttons=new Set(["lookup","retry","reprepare","submit-open","rework-start","approve-open","return-open","submit-save","decision-save","files-tab","rounds-tab"]);
  for(const name of names){const el=new Element(buttons.has(name)?"button":name.endsWith("dialog")?"dialog":name.endsWith("form")||name==="filters"?"form":["reason","description"].includes(name)?"textarea":"div");ids.set(name,el);root.append(el);}
  if(role==="admin"){const el=new Element("select");el.value="review";ids.set("view",el);root.append(el);}
  for(const name of ["submission-dialog","decision-dialog"]){const b=new Element("button");b.dataset.close=name;root.append(b);}
  ids.get("pending-panel").hidden=ids.get("retry").hidden=ids.get("reprepare").hidden=true;
  const task={task_id:"10",title:"TEST task",assignee_name:"TEST worker",task_status:state,start_date:"2026-10-20",due_date:"2099-12-31",row_version:"1"};
  const other={...task,task_id:"11",title:"TEST other"};
  let rounds=state==="submitted"?[
    {task_id:"10",submission_id:"202",round_no:"2",status:"submitted",description:"TEST NEW RESULT",submitted_at:"2026-10-20T01:00:00Z",attachments:[{attachment_id:"new-file",name:"NEW.any",size:1,sha256:"TEST"}]},
    {task_id:"10",submission_id:"201",round_no:"1",status:"returned",description:"TEST OLD RESULT",submitted_at:"2026-10-19T01:00:00Z",attachments:[{attachment_id:"old-file",name:"OLD.any",size:1,sha256:"TEST"}]}
  ]:[];
  const requests=[],paused=new Map();let keys=0,writes=0;
  function context(id) {return {task_id:"10",submission_id:id,round_no:"2",row_version:task.row_version,assignment_revision:1,task_status:task.task_status};}
  const fetch=async(url,options)=>{
    const endpoint=url.replace("/results/data/","");
    const body=options.body?JSON.parse(options.body.get("body")):undefined;
    const item={endpoint,body,key:options.headers["Idempotency-Key"]};requests.push(item);
    if(paused.has(endpoint))return paused.get(endpoint).promise;
    if(endpoint.startsWith("tasks?"))return response({items:[{...task},{...other}]});
    if(endpoint==="tasks/10"||endpoint==="tasks/11")return response({...task,task_id:endpoint.split("/")[1]});
    if(endpoint.endsWith("/submissions"))return response({items:rounds});
    if(endpoint==="tasks/10/context")return response({...context(null),title:task.title,due_date:task.due_date});
    if(endpoint.startsWith("submissions/")&&endpoint.endsWith("/context"))return response(context(endpoint.split("/")[1]));
    if(endpoint.startsWith("actions/key/"))return response({request_key:"KEY"+(++keys)});
    if(endpoint.startsWith("actions/lookup/"))return lookup?lookup(item):response({state:"not_recorded"});
    if(endpoint.startsWith("actions/submit/")||endpoint.startsWith("actions/decide/")){
      writes++;if(write)return write(item,writes);
      task.task_status=body.outcome||"submitted";return response({task_id:"10"});
    }
    throw new Error("Unexpected test endpoint "+endpoint);
  };
  const window=new Element("window");
  vm.runInNewContext(source,{document:{getElementById:id=>ids.get(id)||null,createElement:tag=>new Element(tag)},window,
    fetch,FormData,AbortController,setTimeout,clearTimeout,URL,Intl,Date,BigInt,Map,Set,Uint8Array,Object,JSON,Error,SyntaxError,
    location:{assign(){}},crypto:require("node:crypto").webcrypto,Blob,console});
  const rowButton=(id,index)=>ids.get(id).querySelectorAll("button")[index];
  return {ids,task,requests,paused,context,rowButton,
    get rounds(){return rounds;},set rounds(value){rounds=value;},
    async choose(){await settle();await rowButton("tasks",0).click();await settle();},
    get keys(){return keys;},get writes(){return writes;}};
}

async function decisionRace(outcome) {
  const h=harness();await h.choose();
  const delay=deferred();h.paused.set("submissions/202/context",delay);
  const opening=h.ids.get(outcome==="approved"?"approve-open":"return-open").click();await settle();
  check(h.rowButton("rounds",1).disabled,"round selection disabled while decision context loads");
  await h.rowButton("rounds",1).click();await h.rowButton("tasks",1).click();
  delay.resolve(response(h.context("202")));await opening;
  check(h.ids.get("decision-dialog").open,"decision dialog opened");
  check(h.ids.get("decision-task").textContent.includes("2차"),"decision uses captured round");
  check(h.ids.get("decision-result").textContent.includes("TEST NEW RESULT"),"decision uses captured body");
  check(!h.ids.get("decision-result").textContent.includes("TEST OLD RESULT"),"old body never appears");
  check(h.ids.get("decision-files").textContent.includes("NEW.any"),"decision uses captured attachment");
  h.ids.get("reason").value="TEST reason";await h.ids.get("decision-form").emit("submit");await settle();
  const post=h.requests.find(x=>x.endpoint==="actions/decide/202");
  check(post?.body.outcome===outcome,"actual POST uses the displayed submission and selected outcome");
  check(!h.requests.some(x=>x.endpoint==="actions/decide/201"),"old submission not posted");
}
async function mismatchedContext() {
  const h=harness();await h.choose();const delay=deferred();h.paused.set("submissions/202/context",delay);
  const opening=h.ids.get("approve-open").click();await settle();
  delay.resolve(response({...h.context("202"),submission_id:"201"}));await opening;
  check(!h.ids.get("decision-dialog").open,"mismatched context cannot open a decision");
  check(h.writes===0,"mismatched context never writes");
}
async function prepareSubmission(h) {
  await h.choose();await h.ids.get("submit-open").click();h.ids.get("description").value="TEST original description";
  await h.ids.get("submission-form").emit("submit");await settle();
}
async function rejectionRecovery(status,code) {
  const h=harness({role:"employee",state:"in_progress",write:(item,n)=>n===1?response(null,status,code):response({task_id:"10"})});
  await prepareSubmission(h);
  check(h.ids.get("description").disabled,"failed write keeps original request until lookup");
  check(h.ids.get("reprepare").hidden,"reprepare unavailable before not_recorded");
  await h.ids.get("lookup").click();
  check(!h.ids.get("reprepare").hidden,"definite failure and not_recorded offer reprepare");
  check(h.ids.get("retry").hidden,"stale same-request loop is not the recovery offered for conflict");
  h.task.row_version="2";await h.ids.get("reprepare").click();await settle();
  check(h.ids.get("submission-dialog").open,"submission reopened with latest context");
  check(!h.ids.get("description").disabled,"description editable after reprepare");
  check(h.ids.get("description").value==="TEST original description","original explanation preserved");
  check(h.keys===1&&h.writes===1,"reprepare itself never mints a key or writes");
  h.ids.get("description").value="TEST edited description";await h.ids.get("submission-form").emit("submit");await settle();
  const sent=h.requests.filter(x=>x.endpoint==="actions/submit/10");
  check(sent.length===2&&sent[1].body.expected_row_version==="2","explicit second submit uses current version");
  check(sent[1].key!==sent[0].key,"new confirmed attempt gets its own key");
  check(sent[1].body.description==="TEST edited description","new confirmed attempt uses edited input");
}
async function ambiguousRecovery() {
  const h=harness({role:"employee",state:"in_progress",write:(item,n)=>{if(n===1)throw new Error("TEST network unavailable");return response({task_id:"10"});}});
  await prepareSubmission(h);await h.ids.get("lookup").click();
  check(h.ids.get("reprepare").hidden,"unknown outcome never offers a new-key preparation");
  check(!h.ids.get("retry").hidden,"unknown outcome allows only same request after not_recorded");
  await h.ids.get("retry").click();await settle();
  const sent=h.requests.filter(x=>x.endpoint==="actions/submit/10");
  check(sent[0].key===sent[1].key,"ambiguous retry preserves original key");
  check(JSON.stringify(sent[0].body)===JSON.stringify(sent[1].body),"ambiguous retry preserves original body");
}
async function completedLookup() {
  const h=harness({role:"employee",state:"in_progress",write:()=>response(null,502,"TEST_RESPONSE_LOST"),lookup:()=>response({state:"completed",result:{task_id:"10"}})});
  await prepareSubmission(h);await h.ids.get("lookup").click();await settle();
  check(h.ids.get("pending-panel").hidden,"completed lookup clears pending panel");
  check(h.keys===1&&h.writes===1,"completed lookup does not submit again");
}
async function changedReviewAfterFailure() {
  const h=harness({write:()=>response(null,409,"TASK_VERSION_CONFLICT")});await h.choose();
  await h.ids.get("approve-open").click();await h.ids.get("decision-form").emit("submit");await h.ids.get("lookup").click();
  h.rounds=[{...h.rounds[0],submission_id:"203",round_no:"3",description:"TEST THIRD RESULT"},...h.rounds];
  await h.ids.get("reprepare").click();await settle();
  check(!h.ids.get("decision-dialog").open,"reprepare never silently switches approval to a different submission");
  check(h.keys===1&&h.writes===1,"changed review target needs a new explicit selection");
}
async function navigationHints() {
  let h=harness({initialTask:"10"});await settle();
  check(h.ids.get("detail").textContent.includes("10 · TEST task"),"authorized task navigation selects existing list item");
  check(h.writes===0,"navigation never saves a result");
  h=harness({initialTask:"999"});await settle();
  check(h.ids.get("message").textContent.includes("현재 조회 범위에 없습니다"),"unavailable navigation task stays unselected");
  check(!h.requests.some(x=>x.endpoint==="tasks/999"),"navigation hint does not bypass current list scope");
}
(async()=>{
  await decisionRace("approved");await decisionRace("returned");await mismatchedContext();
  await rejectionRecovery(409,"TASK_VERSION_CONFLICT");await rejectionRecovery(400,"INVALID_INPUT");
  await ambiguousRecovery();await completedLookup();await changedReviewAfterFailure();await navigationHints();
  console.log(`PASS ${checks} browser-handler checks using original results.js with explicit DOM/HTTP doubles. Not real browser/MySQL tests.`);
})().catch(error=>{console.error(error);process.exitCode=1;});
