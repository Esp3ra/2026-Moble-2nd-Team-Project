# 5번 MySQL 전용 등록 클라이언트

2026-10-21 / 5번 / 기준 alpha `bb281e317cdd27e2a4796ad39d8712bbddd9828b`.
서버 대조 기준은 [1번 PR55](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55)의
`0871b2ce80725587e8c1960d0874f5dc191cf46e`,
[고정 계약](https://github.com/jinkyu1452-cmd/Sang1/blob/0871b2ce80725587e8c1960d0874f5dc191cf46e/docs/ENROLLMENT_MYSQL_DURABILITY.md)이다.
확인 시 PR55는 미병합이며 실제 서버의 새 옵션/010은 미적용이라는 1번 보고를 수신했다.

## 구현한 범위

- `fingerprint_enrollment_mysql.py`: 기존 두 채취 코어/같은 센서 잠금을 사용한다. 최초 서버 receipt 조회 → `sensor_capture` 현재 승인 → 두 채취/중복/빈 슬롯 검사 → `sensor_store`의 저장 전 MySQL commit 응답 → 단일 센서 저장 → 고정 서명 결과의 `physical-result` 보관 → 현재 `result_read` 승인/동일 원문 전달 순서다.
- 저장 승인에서는 `may_store`의 실제 bool true와 signed 승인 원문의 MAC·장치/키/작업·claim/revision·새 RAM nonce·scope·2초 RTT/최대5초 수명을 모두 확인한다. 승인 재전송의 false/null은 재저장 허가가 아니다. capture 승인으로 store하지 않는다. 저장 허가 후에는 capture 재발급을 요청하지 않으며, 남은 짧은 store 승인 안에서만 저장/물리 확인을 수행한다. 느린 센서는 확인 필요로 중단할 수 있어 실물 계측이 남아 있다.
- Pi 실행 중의 결과 ID/경과시간/서명 원문은 RAM에서 한 번 고정한다. 서버 전송 실패 시 같은 실행의 동일 원문만 재전송한다. 서버 보관 뒤 재시작하면 원문 MAC/바인딩과 새 result_read 승인을 검사해 동일 bytes를 반환한다. intent만 있고 RAM 원문이 없으면 `recovery_required`로 멈춘다. 센서 재저장/결과 재생성/자동 슬롯 변경은 없다.
- store 전 중복/다른 손가락/빈칸 충돌도 정해진 실패 서명으로 보관할 수 있다. 실패 서명 보관 자체는 예약 해제·새 시도 승인·회원 매핑이 아니다.
- 윈폼 `FingerprintEnrollmentRelay`는 서버 physical receipt를 먼저 조회한다. 미지원 계약/바인딩 오류면 Pi에 요청하지 않는다. 기존 intent 또는 불확실한 Pi 전송 뒤에는 읽기만 요청한다. 원문을 MySQL에 기록한 뒤 `/complete`를 호출하고, 서버 `completed`에서만 완료로 표시한다. 실패/불명 결과는 완료로 표시하지 않는다. 원래 관리자 접속·생성 응답 회원 ID·화면 수명과 취소를 유지한다.
- `MySqlEnrollmentRuntime`은 기존 UART/OLED/잠금을 빌린다. 등록 중 scan은409, 조도 조회/수신은 유지한다. OLED의 ‘지문 저장됨/계정 연결 확인 중’은 최종 등록 완료가 아니다.

## Pi↔PC 중계의 추가 접점

기존 `/enrollments/jobs`의 RAM capability/요청 수명과 `/approval` 응답 운반 경로를 재사용한다. PC에는 장치 키를 주지 않으며 Pi에는 관리자 Bearer나 DB 접속값을 주지 않는다.

| Pi challenge | PC가 호출할 고정 서버 경로 | Pi에 반환하는 decoded bytes |
|---|---|---|
| 기존7필드, scope=sensor_capture/result_read | POST `/api/v1/device-enrollments/{고정job}/approval` | 원문 signed approval |
| 기존7필드, scope=sensor_store | POST `.../{고정job}/storage-intent` | 서버 data JSON 전체, 내부 approval 원문 유지 |
| action=read_physical_result, operation_revision, probe_nonce | GET `.../{고정job}/physical-result` | 서버 data JSON |
| action=record_physical_result, operation_revision, probe_nonce, result_envelope | POST `.../{고정job}/physical-result`, 기존2필드 body | 서버 data JSON, 동일 result bytes 확인 |

중계 challenge의 `probe_nonce`는 RAM 운반 응답을 대응시키는 값이다. RPC JSON 자체는 signed store 승인이 아니다. 읽기 challenge만 원래 티켓이 없어 operation_revision=null일 수 있고 PC는 이미 고정된 작업 revision/경로를 사용한다. PC가 URI/회원/장치/slot을 Pi 입력으로 바꾸지 않는다. 운반 응답은 기존 approval_envelope 필드에 base64url로 싣되, RPC는 최대32768 decoded bytes, 실제 MAC envelope는 기존16384 제한을 유지한다. 구형 `sensor_write`는 새 PC에서 차단되고 새 Pi는 이를 발급 요청하지 않는다. 구형 PC도 새 RPC challenge를 소비하지 못하므로 저장 승인으로 진행하지 않는다.

## 시작 설정과 실제 전환의 경계

새 후보 실행 파일은 `pi_api_enrollment_mysql.py`, 형식 예시는 `enrollment.mysql.example.json`이다. version2와 contract=enrollment-mysql-v1을 명시해야 하며 공개 예제에는 유효 키/프로필이 없다. 기존 v1 설정을 자동 변환하지 않는다.

Linux 소유자 전용 설정 파일/부모 폴더 검사를 유지한다. state_directory는 해당 비공개 설정 부모의 `state`로 제한하고 **기존 runtime.lock과 같은 flock**을 서비스 종료/호스트 자원 정리까지 잡는다. 별도 새 잠금 폴더로 이전 writer를 우회하지 않는다. UART도 기존 호스트의 exclusive=True 단일 소유를 사용한다. 준비된 lock이 없거나 기존 `*.sqlite3`/journal 증거가 남아 있으면 자동 생성/삭제/무시하지 않고 시작을 거절한다. 새 실행 경로를 import해도 SQLite/serial 모듈을 로드하지 않으며 DB 결과 파일을 만들지 않는다.

**이번에는 실행 중인 서비스·설정·키·센서·MySQL을 바꾸지 않았다.** 기존 SQLite 파일/서명 결과와 이전 소스는 이관 근거로 보존한다. 과거 `pi_api_enrollment.py`/v1 준비 helper는 이전 구현 이력이며 추가 배포/등록에 사용하지 않는다. 새 코드를 단순 복사해 기존 서비스에 덮어쓰면 안 된다.

실제 전환은 다음 확인 후 사용자와 별도로 진행한다.

1. 1번의 비공개 실패 작업 대조 ZIP과 실제 Pi 기록의 작업/장치/회원 바인딩을 비공개로 대조한다.
2. 기존 PC/Pi writer를 종료/차단하고 비공개 백업·시각/소스/서비스 근거를 보존한다. 그 이후 새 읽기 inventory로 슬롯12/전체 목록을 확인한다. 이번 코드는 이 실물 확인을 수행한 것이 아니다.
3. 기존 ledger/cache를 읽기 전용으로 검증하고, 존재하는 signed 원문만 현재 키/바인딩/보관기간을 검증해 1번 운영자 CLI로 증거 보관한다. 원문이 없는 실패 행은 서명 결과로 재생성하지 않는다. 실물 읽기 전용 점검 결과 signed 원문은 없었다. 기존 파일을 비공개 백업하고 작업 metadata를 대조했지만 회원 서명 바인딩 검증이나 서버 이관 완료를 뜻하지 않는다.
4. 1번이 원래 작업/예약 이력을 보존하는 명시 복구·재발급 변경안을 제공하고 5번 실물 근거와 대조한다. PR55 head0871b2c가 운영자 종료·명시 재발급/011을 제공했으며 검토·실제 적용은 별도다. 센서 삭제 API는 없다.
5. 서버/Pi/PC의 승인 SHA·010·새 옵션·설정·기존 증거 보존 위치·같은 단일 서비스 전환/복구안을 확인한 뒤 활성화한다. 원발급 접속이 끝난 작업을 새 로그인으로 자동 인수하지 않는다. 재시작 후 사용자의 기존 시도 문맥을 윈폼 화면이 자동 복원하는 기능도 이번에 추가하지 않았다.
6. 사용자 목표인 시험 템플릿 초기화→빈 목록→회원 생성 응답 ID 고정→새 두 채취→MySQL 매핑 commit을 실물에서 검증한다. 용량300만 보고 허용 슬롯을 확대하지 않는다. 현재 단일 슬롯 승인과 초기화 후 허용 범위는 별도 대조한다.

공유 서버 변경/원본 병합/실물 전환/기존 증거 이관 완료는 로컬 코드 검증과 구분한다. 새 협의 MD/LOG PR은 발행하지 않았고 이 문서는 소유 코드의 기술 설명이다. 날짜 LOG는 팀장 전용이라 작성하지 않았다. 현재 게시 상태는 관련 코드 커밋/PR과 PR43 기존 통합 회신에서 확인한다. 코드 게시가 원본 병합이나 실제 배포를 뜻하지 않는다.

## 로컬 검증

테스트와 실제 실행 결과는 아래 최종 검사 기록을 따른다. 서버는 PR55 형식의 합성 대역이며 새 MySQL 테이블을 실제 연결/적용한 결과가 아니다. 기존 legacy 회귀는 이전 동작 보존을 확인하기 위해서만 실행하며 새 MySQL 경로의 저장소로 SQLite를 주입하지 않는다. 신규 실물 센서 저장/삭제·새 Pi 런타임 서비스 전환·새 MySQL 왕복·전원 차단·두 PC·서버 증거 이관은 미실행이다. 아래 읽기 전용 실물 복구 점검과 구분한다.

2026-10-21 작성자 Windows/Python3.13.15·.NET SDK10.0.302, NET8 대상:

```text
python -X utf8 -W error::ResourceWarning -m unittest discover -s Devices/RaspberryPi -p 'test*.py' -q
게시 대상 파일만 내보낸 독립 폴더에서170개 실행 / 168통과 / Windows에서 Linux flock 2개 생략 / 실패0 (3.605초)

dotnet run --project Tests/FingerprintEnrollment.Tests/FingerprintEnrollment.Tests.csproj -c Release -- --pi-python <검증 가상환경 python.exe>
283 checks PASS: 실제 C#→loopback HTTP→새 Python adapter/가짜 센서,
합성 서버 signed 승인·원문 보관/재조회·구규약 차단·intent 읽기·응답 유실 포함

dotnet run --project Tests/FingerprintEnrollment.Ui.Tests/FingerprintEnrollment.Ui.Tests.csproj -c Release
15 checks PASS: 실제 WinForms/합성 HTTP, 완료 전 표시·중복 지문·닫기/로그아웃

dotnet build RemoteWork.WinForms/RemoteWork.WinForms.csproj -c Release --nologo
경고0 / 오류0 (실행 중 앱을 덮어쓰지 않는 별도 임시 출력 폴더)

git diff --check
통과
```

새 Python 검사에는 capture/store 분리, 같은 permit 재사용·잘못된 MAC/nonce/scope/바인딩·RTT·만료 거절, bool이 아닌 may_store 거절, store 전 중복·다른 손가락·슬롯 충돌, store 응답 유실/목록 불일치, 결과 전송 전후 응답 유실, RAM 유실 때 intent 차단, 동일 원문 재시작 조회, 단일 writer·공유 OLED/scan/motion, SQLite 없는 import/실행 경로와 새 설정 차단을 포함한다. 전체170은 기존 검사를 포함한 수치며 다른 실행과 합산하지 않는다. 복구 점검기19개도 이170개에 포함되며 별도 합산하지 않는다.

중간 전체 검사에서 느린 store 응답 대역이 뒤따르는 result_read까지 느리게 만들어 결과 전달도 정상 차단됐는데 이를 반환 성공으로 기대한 fixture 오류1개가 있었다. 지연 주입을 store 한 번에만 적용하도록 대역을 고쳤고 이전 전체168 재검사에서 해결했고 게시 대상170개 검사에서도 통과했다. 제품의 RTT/만료 방어를 완화하지 않았다. 실제 서버 MySQL180개는 1번의 별도 보고이며 이번5번 실행 수치가 아니다.

## 실물 복구 점검과 현재 상태 — 2026-10-21

사용자가 Windows helper에서 SSH/sudo 암호를 입력해 `fingerprint_recovery_check.py`를 실행했다. 이 소유 도구는 기존 서비스 중지·지속 시작 차단·기존 단일 lease 확보·비공개 백업 검증·UART 점유 확인 뒤 센서 parameter/count/index만 읽는다. 센서 채취·저장·삭제, 서버 HTTP/DB 변경, 자동 재시작 기능은 없다. 기존 SQLite 파일은 생성/수선 없이 immutable/read-only 증거로만 해석한다. 새 MySQL 실행 경로에서는 이 검사기를 import하지 않는다.

최초 시도는 보호된 systemd 폴더를 일반 사용자로 읽다가 중단됐다. 원인 확인 후 사용자에게 보이는 sudo 인증과 root 소유/링크/크기 검사를 추가했고 폴더 권한은 열지 않았다. 최종 사용자 실행에서 다음을 확인했다.

- 서비스 inactive/MainPID0, 기존 서비스 조건 hold 유지, 비공개 원본/백업 hash 대조 완료.
- stopped_at=2026-10-21T00:38:50.387494+00:00 → inventory observed_at=00:38:50.558597+00:00 → checked_at=00:38:50.566980+00:00. 한국 시각09:38:50의 중지 후 관찰이다.
- 보고 용량300/템플릿11개/count-list 일치/예약12 빈 상태. 기존 시험 지문은 유지됐다.
- 작업 metadata 일치, ledger failed/already_registered, signed result 없음, target cryptographic binding 미검증. 없는 원문/서명을 새로 만들지 않았다.
- 서버 변경·센서 삭제·서비스 재시작은 수행하지 않았다. 기존 키·원본 기록·소스·백업은 보존한다.

비공개 report 원본은 사용자 SCP 후 직접 읽어 위 시각과 상태를 대조했다. 공개 코드에 실제 작업/회원/장치 식별값·점유 목록·private 경로·키/보고서 원문을 넣지 않는다. 필요한 metadata와 원본 report hash만 별도 비공개 묶음으로 준비했고, 1번의 후속 회신에서 개인 수신·묶음 내부 hash와 현재 서버 바인딩 대조 보고를 확인했다. 1번이 Pi 원본 report 자체/회원 서명 바인딩까지 직접 검증한 것으로 확대하지 않는다.

[1번 최신 수신 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6288126821)은 이 점검 보고를 수신했으며 동일 검사를 다시 요구하지 않는다. 후속 head0871b2c에서 운영자 관찰 증거와 기존 job/claim/revision 이력을 보존하는 명시 복구·재발급 접점이 제공됐다. 1번은 받은 비공개 요약 hash/서버 바인딩 대조와 작성자 MySQL 검사를 보고했지만 실제 종료/재발급·010/011 적용은 아직 수행하지 않았다고 명시했다. 아직 예약 복구·010 활성화·새 Pi 런타임 배포·SQLite 증거 정리/이관·시험 지문 초기화·신규 회원 매핑은 완료되지 않았다. 기존 hold를 임의로 풀거나 서비스를 시작하지 않는다.

## 게시 범위와 재현

이번 변경은 MySQL 클라이언트/중계·중복 지문 안내와 읽기 전용 복구 점검기다. 기존 untracked v1 시작 파일·SQLite 설정/준비 helper·그 예제/테스트는 새 게시물에 포함하지 않는다. 새 시작 테스트는 이전 SQLite 시작 테스트에 의존하지 않는다. 기존 커밋의 legacy 코어/adapter는 회귀와 과거 증거 해석을 위해 보존하되 MySQL 런타임은 로드하지 않는다.

기술 차이·대역 구성·위 실제 실행 근거를 1번에게 전달하고, 양쪽 게시 코드 결합 검증과 명시 운영 적용을 별도로 진행한다. 날짜 LOG는 팀장 전용이므로 직접 올리지 않고 기존 PR43 통합 회신을 기존 LOG/1번·5번에 취합 요청한다. PR 브랜치의 기존 5번 수정 요청은 해결된 과거 검토이며 이번 신규 차단 요청은 없다.

게시 전에는 Git index의24개 변경과 기존 HEAD만 별도 임시 폴더로 내보내 위 Python170·C#283·윈폼15와 Release 경고0/오류0을 직접 확인했다. 이전 미게시 SQLite 시작 파일이 없는 상태의 재현이다. 현재 바탕화면 실행본이나 Pi 파일을 이 검사로 갱신하지 않았다.

## 명시 복구 소비/UI 후속 — 2026-10-21

[1번 복구 계약](https://github.com/jinkyu1452-cmd/Sang1/blob/0871b2ce80725587e8c1960d0874f5dc191cf46e/docs/ENROLLMENT_RECOVERY.md)을 직접 읽고 기존 5번 등록창/중계에 다음을 추가했다. 서버 운영자 CLI·011·공통 회원 화면/launcher·실물 센서는 변경하지 않았다.

1. 현재 생성 응답 회원ID/관리자Session을 가진 기존 등록창에서 운영자 복구 모드를 명시적으로 선택한다. 운영자가 종료 적용 후 전달한 recovery_id를 넣고 **복구 작업 확인**을 누른다. GET recoveries/{id}의 정확한 wrapper/11필드·UUID·revision·UTC·operator_observation 상태와 선택한 장치를 검사한다. 이 조회는 새 작업/채취/저장을 시작하지 않는다.
2. 현재 회원ID와 새 등록 목적을 화면에 표시한다. 사용자가 별도로 **복구 승인으로 지문 등록**을 눌렀을 때만 reissue에 client_request_id/target_user_id 두 필드를 보낸다. 장치/slot/빈 상태·기존 접속정보를 body로 보내거나 원래 일반issue로 우회하지 않는다.
3. recovery/source job/device/slot/source revision/종료시각 불변, 새 job 링크·다른 UUID·operation1·claim+1·현재 선택 key epoch·고정 request UUID를 검사한 후 기존 MySQL capture→intent→store→result→complete로 연결한다. 회원/관리자 접속 교체·취소·화면 종료 뒤 결과를 다른 문맥에 반영하지 않는다.
4. 발급 응답 유실은 GET recovery→연결된 새 job을 조회하고 원래 request UUID와 대조한다. 미발급이면 사용자 재확인까지 중단하며 같은 attempt의 같은 요청만 재사용한다. 임의 UUID/일반issue/oldsession 자동 인수는 없다. 앱 종료·새 로그인 뒤 이전 attempt를 자동 복원하지 않는다. 이미 재발급된 recovery_id를 새 창에서 처음 읽으면 인수를 거절한다.

이 후속은 사용자 전송 전 보고를 마친 뒤 회신/코드 PR 게시 승인을 받았다. 최종 게시 SHA·PR 상태는 코드 PR과 기존 PR43 통합 회신에 기록하며 이전1b70715 게시 상태와 구분한다. 게시 승인은 실제 서버/Pi 활성화·초기화 실행 완료가 아니다. 1번은 기존 MySQL adapter→실제HTTP/소유MySQL 추가 연결2통과를 보고했지만 이번 새 C# recovery/UI의 직접 MySQL·실물 검증으로 합산하지 않는다. PR55 R55-01/02/03은 작성자 수정 보고·팀장 재검토 대기이며 5번이 해결/병합 확인으로 표시하지 않는다.

### 기존 회원 생성 검사에서 발견한 별도 호환성 항목

저장소 원본 UserManagement.Ui.Tests의 회원관리14시나리오46검사는 진행됐지만 등록 연결은 Modal registration driver 시간초과로 실패했다. 이전 게시1b70715의 form/relay로 비교해도 같은 실패를 확인했다. 기존 테스트 서버가 physical-result 경로를 제공하지 않는 것이 원인이며 새 복구 UI로 생긴 회귀라고 쓰지 않는다.

1번 소유 원본 테스트 파일은 변경하지 않았다. 독립 임시 복사본의 모의서버에 GET/POST physical-result와 exact 결과 선보관 검사를 추가한 제안 patch만 준비했고, 최신5번 코드와 실제 회원관리/등록창/launcher에서 연결120검사·회원관리14시나리오46검사가 통과했다. 원본 검사 통과로 보고하지 않으며 1번에게 자기 테스트 fixture 반영/재검증을 요청하는 회신에 제안 patch와 함께 전달한다. 실제 서버·계정·MySQL·센서·서비스 설정은 바꾸지 않았다.

### 후속 최종 직접 검사

게시 대상 추출본 위에 이번 소유 C# 변경만 반영한 독립 임시 폴더, Windows/.NET SDK10.0.302·NET8:

- `dotnet run --project Tests/FingerprintEnrollment.Tests/FingerprintEnrollment.Tests.csproj -c Release -- --pi-python <기존 테스트 venv python.exe>`: **540검사 PASS**, 경고 없음. 기존283을 포함한 최종 실행이며 합산하지 않는다.
- `dotnet run --project Tests/FingerprintEnrollment.Ui.Tests/FingerprintEnrollment.Ui.Tests.csproj -c Release`: **31검사 PASS**, 실제 WinForms/합성 HTTP. 준비 화면을 별도 임시 capture로 눈으로 확인했고 안내/버튼 잘림 없이 표시됐다. UI capture는 공개 fixture 화면이며 제품/운영자 실제 ID가 아니다.
- `dotnet build RemoteWork.WinForms/RemoteWork.WinForms.csproj -c Release --nologo`: **경고0/오류0**.
- 기존 UserManagement 원본 검사 실패와 fixture 제안 적용 복사본의 연결120·회원관리14시나리오46 PASS는 위에 구분했다. 실제 서비스 코드/API를 완화해 기존 fixture에 맞추지 않았다.
- Python 제품 소스는 이번 복구 소비 후속에서 변경하지 않았다. 기존170개 검사 결과를 오늘 새 실행으로 재사용/합산하지 않는다. 실제 새 서버·MySQL·운영자 종료/reissue·Pi/PC 배포·센서 초기화/등록은 미실행이다.
