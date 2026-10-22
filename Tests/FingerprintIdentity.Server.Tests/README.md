# 5번: 1번 서버와 Pi 코드의 실제 HTTP/MySQL 연결 검사

작성자5번 / 전달 대상1번·팀장. 가짜 issuer를 쓰던 기존110검사와 별도다. **1번 실제 identity HTTP·MySQL 서비스 + 5번 실제 Pi HTTP/runtime·서명/비교 코어**를 loopback TCP로 연결한다. 가짜인 것은 센서/UART·OLED와 합성 계정/장치·시험용 준비 상태다. 실제 C# 서버 adapter/LoginForm/MainForm, Windows↔실물Pi/두PC 검사가 아니다.

## 파일·실행 범위

- `prepare_source.py`: 고정한 Git alpha/서버 ref를 export한다. 사용자 checkout에 미병합 서버 코드를 섞거나 `.env`를 복사하지 않는다. 새 출력 폴더만 허용한다.
- `run_check.py`: 기존2번의 `OwnedMySQL`/Windows Job/식별·정리 도구를 **수정 없이** 재사용한다. `--no-defaults`·새 datadir·loopback 임시 포트·무작위 새DB를 소유하고 실제 datadir/port/InnoDB/RR 검증 후에만 생성/삭제/SHUTDOWN한다. 기존 MySQL 서비스에는 접속/종료하지 않는다. 격리 환경의 기본 DB URL은 즉시 제거·자기 MySQL로 교체하며 SQLite DB를 만들거나 사용하지 않는다.
- `test_server_pi.py`: 실제 두 HTTP 서버를 띄우고 서버가 낸 request/approval을 Pi가 검증하며, Pi 원문 result를 서버가 검증·보관한다. 공개 서버 fixture는 임시 MySQL에 합성 등록/계정·매핑만 준비한다. 센서 등록/삭제를 호출하는 시험이 아니다.

실행에는 Windows/MySQL8.2 binary와 전용 Python 검사 venv가 필요하다. **운영 서버 venv/개인 설정을 사용하지 않는다.** 외부 보고 폴더에는 안전한 `report.json`과 비공개 pytest 출력을 남기며 키·토큰·접속 URL·원문은 콘솔/커밋에 넣지 않는다.

```powershell
# 별도 검사 venv의 python.exe. 기존 운영 서버의 Python으로 대체하지 않는다.
$identityPython = '<test-venv>\Scripts\python.exe'
$identitySource = Join-Path $env:TEMP ('identity-source-' + [guid]::NewGuid().ToString('N'))
$identityReport = Join-Path $env:TEMP ('identity-report-' + [guid]::NewGuid().ToString('N'))
& $identityPython Tests/FingerprintIdentity.Server.Tests/prepare_source.py --alpha-ref 18a48555d3ef995afca53ed74b4cfb8f0e1dba73 --server-ref 28cd8d94108247ebc656be27e17a151da19795f5 --output $identitySource
# 처음에는 해당 검사 venv에 export의 requirements-dev.txt와 기존 Pi tzdata를 준비한다.
& $identityPython -m pip install -r "$identitySource\RemoteWork.Server\requirements-dev.txt" -r "$identitySource\Devices\RaspberryPi\requirements-test.txt"
& $identityPython Tests/FingerprintIdentity.Server.Tests/run_check.py --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin' --source-root $identitySource --report-directory $identityReport
```

서버 ref의 Git object가 없다면 PR62를 read-only fetch한 뒤 **확인한 SHA**를 사용한다. 자동 최신 ref로 바꿔 기존 결과를 새 코드 검증으로 표시하지 않는다.

## 고정 조합과 충돌

원본 alpha18a4855(65/64 병합) + 1번 PR62 28cd8d9의 검토 조합이다. `merge-tree`는 `.env.example`·`remotework/__init__.py` 두 곳에 충돌을 낸다. export 전용 준비기는 각각 한 충돌 블록/예상한 협업 guard와 identity 검증 블록인지 확인하고 **양쪽을 보존**한다. 예상과 다르면 중단한다. 결과 hash·양쪽 SHA·검토 tree를 manifest에 기록하고 실행기가 hash를 대조한다. 이 처리를 정식 충돌 해결/원본 병합 또는1번의 동의로 기록하지 않는다. 두 담당 guard/설정을 실제 PR에서 결합하는 것은 소유자 후속이다.

