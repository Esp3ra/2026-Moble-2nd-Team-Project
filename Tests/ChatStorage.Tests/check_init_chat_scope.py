"""R21-04: execute real cmd.exe control flow with only Flask replaced by a stub.

No Python virtual environment, server or database is executed. The product batch
is copied into an owned temporary tree and its exact Flask command is replaced.
"""
from pathlib import Path
import argparse
import json
import os
import re
import subprocess
import tempfile

COMMAND='".venv\\Scripts\\python.exe" -m flask --app remotework chat init-tables'


def run_case(source, caller, outcome):
    with tempfile.TemporaryDirectory(prefix="remote-work-chat-scope-") as name:
        temp=Path(name).resolve()
        assert temp.parent==Path(tempfile.gettempdir()).resolve()
        root=temp/"한글 프로젝트 경로";server=root/"RemoteWork.Server"
        server.mkdir(parents=True)
        if outcome!="missing":
            python=server/".venv/Scripts/python.exe"
            python.parent.mkdir(parents=True)
            python.write_text("unused placeholder; not an executable",encoding="ascii")
        # The rest of Init-Chat is unchanged, including setlocal, branches,
        # pause and exits. UTF-8 and CRLF match the product's batch encoding.
        assert source.count(COMMAND)==1
        target=root/"Init-Chat.cmd"
        target.write_text(source.replace(COMMAND,'call "%~dp0stub.cmd"'),encoding="utf-8",newline="\r\n")
        (root/"stub.cmd").write_text('@echo off\necho CHAT_SCOPE_CHILD=%REMOTEWORK_ENABLE_CHAT_DRAFT%\nexit /b %CHAT_SCOPE_STUB_EXIT%\n',encoding="ascii",newline="\r\n")
        caller_set='set "REMOTEWORK_ENABLE_CHAT_DRAFT='+('' if caller is None else caller)+'"'
        wrapper=root/"caller.cmd"
        wrapper.write_text('@echo off\nsetlocal\n'+caller_set+'\nset "CHAT_SCOPE_STUB_EXIT='+('7' if outcome=="failure" else '0')+'"\ncall "%~dp0Init-Chat.cmd" <nul\nset "CHAT_SCOPE_RETURN=%errorlevel%"\nif defined REMOTEWORK_ENABLE_CHAT_DRAFT (\nset REMOTEWORK_ENABLE_CHAT_DRAFT\n) else (\necho REMOTEWORK_ENABLE_CHAT_DRAFT=UNSET\n)\necho CHAT_SCOPE_RETURN=%CHAT_SCOPE_RETURN%\nexit /b 0\n',encoding="utf-8",newline="\r\n")
        run=subprocess.run([os.environ.get("COMSPEC","cmd.exe"),"/d","/c",str(wrapper)],cwd=root,
                           capture_output=True,timeout=12,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
        output=run.stdout.decode("utf-8",errors="replace")+run.stderr.decode("utf-8",errors="replace")
        after=re.findall(r"^REMOTEWORK_ENABLE_CHAT_DRAFT=(.*)$",output,re.M)
        returns=re.findall(r"^CHAT_SCOPE_RETURN=(.*)$",output,re.M)
        child=re.findall(r"^CHAT_SCOPE_CHILD=(.*)$",output,re.M)
        actual=after[-1].strip() if after else "NO_OUTPUT"
        code=returns[-1].strip() if returns else "NO_OUTPUT"
        expected="UNSET" if caller is None else caller
        ok=(run.returncode==0 and actual==expected and code==("0" if outcome=="success" else "1")
            and (not child if outcome=="missing" else child==["1\r"] or child==["1"]))
        return {"caller":expected,"outcome":outcome,"after":actual,"exit_code":code,
                "child": [v.strip() for v in child],"passed":ok,"database_executed":False}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--batch",type=Path,default=Path(__file__).resolve().parents[2]/"Init-Chat.cmd")
    parser.add_argument("--report",type=Path)
    args=parser.parse_args()
    if os.name!="nt":raise SystemExit("This regression requires Windows cmd.exe")
    source=args.batch.read_text(encoding="utf-8-sig")
    rows=[run_case(source,value,outcome) for value in (None,"0","1") for outcome in ("success","failure","missing")]
    result={"batch":str(args.batch.resolve()),"passed":sum(row["passed"] for row in rows),
            "failed":sum(not row["passed"] for row in rows),"cases":rows,"database_executed":False}
    if args.report:
        args.report.parent.mkdir(parents=True,exist_ok=True)
        args.report.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps(result,ensure_ascii=True))
    return 0 if result["failed"]==0 else 1


if __name__=="__main__":raise SystemExit(main())
