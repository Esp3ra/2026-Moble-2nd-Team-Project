# 5번: 1번 서버와 Pi 코드의 실제 HTTP/MySQL 연결 검사

## V06 동명 테스트 모듈 수집 수정 — 2026-10-23

기본 실행기가 `pytest --import-mode=importlib`를 명시한다. 로그인·근무 클라이언트 옵션을
함께 사용하면 서로 다른 폴더의 `test_real_server.py` 두 개가 기본 import 이름으로 충돌했던
문제다. 파일명·수집 대상·인증/근태 assertion은 유지하고 모듈의 파일별 식별만 보존한다.
추가 환경변수나 외부 진단 패치 없이 아래 세 옵션을 같은 기본 명령에 함께 지정할 수 있다.

```powershell
& $identityPython Tests/FingerprintIdentity.Server.Tests/run_check.py `
  --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin' `
  --source-root $identitySource --report-directory $identityReport `
  --identity-login-client '<IdentityLogin.Ui.Tests.dll 절대 경로>' `
  --workday-activity-client '<WorkdayActivity.Tests.dll 절대 경로>' `
  --workday-clockout-client '<같은 WorkdayActivity.Tests.dll 절대 경로>'
```

현재 전체 선택 대상은 서버/Pi10개·로그인/근무 폼7개·메인 오후 지문2개·퇴근2개로21개다.
소유 MySQL·합성 회원/키·센서 대역 범위와 DLL SHA/자식 프로세스 정리 검사는 그대로 유지한다.
아래 R67-01과13/10개 결과는 이전 실행 이력이다.

직접 검증: alpha `6ab7a43` 제품 export와 이 수정 runner의 위 기본 명령으로 **21 passed in
63.44s**, exit0을 확인했다. 추가 import 옵션을 외부에서 주입하지 않았다. 로그인 DLL SHA는
`488ea958a4c381a6f0ca26edf7b45339993652000b1ae066ac52076f771a505f`, 근무/퇴근 DLL SHA는
`805be374553d483dfb71c598027101a87ca13334f8f1d974897339432eb460d5`이며 실행 전후 동일했다.
별도 기존 runner 정리 회귀7개도 통과했다(0.005초). 소유 MySQL·DB·runtime 정리를 확인했다.
실제 운영 서버·장비·개인 키·공유 DB·물리 두PC는 사용하지 않았다.

## 최신 수정 — R67-01 작성자 수정·재검토 대기, 2026-10-22