## 확인 항목

1. matched와 MySQL 원문 보관·capture/result 별도 승인, GET 후 동일 결과 재전송·중복 센서 검색 금지. 응답 유실은 소비자가 실제 받은 응답을 버린 경우를 모형화한다.
2. 다른 등록 지문/미등록은 동일 no_match+null slot·서버 rejected, 요청 발급 유실은 원UUID GET/동일 본문으로 복구한다.
3. 검색 직후 현재 문맥 취소/회수 시 result 승인을 거절하고 성공 결과를 만들지 않는다.
4. 새 Pi runtime+새 서버 app/engine 뒤 같은 DB capture fence가 이전 요청의 새 probe를409로 막고 센서를 다시 읽지 않는다.
5. 변조 request는 센서를 열지 않는다.
6. **1번 AttendanceWorkflow 실제 코드**가 Pi 결과를 소비해 합성 회원의 출근·퇴근을 저장하고 같은 finalize UUID 재생, 암호화 token 보관·퇴근 후 세션 폐기를 처리한다.
7. 제어된 서버 UTC로 정오 전 차단·정오 확인·같은 열린 근무·중복 정오 차단을 확인한다. 이 시나리오의 합성 로그인 수명은 명시8시간으로 설정하며 운영 기한/인증 검사를 완화하지 않는다.

`ready=lambda _: True`는 오직 새 임시DB·가짜 센서를 소유한 fixture의 대역이다. 이 코드/검사 통과를 운영 readiness ACK·실제 재시작 조정·개인키 설치·실물 로그인 성공으로 사용하지 않는다. 운영 서비스·007/사용DB·정식 DDL번호·등록 지문/회원 매핑은 바꾸지 않는다.

## 미완료·협업 상태

1번의 R62-01(외부 DB의 동명 FK를 storage 검사에서 구분하지 못함)은 별도 검토·수정 요청이며 이 정상 조합 검사가 해결 근거가 아니다. 실제 서버 배포·개인키/회수키·Linux ACL/BOOTTIME·운영 재시작 준비 상태, C# 현재 IdentityPending/정상 접속 adapter와 화면·실물 센서/두PC는 미실행이다. 이 단계는 다른 담당 제품을 수정하지 않고 독립 연결 근거를 준비하는 작업이다.

실행 결과/게시 SHA는 작업 보고와 승인 후 기존 PR58 대표 댓글에 한 번에 연결한다. 날짜 LOG는 팀장 취합 범위라 직접 게시하지 않는다. 새 코드 PR/회신은 사용자 승인 뒤 함께 보낸다.

## 직접 실행 결과 — 2026-10-22

Windows / Python3.13.15 / MySQL8.2 InnoDB RR / 위 고정 alpha18a4855+서버28cd8d9 조합에서 **10 passed in 30.59s**. 소유 MySQL의 DB 삭제·프로세스 종료와 임시 runtime 폴더 삭제를 모두 확인했다. source manifest의 충돌 해결 파일 hash도 실행 전 대조했다. 이전9개 검사·다른 담당 검사와 합산하지 않는다.

최초 정오 추가 실행은 테스트 helper의 기본 로그인1시간을 넘겨 서버 시계를2시간 이동해401로 실패했다. 정오 시나리오의 합성 로그인을 명시8시간으로 준비한 뒤 통과했다. 제품의 만료 검사/시간 제한은 변경하지 않았고 실패 실행은 통과 수에 넣지 않았다. source 준비기의 파일 쓰기는 LF로 고정해 Windows 줄바꿈과 manifest의 byte hash가 일치하도록 했으며, 최종 실행은 해당 hash 검사를 포함한다.

이번 변경은 이 폴더의 준비기/실행기/검사/안내4파일뿐이다. 제품·타 담당 코드/설정·DB·실제 회원/센서는 변경하지 않았다. 로컬 검사 완료이며 코드 push/새PR·외부 회신/원본 병합·운영 적용은 아직 하지 않았다.