[팀장 R67-01/P2 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/67#issuecomment-6330831748)을 수신했다. 최초 runner는 Popen/Job 등록 뒤에 정리 try/finally가 시작되어 등록 실패 시 suspended 자식이 남을 수 있었다. 새 `capture_pytest()`가 기존 `owned.run_owned_client`에 생성부터 종료까지 위임한다. 이 helper는 child/job=None에서 시작해 Popen·등록/스레드 재개 이전부터 finally로 보호하고 Job close→소유 자식 stop/wait→stdout/stderr close를 수행한다. **공통 helper는 수정하지 않았다.** timeout은 실패 상태를 유지하며 새 자식/DB로 자동 재실행하지 않는다.

`python -m unittest discover -s Tests/FingerprintIdentity.Server.Tests -p test_run_check.py`: **7 tests PASS/0.003초**. 실제 기존 helper의 Popen/Job만 대역으로 바꾸고 원 stop/wait 코드를 실행했다. 등록 실패 시 spawn1/assign1/job.close1/terminate1/wait1·child alive=False·두 출력 스트림 closed를 확인했다. 등록 중 KeyboardInterrupt, 등록 뒤 resume 실패, 통신 중 KeyboardInterrupt, timeout, 정상 종료, Popen 실패도 검사했다. 실제 프로세스/DB/HTTP를 만든 실패 주입이 아니다.

수정 runner의 정상 연결은 동일 고정 source9c5c07c+95ef89d에서 **10 passed in30.28s**, exit0·새 MySQL 종료/DB 삭제·runtime 삭제 모두True다. 최신 alpha8cc0821의 서버/Pi 파일은 해당 검사 source95ef89d와 diff0이다. 대역7개와 실제 연결10개를 합친 단일 수치로 표시하지 않는다. `git diff --check`도 통과했다. 작성자 수정·게시 후 재검토 요청이며 팀장 해결 확인/원본 병합을 대신하지 않는다. 실제 서버/장비·키/설정·C# UI는 변경하지 않았다.

작성자5번 / 전달 대상1번·팀장. 가짜 issuer를 쓰던 기존110검사와 별도다. **1번 실제 identity HTTP·MySQL 서비스 + 5번 실제 Pi HTTP/runtime·서명/비교 코어**를 loopback TCP로 연결한다. 가짜인 것은 센서/UART·OLED와 합성 계정/장치·시험용 준비 상태다. 실제 C# 서버 adapter/LoginForm/MainForm, Windows↔실물Pi/두PC 검사가 아니다.

## 파일·실행 범위

### 선택: 실제 C# 로그인 폼까지 연결

`dotnet build Tests/IdentityLogin.Ui.Tests/IdentityLogin.Ui.Tests.csproj -c Release` 후 아래 runner 명령에 `--identity-login-client "<Tests/IdentityLogin.Ui.Tests/bin/Release/net8.0-windows/IdentityLogin.Ui.Tests.dll의 절대 경로>"`를 추가하면 C# LoginForm→지문 view→서버/Pi HTTP→실제 근태 저장/정상 접속 인수를 검사한다. matched·unregistered·실제 finalize 응답 유실 복구3개가 추가되어 기본10개와 합쳐13개 시나리오다. 계정/키·UART·readiness는 시험 대역이며 물리 장비/공유 배포가 아니다. DLL SHA256을 기록하고 검사 중 파일이 바뀌면 실패로 처리한다. [C# 검사 범위](../IdentityLogin.Ui.Tests/README.md)를 따른다.

- `prepare_source.py`: 고정한 Git alpha/서버 ref를 export한다. 사용자 checkout에 미병합 서버 코드를 섞거나 `.env`를 복사하지 않는다. 새 출력 폴더만 허용한다.
- `run_check.py`: 기존2번의 `OwnedMySQL`/Windows Job/식별·정리 도구를 **수정 없이** 재사용한다. `--no-defaults`·새 datadir·loopback 임시 포트·무작위 새DB를 소유하고 실제 datadir/port/InnoDB/RR 검증 후에만 생성/삭제/SHUTDOWN한다. 기존 MySQL 서비스에는 접속/종료하지 않는다. 격리 환경의 기본 DB URL은 즉시 제거·자기 MySQL로 교체하며 SQLite DB를 만들거나 사용하지 않는다.
- `test_server_pi.py`: 실제 두 HTTP 서버를 띄우고 서버가 낸 request/approval을 Pi가 검증하며, Pi 원문 result를 서버가 검증·보관한다. 공개 서버 fixture는 임시 MySQL에 합성 등록/계정·매핑만 준비한다. 센서 등록/삭제를 호출하는 시험이 아니다.
- `test_run_check.py`: R67-01의 프로세스 생성/등록·취소·timeout·정상 종료 정리 회귀7개. 표준 unittest로 따로 실행하며 실제 MySQL/프로세스를 만들지 않는다.

실행에는 Windows/MySQL8.2 binary와 전용 Python 검사 venv가 필요하다. **운영 서버 venv/개인 설정을 사용하지 않는다.** 외부 보고 폴더에는 안전한 `report.json`과 비공개 pytest 출력을 남기며 키·토큰·접속 URL·원문은 콘솔/커밋에 넣지 않는다.

```powershell
# 별도 검사 venv의 python.exe. 기존 운영 서버의 Python으로 대체하지 않는다.
$identityPython = '<test-venv>\Scripts\python.exe'
$identitySource = Join-Path $env:TEMP ('identity-source-' + [guid]::NewGuid().ToString('N'))
$identityReport = Join-Path $env:TEMP ('identity-report-' + [guid]::NewGuid().ToString('N'))
& $identityPython Tests/FingerprintIdentity.Server.Tests/prepare_source.py --alpha-ref 9c5c07c9aa6757c83a6d52a0c01cf7bcc3b85fc6 --server-ref 95ef89dfa8d9478e0cb6c3cb26282cd43c09a820 --output $identitySource
# 처음에는 해당 검사 venv에 export의 requirements-dev.txt와 기존 Pi tzdata를 준비한다.
& $identityPython -m pip install -r "$identitySource\RemoteWork.Server\requirements-dev.txt" -r "$identitySource\Devices\RaspberryPi\requirements-test.txt"
& $identityPython Tests/FingerprintIdentity.Server.Tests/run_check.py --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin' --source-root $identitySource --report-directory $identityReport
```

서버 ref의 Git object가 없다면 PR62를 read-only fetch한 뒤 **확인한 SHA**를 사용한다. 자동 최신 ref로 바꿔 기존 결과를 새 코드 검증으로 표시하지 않는다.

## 고정 조합과 충돌

최종 게시 전 조합은 **원본 alpha9c5c07c + 1번 PR62 95ef89d**이며 `merge-tree` 충돌0이다. 1번이 원본 통합을 이미 제공했으므로 시작 설정을 다시 수정/요청하지 않는다. 양쪽 SHA·tree를 manifest에 고정한다.

최초 검사 이력은 alpha18a4855(65/64 병합)+PR62 28cd8d9다. 이때 `.env.example`·`remotework/__init__.py` 두 곳에 충돌이 있었다. export 전용 준비기는 각각 한 충돌 블록/예상한 협업 guard와 identity 검증 블록인지 확인하고 **양쪽을 보존**했다. 예상과 다르면 중단한다. 결과 hash를 manifest에 기록하고 실행기가 대조한다. 이 최초 export 처리를 정식 충돌 해결/원본 병합 또는1번 동의로 기록하지 않는다. 현재 무충돌 조합과 구분한 과거 근거다.

## 확인 항목

1. matched와 MySQL 원문 보관·capture/result 별도 승인, GET 후 동일 결과 재전송·중복 센서 검색 금지. 응답 유실은 소비자가 실제 받은 응답을 버린 경우를 모형화한다.
2. 다른 등록 지문/미등록은 동일 no_match+null slot·서버 rejected, 요청 발급 유실은 원UUID GET/동일 본문으로 복구한다.
3. 검색 직후 현재 문맥 취소/회수 시 result 승인을 거절하고 성공 결과를 만들지 않는다.
4. 새 Pi runtime+새 서버 app/engine 뒤 같은 DB capture fence가 이전 요청의 새 probe를409로 막고 센서를 다시 읽지 않는다.
5. 변조 request는 센서를 열지 않는다.
6. **1번 AttendanceWorkflow 실제 코드**가 Pi 결과를 소비해 합성 회원의 출근·퇴근을 저장하고 같은 finalize UUID 재생, 암호화 token 보관·퇴근 후 세션 폐기를 처리한다.
7. 제어된 서버 UTC로 한국 시간11:00 전 차단·11:00~12:00 오후 업무 전 지문 확인·같은 열린 근무·중복 확인 차단을 확인한다. 이 시나리오의 합성 로그인 수명은 명시8시간으로 설정하며 운영 기한/인증 검사를 완화하지 않는다. 기존 `noon_check` 목적 이름은 유지한다.

`ready=lambda _: True`는 오직 새 임시DB·가짜 센서를 소유한 fixture의 대역이다. 이 코드/검사 통과를 운영 readiness ACK·실제 재시작 조정·개인키 설치·실물 로그인 성공으로 사용하지 않는다. 운영 서비스·007/사용DB·정식 DDL번호·등록 지문/회원 매핑은 바꾸지 않는다.

## 미완료·협업 상태

최초28cd8d9 당시 R62-01(외부DB FK 준비 검사)과 R62-02(C# pending 만료 후 결과 복구) 수정 요청이 있었고, 게시 전 **1번447d7a5/95ef89d 수정 코드를 수신**했다. 이 정상 연결10검사는 다른DB FK 또는 C# 만료 후 복구를 직접 검사한 것이 아니며 두 요청의 재검토 완료 판정을 대신하지 않는다. 실제 서버 배포·개인키/회수키·Linux ACL/BOOTTIME·운영 재시작 준비 상태, C# 현재 IdentityPending/정상 접속 adapter의실제서버 연결·화면·실물센서/두PC는 미실행이다. 이 단계는 다른 담당 제품을 수정하지 않고 독립 연결 근거를 준비하는 작업이다.

실행 결과/게시 SHA는 작업 보고와 승인 후 기존 PR58 대표 댓글에 한 번에 연결한다. 날짜 LOG는 팀장 취합 범위라 직접 게시하지 않는다. 새 코드 PR/회신은 사용자 승인 뒤 함께 보낸다.

## 직접 실행 결과 — 2026-10-22

Windows / Python3.13.15 / MySQL8.2 InnoDB RR / 최종 고정 **alpha9c5c07c+서버95ef89d에서10 passed in30.76s**. 소유 MySQL의DB 삭제·프로세스 종료와임시runtime 폴더삭제를 모두 확인했다. 앞선 alpha18a4855+28cd8d9의10통과/30.59초와새결과를 합산하지 않는다. 앞선조합의충돌해결파일hash도검사했고, 최종은충돌0이다. 다른담당 검사수치를5번직접검증에더하지않는다.

최초 정오 추가 실행은 테스트 helper의 기본 로그인1시간을 넘겨 서버 시계를2시간 이동해401로 실패했다. 정오 시나리오의 합성 로그인을 명시8시간으로 준비한 뒤 통과했다. 제품의 만료 검사/시간 제한은 변경하지 않았고 실패 실행은 통과 수에 넣지 않았다. source 준비기의 파일 쓰기는 LF로 고정해 Windows 줄바꿈과 manifest의 byte hash가 일치하도록 했으며, 최종 실행은 해당 hash 검사를 포함한다.

최초 변경은 이 폴더의4파일이었고 R67-01 정리 회귀1파일을 추가해 현재5파일이다. 제품·타 담당 코드/설정·DB·실제 회원/센서는 변경하지 않았다. 사용자 승인 범위인 기존 PR67에 수정/회귀 근거를 이어 게시하며, 최종 게시SHA/주소는 PR본문과 기존 PR58 대표 댓글을 따른다. 원본 병합·운영 적용은 별도다.
