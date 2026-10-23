# 5번 인계: RW-ID-v2 본인인증 계약 2.0

## 2026-10-23 관리자가 승인한 기존 지문 계정 연결 이전

`transfer-enrollment-owner`는 서버 로컬의 명시 관리 명령이다. 기존 회원을 새로 만들거나
센서의 지문을 저장·삭제하지 않는다. `enrollment_jobs.target_user_id`와 원 signed 결과는
최초 등록 대상의 역사적 증거로 유지한다. `enrollment_mappings.user_id`는 현재 인증 대상이며,
이전 시 같은 mapping/slot의 revision을 1 증가시키고 전후 사용자·사유·원 요청UUID와 감사를
같은 transaction에 기록한다. 원 claim/등록·근태·capture fence·키/epoch/프로필은 바꾸지 않는다.

명령은 활성 관리자 및 해당 장치의 명시 등록 권한, 활성 양쪽 계정, 현재 연결/기대 revision,
원 completed 등록 증거와 전체 이관 이력을 현재 locking read로 확인한다. 대상 계정의 다른
연결, 같은 장치의 미완료 등록/인증, 양쪽 계정의 열린 근무가 있으면 거절한다. 만료된 issued도
자동 정리하지 않는다. 같은 요청UUID·내용은 영구 보관된 결과를 반환하고, 다른 내용의 재사용은
거절한다. 감사 실패·충돌은 사용자 변경과 이관 기록까지 rollback한다. 미사용 pending login만
철회하고 정상 일반 로그인 세션/완료 receipt 문맥을 임의 삭제하지 않는다.

새 HTTP endpoint/DTO와 C#·Pi 변경은 없다. 새 인증 요청의 기존 `mapping_revision`/`expected_slot`
계약을 그대로 사용한다. 이전 회원은 새 센서 승인·출근을 할 수 없고 새 회원은 증가한 revision으로
새 요청을 발급받는다. 이미 workflow_completed인 원 요청에 한해서 이전 이력의 원 사용자·revision을
확인하여 기존 result 조회/동일 complete·동일 finalize UUID replay를 허용한다. 새 승인·취소·다른 finalize
UUID와 다른 회원으로의 결과 승계는 허용하지 않는다. 기존 만료/비밀번호·활성/현재 장치·키 검사는 유지된다.

### 명시 DB 준비·배포 순서

번호 없는 [이관 DDL 검토안](../RemoteWork.Server/migrations/drafts/enrollment_owner_transfer.mysql.sql)은
기존007을 수정하지 않는다. 이관 이력 표 1개를 추가하고 기존 mapping의 4열 복합FK를
등록/장치/slot 3열 FK로 한 ALTER에서 교체한다. 사용자 FK와 슬롯·현재 사용자 UNIQUE는 계속 강제된다.
원 등록 대상은 job의 기존 target_user_id에 남는다. FK 검사 해제·과거 completed job 변경으로
이전을 흉내 내지 않는다. 정식 DDL 번호는 팀장 취합 전 확정 번호로 표시하지 않는다.

1. 전체 DB·외부 파일·비공개 설정 백업과 독립 MySQL 복원/DDL·행·파일 대조.
2. 복원 DB에서 DDL 및 동일 대상 이관·원 요청 재실행 검증.
3. Pi 단일 서비스를 기존 절차로 clean stop/drain하고 5000 서버도 종료.
4. 현재 미확정/열린 근무·원 연결 재검사 후 DDL 명시 적용. 부분 설치는 원인 확인 전 재실행하지 않는다.
5. 같은 DB/비공개 프로필을 사용하는 앱의 CLI에서 명시 실행:

```text
transfer-enrollment-owner --actor-login <관리자> --from-login <기존회원> --to-login <대상회원>
  --device-id <현재장치UUID> --slot <기존슬롯> --expected-revision <기대값>
  --request-id <변하지않는새UUID> --reason <사용자승인사유>
```

6. 보존 항목/현재 연결·revision·이력/감사/동일 요청 결과를 직접 대조하고 같은5000 서버 재시작.
7. 변경된 서버 소스 pin과 새 ON 회차 묶음을 5번에게 비공개 전달. 기존 Pi 전환·복구 절차로
   단일 서비스에 재연결하고 대상 회원의 실제 지문 입력·로그인 확인. 서버 검사만으로 실물 완료로 쓰지 않는다.

복구도 원 UUID와 이력을 보존한다. 성공한 이전을 숨기기 위해 행을 삭제하거나 직접 user_id를
되돌리지 않는다. 필요하면 명시적인 반대 방향 이관을 새 UUID/기대 revision으로 수행한다.
구버전으로 돌아갈 때도 현재 소유자와 원 job 대상이 다른 상태에서 옛 4열FK를 재설치할 수 없으므로,
승인된 역이관·이력/DDL 호환성을 먼저 확인해야 한다. 실제 운영 DB 전체 restore/DROP은 별도 판단이다.

## 2026-10-22 후속: 정상 요청 drain과 원 퇴근 결과 소유권

1번 제공: `ApiClient.Session.BeginClockOutFinalizationAsync(workId,workDate,challengeId,
originalFinalizeId,ct)` → `ApiClient.ClockOutFinalization`. 이미 서버가 verified/pending/clock_out으로
확인한 도전만 수용하며 새 센서 검색/도전/승인이나 정상 token 인수는 하지 않는다.
기존 identity challenge GET으로 목적을 재검사한 뒤 현재 로그인 lane을 닫고, 이전 일반
HTTP 요청을 취소하고 완료를 기다린다. 그동안 모든 일반 Session/API 호출은 취소되며,
늦은 취소 응답401이 원 퇴근 결과 소유권을 지우지 못한다. 실제 복구 요청의401은
기존처럼 인증을 종료한다. 전역401 무시나 업무권한 연장은 없다.

반환된 소유자는 원 challenge/UUID/body의 `CompleteAsync(ct)`만 제공한다. 같은 최초
작업/최소 receipt를 RAM에 보관하고 새 동작/일반 GET·POST/token 추출 기능을 제공하지 않는다.
서버의 더 짧은 기한/철회/비밀번호 변경·권한 회수/401이 우선하며 로컬 상한은 준비 시점부터
최대24시간이고 재시도로 연장되지 않는다. 정상 로그아웃·서버 변경·같은 계정 재로그인도
이전 세대를 취소하며 같은 문자열 토큰이어도 새 접속으로 결과를 넘기지 않는다.
영수증의 completed·원 work UUID/DATE·정규 receipt UUID와 토큰 미포함을 확인한다.
결과 불명에는 같은 소유자의 CompleteAsync로만 재확인하고, 성공 캐시는 추가POST하지 않는다.

일반 요청을 취소했다는 것은 서버에서 진행되던 업무 TX가 취소됐다는 증거가 아니다.
서버 공통 guard와 퇴근/AuthSession 폐기 TX가 최종 직렬화·인가를 담당한다.
미송신 소유자 Dispose는 일반 통신을 다시 허용하지만 한 번이라도 finalize를 보냈으면
Dispose 시 현재 해당 로그인만 폐기한다. 미확정 퇴근 뒤 일반 업무를 자동 재개하지 않는다.
다른 접속으로 바뀐 뒤 옛 Dispose/EndSession은 새 로그인을 지우지 않는다.

### MainForm 접점과5번 소비 방법

`MainForm.PrepareClockOutFinalizationAsync(workId,date,verifiedChallenge,originalFinalizeId,
stopWorkdayAndWait,ct)`는 표시된 로그인UI에서만 호출한다.5번이 소유한 monitor/근무 poll
종료·대기 callback을 먼저 await하고 공통 drain을 연결한다. 반환된 소유자는 로그인
component가 보관하므로 빌린 지문창/메뉴를 닫아도 폐기되지 않는다.
준비/미확정 보관 중에는 기존 주기 auth/me를 전송하지 않는다.

5번은 `FingerprintWorkdayOperation`의 **verified clock_out 이후 finalize 경계**에만
이 접점을 소비한다. 원 `OriginalFinalizeId`, 근무일/UUID, Attempt의 원 challenge를 전달하고,
기존 일반 `session.PostAsync(.../finalize)`를 이 소유자의 `CompleteAsync`로 대체한다.
새 operation/UUID나 일반 토큰으로 복구하지 않는다. 정오 경로는 기존 정상 Session 그대로다.
영수증을 사용자에게 표시한 뒤 `MainForm.FinishConfirmedClockOut(같은소유자)`가 현재
로그인·동일소유자·검증receipt를 확인하고 공통알림/타이머를 정리한 뒤 EndSession/화면종료한다.
퇴근 commit 후 auth/logout을 다시 보내지 않으며 단순 창닫기/일반로그아웃을 퇴근으로 표시하지 않는다.

**이것은 명시 소비 접점이다.**5번 Form/Operation 코드를 대신 교체하거나 기본 시작 경로에
자동 설치하지 않았다. 기존5번 operation의 직접 Session finalize는 이 새 소유권 연결을
소비하기 전 상태다. UI 소유자가 같은RAM작업을 재사용하도록5번 연결·결합검증이 필요하다.
카메라를 중단한 후 사전검사가 실패했다면 실제 근무를 다시 읽고 소유자가 명시 재연결해야 하며
정지한 monitor를 공통 코드가 임의 복구하지 않는다. 강제 종료/재시작 뒤 RAM복구는 제공하지 않는다.

### 다섯 묶음의 현재 경계

| 묶음 | 제공/다음 담당 |
|---|---|
| 현재근무 | PR71의WorkdayStateConnection/GET 제공 유지.5번PR72의projection/명시reader소비와대조 |
| 정오 |5번PR72의서버시각전환/원작업/응답유실복구 공급.1번이같은동작을재구현하지않음 |
| 퇴근 |이번공통drain/원finalize소유/MainForm접점제공.5번verified→finalize경계소비후결합검증 |
| 활동 |5번로그인소유monitor/StopAsync를callback으로연결.활동저장API/DB는미제공·로컬판정유지 |
| 실물/운영 |실제identity6테이블/프로필·분리키묶음/회수·1년정리/백업복원/Linux서비스·readiness·두PC는별도운영준비.이번실DB/실키/장비·활성화없음 |

공유 새지문은OFF를유지한다.실제설치시간/장치준비를추정하지않는다.현재개발·시연은
같은네트워크의서버사설IP+PORT직접접속이며Tailscale/VPN/터널·외부HTTPS는선행조건이아니다.
초기등록/완료mapping을재초기화하거나시험ready=True를운영승인으로쓰지않는다.

검사: `Tests/ClockOutRecovery.Tests`12시나리오35검사, 기존 `Tests/ApiClient.Tests`29시나리오144,
`Tests/ClockOutOwner.Tests`실제MainForm11검사, `test_clock_out_recovery_client.py`실제C#/Flask/
소유MySQL2검사(정상·성공응답유실,AuthSession0/근무ended/receipt1대조).
센서/키·회원/열린근무·서명결과는fixture이며실물/공유서버·5번새경계소비완료가아니다.
검사자식은기존ownedJob helper로정리한다.최초UI검사listener종료대기는ConfigureAwait(false)로
시험서버만교정했고제품성공수치에실패시도를합산하지않는다.

## 2026-10-22 추가: 재접속 후 현재 근무 조회 — 구현 제공·5번 대조 대기

`GET /api/v1/device-identity/workday`를 identity blueprint에 추가했다. 새 개발
접점이며 5번·팀장 수용/원본 병합·공유5000 적용과 구분한다. 기존 지문 flag가
OFF이면 이 경로도404이며, 이 조회를 쓰기 위해 현재 서버의 지문을 강제 활성화하지 않는다.
새 DDL/설정/seed는 없다.

- 입력: 현재 정상 API Bearer만, query/body·사용자/근무ID 입력 없음.
- 같은 `authenticated_transaction()`에서 현재 사용자·AuthSession/AdminGuard를 재검사하고,
  실제 `AttendanceWorkflow`의 현재 사용자 WorkSession을 잠금 읽는다. GET은 새
  Identity Context/근무/영수증을 만들지 않는다. 서버 UTC를 한 번 고정해 KST 근무일과
  정오 조건에 사용한다. PC 시계/표시 사용자/지난 LoginResult로 근무를 만들지 않는다.
- `data`의 정확한 필드: `server_time`(UTC), `work_session_ref`(정규 UUID),
  `work_date`(KST YYYY-MM-DD), `state`(`open`), `started_at`(UTC),
  `noon_at`(미확인 null 또는 실제 기록 UTC), `noon_due`(bool).
- `noon_due`는 **이미 제공된 서버 조건**인 당일 열린 근무·KST12시 이상·정오 기록
  없음이다. 다른 PC의 확인도 다음 조회에 반영한다. 늦은 첫 출근/장시간 미접속 뒤의
  자동 팝업 일정·안내 정책을 새로 확정하지 않으며, 5번 자동 일정은 별도 연결이다.
- 기존 `load_identity`가 열린 당일 근무 없는 정상 토큰을 허용하지 않으므로,
  미출근/전날 미종료/다른PC 퇴근/만료·회수는 원칙적으로401이다. 비밀번호 재로그인의
  409 사유 안내를 재사용한다. 인증 후 같은 TX 재검사 사이 상태가 달라지면
  `ATTENDANCE_NOT_STARTED`, `ATTENDANCE_DAY_CLOSED`, `ATTENDANCE_DAY_REVIEW_REQUIRED`
  409로 구분한다. 부분 schema503/실제 reader 미제공503을 빈 성공으로 만들지 않는다.
- `IdentityPending`·퇴근 뒤 결과전용 문맥은 이 일반 근무 조회를 사용할 수 없다.
  퇴근 finalize 원 UUID의 보관결과 재생 권리는 기존 전용 경로에만 유지한다.

**C# 제공 접점:** `Infrastructure/WorkdayStateConnection(session).ReadAsync()`는
캡처된 정상 `ApiClient.Session`에서 위 GET을 한 번 실행한다. 반환
`WorkdayState(WorkSessionId,WorkDate,ServerTime,StartedAt,NoonAt,NoonDue)`의 날짜·UUID·
UTC·기록/정오 조건을 검사하고 기존 접속 교체/만료/401 처리를 유지한다. 키·token
노출/설치, 자동 재시도·근태 POST·일정/카메라 감시는 없다.
5번은 이 값을 `FingerprintWorkdayOperation`의 서버 근무UUID/날짜에 전달하고,
실제 challenge가 같은 근무/목적/날짜인지 센서 전에 다시 검사하는 기존 방어를 유지한다.

### 퇴근 결과 유실·일반401: 아직 남은 공통 수명 조립

서버 퇴근 commit은 해당 회원의 정상 AuthSession을 삭제한다. 그 뒤에도 같은
원 문맥/challenge/finalize UUID의 결과 조회/재생만24시간 유지하며 새 업무 권한은 없다.
그러나 현재 ApiClient는 어느 일반 요청의401이든 현재 로그인 세대를 폐기하고,
5번 WorkdayOperation은 그 Session 수명에 묶여 있으므로, auth/me 또는 다른 요청의
401이 먼저 오면 원 결과 복구 객체도 사용할 수 없다. 이번 조회 추가로 해결된 문제가 아니다.

1번의 후속 구현은 **퇴근 전용 원 작업 수명과 일반 업무 요청 정리/중지·기존 in-flight
요청 경합을 함께 소유하는 공통 조립**이다. 단순 timer 중지 또는 전역401 무시는
해결로 인정하지 않는다. 5번 폼은 operation을 빌리고, 소유자는 같은 로그인/근무의
원 operation/UUID를 창 밖에 보관해야 한다. 정상 receipt 전 자동 로그아웃·재로그인·
새 operation으로 덮어쓰기 금지다. 외부 계정/서버 교체·비밀번호/권한 회수는 서버가
현재 검사하고, 새 로그인에 옛 token/결과를 인수하지 않는다. 이 공통 조립·정오 자동
일정/활동monitor·실물/두PC 검증 전에는 새 지문 기능을 OFF로 유지한다.

검사: 서버 `tests/test_identity_workday.py`는 새 소유 MySQL/실제 출근·정오·퇴근,
재접속/타회원 비노출·GET 무변경·schema503·자정 경계를 사용한다.
`Tests/WorkdayState.Tests`는 실제 ApiClient/명시 HTTP 대역이다.
기존5번 정오/퇴근179·실제HTTP/MySQL17검사와 이번 서버조회 검사를 구분한다.

## 2026-10-22 추가 기준 반영 — 근태 저장·재접속·암호화 복구 구현

사용자가 **오늘 첫 출근 후 미퇴근 상태의 재로그인은 지문 없이 아이디·비밀번호로 기존 근무에 재연결**, **퇴근 후 같은 날 재출근은 보류 안내**로 정했습니다. 오늘 첫 출근/정오/퇴근은 기존 지문 기준을 유지합니다. 정상 API/웹 재로그인은 서버의 당일 실제 열린 근무만 허용하고, PC 표시나 로컬 저장으로 판단하지 않습니다. 최초 출근409 `IDENTITY_FINGERPRINT_REQUIRED`, 이미 퇴근409 `ATTENDANCE_DAY_CLOSED`를 안내합니다. 이전날 미종료는 자동 퇴근·날짜 변경 없이409 `ATTENDANCE_DAY_REVIEW_REQUIRED`로 닫습니다.

`features/identity/attendance.py::AttendanceWorkflow(receipt_key, key_id)`가 실제 MySQL 근태 제공부입니다. 위 서버 실행기의 `IDENTITY_WORKFLOW_PROVIDER`에 **명시 주입할 수 있는 구현**이며 자동 설치/실제 운영 활성화는 아닙니다. `receipt_key`는 기존 SECRET_KEY/등록/v2방향키·공개키와 다른 비공개32byte키이고, 실제 loader/키보관·배포는 별도입니다. cryptography50.0.2의 AES-256-GCM·별도12byte nonce와 문맥/challenge AAD로 정상토큰 포함 결과를 암호화합니다. DB/감사에 평문 access_token을 남기지 않습니다. 같은 원래 finalize UUID/현재 문맥만 복구하며 키 변경/복호화 불가를503으로 닫습니다. 키는 최소 조회보관 동안 유지해야 하며 이 구현이 자동 키회전을 제공하지 않습니다.

- 초안은 이제 **identity4+근무/영수증2=6테이블**입니다. 정식 DDL번호/전역schema1/기존007은 바꾸지 않았습니다. 근무(user, KST날짜) UNIQUE, challenge별 workflow receipt와 원 finalize UUID UNIQUE를 둡니다.
- `finalize`가 현재 권한/미소비 verified·원래 challenge 기한 안에서 근무·감사·workflow 영수증을 같은 TX에 저장합니다. 첫 출근은 열린 근무와 정상 AuthSession, 정오는 같은 실제 근무의12시 이후 한 번, 퇴근은 종료 기록과 해당 회원 정상 세션 폐기를 원자 처리합니다. 다른 회원 세션은 유지합니다. 실패는 모두 rollback하며 verified만으로 완료를 표시하지 않습니다.
- 성공 뒤 원문/원키 재생은 보관창 안에서 저장된 암호화 결과를 복구하며 새 근무·토큰을 발급하지 않습니다. 출근의 pending/퇴근의 폐기 문맥은 **해당 확정 challenge 결과 조회/동일 finalize 재생/폐기만** 허용합니다. 다른 challenge/업무 권한은 주지 않고 현재 계정·비밀번호/회수·장치/매핑도 재검사합니다. 조회 창은 원발급·문맥/결과 보관의 더 이른 상한까지 최대24시간입니다.
- 제공부에 finalize가 없으면 이전처럼503입니다. 실제 운영에는 위 구현과 원본 근태 범위의 합의/DDL·키·클라이언트 연결이 모두 필요합니다. 관리자 정정·재출근·심야/휴일 정책은 자동 추가하지 않았습니다.
- `Infrastructure/IdentityPendingConnection.cs`는 실제 IdentityPending 전용 HTTP 전달부입니다. RAM 토큰/같은 로그인 세대·취소·만료·정확 base64url 전달, 승인2초/기타12초와64KiB 응답 상한을 유지합니다. 일반 ApiClient에 임시 토큰을 넣거나 자동 정상로그인/재시도·재스캔하지 않습니다. 현재 LoginForm/MainForm/5번 relay를 자동 연결한 것은 아니며 정상 토큰 최종 인수/화면 연결도 후속입니다.

**검증·미배포:** 실제 raw초안6테이블의 소유 MySQL/Flask 및 loopback 검사는 기존 `test_identity_service.py`, C#은 `Tests/IdentityPending.Http.Tests`를 재현합니다. 최종 실행 수치는 PR62에 구분합니다. 실제5000·37테이블·.env/키/센서와 실물/두PC는 변경·검증하지 않았습니다. 정책 반영/구현·원본 병합·운영 적용을 구분합니다. 아래 1차 게시의 “finalize 미구현/4테이블”은 b71e2e8 단계 이력입니다.

## 2026-10-22 1차 서버 게시 이력 — b71e2e8

**구현됨/검토 대기:** `RemoteWork.Server/remotework/features/identity/`의 제한 로그인·challenge 발급/요청UUID 조회·현재 상태 조회·capture/result 승인·원문 complete·취소·문맥 회수 HTTP와 별도 MySQL 4테이블입니다. 기존 경로/DTO/원문 코덱을 재사용하며 Pi/C# 코드를 대신 수정하지 않습니다. 아래 10/21의 “전체 endpoint 미구현/contract-only”는 당시 기록입니다.

**미구현/사용 금지 경계:** 실제 근태 저장·영수증 소비/정상 토큰 전환·암호화 토큰 재전달은 아직 연결하지 않았습니다. `finalize`는 검증된 영수증에도503 `IDENTITY_WORKFLOW_NOT_READY`입니다. `verified/workflow_state=pending`은 출근/퇴근 완료가 아닙니다. 현재5000·사용DB·운영키/장비에는 이번 코드를 적용하지 않았고 새 API 사용 가능 서버라고 안내하지 않습니다.

### 설치 조건과 현재 인증

기본 flag는 계속0입니다. 1만 설정하면 DB 초기화 전 거절합니다. 신뢰 서버 실행기가 `create_app`에 **제안된 Python 공급 접점** `IDENTITY_DEVICE_PROFILES`와 `IDENTITY_WORKFLOW_PROVIDER`를 명시 주입해야 합니다. 이는 새 환경변수나 HTTP 입력이 아니며 다른 담당의 구현 완료/운영 승인으로 간주하지 않습니다. 기존 등록 `ENROLLMENT_DEVICE_KEYS`도 명시 전달해야 네 identity 키와의 중복을 검사할 수 있습니다. 공개 합성키/등록키/중복키와 신뢰 실행기의 `IDENTITY_EXCLUDED_KEYS`는 거절하고 프로필을 불변 복사합니다. 파일 loader·실제 비공개 파일/ACL 검증·키 설치는 별도이며 `.env`에 키를 넣지 않습니다.

provider의 제안 접점은 `current(db, actor, purpose) -> {work_date, work_session_ref}`입니다. **현재 인증 TX의 같은 SQLAlchemy db에서 실제 근태 원본을 현재 읽기**해야 하고 네트워크/센서 I/O·독립 commit을 하지 않습니다. 실제 원본을 모르면 `AppError(503, 'IDENTITY_WORKFLOW_NOT_READY', ...)`로 닫습니다. 날짜는 서버UTC+9의 KST, clock_in의 열린 근무세션은null/나머지는 실제 UUID여야 합니다. 시험의 고정 provider는 운영 연결부가 아닙니다.

활성화 시 기존 비밀번호 API/웹은 정상 토큰을 발급하지 않으며 이전 일반 업무 세션도 자동 승계하지 않습니다. 정상 Bearer는 identity API 안에서만 실제 기존 AuthSession을 현재 잠금 검증해 정오/퇴근 요청 문맥에 연결합니다. IdentityPending은 별도 토큰hash/비밀번호hash 세대/300초/불투명 session_ref로 제한합니다. 이전 API·웹/UI의 실제 전환이 미완료이므로 운영 활성화하지 않습니다. 기본0에서는 기존 인증을 보존합니다.

### MySQL·응답 유실·재시작

`migrations/drafts/identity_v2.mysql.sql`은 **정식 번호를 예약하지 않은 검토 초안**입니다. 공통 users/admin_guard와007 원본 위에 identity_contexts/challenges/approvals/operations만 추가하며 init-db 자동 생성·전역schema1 변경·기존 테이블ALTER/자료 초기화는 없습니다. 실제 적용은 검토·비공개 전체 백업/새DB 복원·권한/같은배포 후속입니다. HTTP는 부분 컬럼/길이/nullable/PK/UNIQUE/FK/검사 제약 미준비를503으로 닫습니다. 초안 schema의 존재가 운영 보존 정책 확정은 아닙니다.

- 발급UUID와 request bytes, nonce별 probe/approval bytes, 첫 result bytes/SHA·result_id/최소 receipt가 MySQL에 남습니다. 요청UUID 행은 tombstone으로 남기고 자동 삭제하지 않습니다. 원래 UUID를24시간 뒤 새 요청으로 재실행하지 않습니다.
- 공통 AdminGuard 아래 현재 사용자/접속·장치/활성 매핑·revision/epoch·challenge·승인/결과를 재검사합니다. 같은 장치의 미만료 issued는 다른 PC/다른 문맥의 새 발급도409로 막습니다. 동일 원문 complete는 원래 결과, 다른 원문/result_id는409이며 실패6종도 종단화합니다.
- **이번 보수적 제한:** 현재5번의 “최초 capture 승인 안 단일15초” 구현에 맞춰 capture nonce를 DB에한번 고정합니다. 같은 probe는 같은 원문/원래 기한만 돌려주고 새 capture nonce는409입니다. 아래 원계약의 capture 갱신까지 구현한 것은 아닙니다. 갱신 지원이 필요하면5번과 별도 대조하며, 새 채취/예산 연장으로 우회하지 않습니다. result scope는 별도 발급/현재 검사합니다.
- Pi RAM 소실 뒤 기존 challenge를 새 capture로 실행하지 않습니다. 알고 있는 UUID는 같은 현재 문맥 GET→확정 상태만 표시, issued면 사용자 명시 취소 또는 서버 만료를 기다립니다. ID까지 잃으면 임의 추정 조회/새 UUID 재촬영을 하지 않고 이전 challenge의 최대60초 종료를 기다립니다. 그 뒤 새 사용자 선택으로 새 요청을 시작해도 서버의 issued 중복 차단·capture fence가 마지막 방어입니다. **health200/시간 대기만으로 runtime readiness를True로 만들라는 뜻이 아닙니다.** 장치 작업 drain과 실제 현재 문맥/서버 결과 대조는5번 runtime/1번 접속 adapter 공동 후속입니다.
- 취소가 먼저 확정되면 늦은 matched는 성공으로 바꾸지 않습니다. 서버 프로세스 재생성 뒤에도 같은 bytes/원래 승인 기한·capture fence를 보존합니다. 재로그인 시 이전 pending과 미완료 요청을 회수합니다. 비밀번호·활성·장치/매핑 변경은 조회/complete 직전에 재검사합니다. 비협력 직접DB 수정까지 차단했다고 주장하지 않습니다.

### 직접 검증과 남은 연결

`tests/test_identity_service.py`는 소유 MySQL8.2 RR에서 위 raw 초안4테이블을 만들고 실제 Flask 요청·loopback TCP·합성 장치 서명으로 검사합니다. 6 outcome, 동일키/원문·다른결과, 두 스레드 중복, 취소 우선, 재생성·현재 권한/만료·DB 제약 누락·구로그인 차단을 포함합니다. 운영 키/Pi/센서·실계정·근태 저장·C#/웹 실제 로그인·두PC 검증으로 확대하지 않습니다. 구체 게시 SHA/최종 실행 수치는 기존 PR62 본문과 PR58 통합 회신에 기록합니다.

**남은 담당:** 1번 실제 근태 provider/finalize·정상 토큰 안전 재전달·현재 pending/정상 접속 HTTP adapter와 LoginForm/MainForm/웹 전환,5번 게시 runtime/개인설정 loader·실제 센서 조립, 이후 공동 실제 환경 검증입니다. 5번 미게시 c6f9676은 보고 수신이며 직접 코드 검토하지 않았습니다.

---

작성/전달: 1번 → 5번·팀장, 2026-10-21. 상태: **실행 가능한 바이트 계약·API DTO 인계 / 서버 제품 구현·배포 전**.
근거: [5번 기술 수용·남은 4개 접점](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309485288), [1번 앞선 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309878130).

## 적용 범위와 실제 사용 가능 여부

이번 게시에서 고정하는 것은 아래 API **계약**, 새 wire/합성 벡터와 검증 reference다. 5번은 이 버전으로 Pi 어댑터·정확 bytes 중계 대역을 작성할 수 있다.
**아래 endpoint는 아직 실제 서버에 등록되지 않았다. 현재 5000번 서버에서 호출하면 안 된다.**
`python -m remotework.device_identity_contract`는 실제 구현 여부를 출력하며 DB·HTTP·키를 읽지 않는다.
`REMOTEWORK_ENABLE_DEVICE_IDENTITY_DRAFT=0`이 기본이다. 현재 코드에서 1로 바꾸면 `IDENTITY_IMPLEMENTATION_REQUIRED`로 시작을 거절한다.
이는 정상 비밀번호 토큰을 먼저 발급해 놓고 새 인증이 켜졌다고 오인하는 상황을 막는다. 기존 로그인·웹·등록 경로는 0에서 그대로다.

- 구현됨: [Python codec/신선도 reference](../RemoteWork.Server/remotework/device_identity_contract.py), [공개 벡터](../Tests/Fixtures/device_identity_v2.json), [API DTO JSON](../Tests/Fixtures/device_identity_v2_api.json).
- 미구현: 제한 로그인·challenge/승인/결과의 실제 HTTP/MySQL 저장·단일소비·정상 토큰 전환·근태 TX·웹/C# 실제 연결.
- 기존 등록 성공·DB12항목 확인은 유지한다. 기존 등록 job/claim/mapping·센서 슬롯을 정리하거나 다시 등록하지 않는다.
- v1 `device_protocol_draft`와 enrollment v1 및 등록키·DB·TTL을 변경하지 않는다. 새 SQL 번호도 이번 계약에서 선점하지 않는다.
- 통합 영향: 1번 공통 auth/API/DB와 5번 Pi/중계 접점. 타 담당 업무/공지/채팅/파일에 새 지문 조건을 추가하지 않는다.

## 1. 시간·권한·목적

아래는 **개발 연결 계약값**이다. 운영 배포 승인이나 기존 지문 필수 정책 재승인이 아니다.

| 항목 | 고정값/규칙 |
|---|---|
| 제한 로그인 문맥 | 비밀번호 검증 후 300초. 별도 `pending_token`과 `token_type=IdentityPending`; 일반 Bearer/웹 쿠키로 사용 불가 |
| challenge | min(서버 발급 시각+60초,현재 문맥 만료), 이후 연장 불가. GET/재요청/approval 갱신이 이 기한을 늘리지 않음 |
| approval | 발급부터 최대20초, 반드시 challenge 남은 기한 이내. `capture`/`result` 분리. result도20초 상한 |
| Pi RTT | 각 fresh probe 전송~수신 최대2초. UTC 초 단위 절삭/드리프트 여유1.25초 차감 |
| 센서 I/O | 최대15초의 단일 예산. 승인/챌린지 남은 보수적 시간과15초 중 더 작은 값 사용 |
| 조회 창 | 발급 요청UUID·확정결과 보관 최대24시간. 현재 문맥/권한이 더 일찍 만료되면 조회도 종료. 삭제/만료 후 동일UUID 재실행 금지; UUID tombstone 필요 |
| 감사 | 기존 사용자 정책의1년 인증감사. 원문/nonce/키/토큰/생체정보를 감사에 남기지 않음.24시간 결과 조회와 별개 |
| 저장 | 제품은 MySQL만. Pi 결과는 RAM의 고정 bytes; SQLite/로컬DB 추가 안 함 |

목적은 `clock_in`, `noon_check`, `clock_out` 세 가지다. 로그인 전 pending 문맥은 `clock_in`만, 정상 업무 접속은 `noon_check`/`clock_out`만 허용한다.
`session_ref`는 서버가 만든 불투명 문맥 난수이며 토큰/DB PK/회원정보가 아니다. 같은 회원 재로그인도 새 문맥이다.
`work_date`는 서버 근무일(KST 달력 날짜), `work_session_ref`는 서버의 실제 열린 근무세션 UUID다. clock_in은 null이다.
정오 도래·근무일·기존 정오 처리 여부는 서버 근태 조회에서 판단한다. 클라이언트 시각·요청 body로 다른 날짜/근무세션을 선택하지 않는다.
실제 근무상태 provider가 없으면503 `IDENTITY_WORKFLOW_NOT_READY`이며 임의 근무세션/날짜로 대체하지 않는다.

## 2. HTTP DTO와 인증 범위 — 구현할 서비스의 계약

공통 응답은 기존 `{ok,data,request_id}` / `{ok:false,error:{code,message},request_id}`다. 본문은 JSON object이며 미지/중복 필드·잘못된 Content-Type은 거절한다.
`client_request_id`는 사용자 명시 작업마다 한 번 만든 정규 UUID다. 발급 전에 RAM에 저장한다. 응답 유실에 새 UUID로 우회하지 않는다.
모든 원문은 **표준 base64url 무패딩** `*_b64` 문자열로 포장한다. 디코딩한 bytes를 다시 JSON으로 직렬화하지 않는다.
`device_id` 외 user/slot/purpose별 날짜/임의 URI/서버 주소/공개키를 PC 입력으로 받지 않는다.

| Method/경로 (`/api/v1` 이후) | 입력 | 성공 data / 인증 |
|---|---|---|
| POST `/device-identity/login` | login_id,password,client_request_id | pending_token,token_type,context_ref,expires_at,state=`fingerprint_required`; 비밀번호 rate limit/활성 검증. 정상 access_token 없음 |
| POST `/device-identity/challenges` | device_id,purpose,client_request_id | challenge_id,client_request_id,state,expires_at,request_b64; IdentityPending 또는 현재 정상 Bearer |
| GET `/device-identity/requests/{client_request_id}` | 없음 | 같은 문맥의 발급 결과 또는 state=`not_recorded`; 위 문맥 토큰. UUID만 알아서는 조회 불가 |
| GET `/device-identity/challenges/{challenge_id}` | 없음 | challenge_id,state,expires_at,identity_receipt(또는 null),workflow_state; 현재 같은 문맥·권한 |
| POST `/device-identity/challenges/{challenge_id}/approvals` | probe_b64 | approval_b64; 위 문맥 + 현재 신뢰 장치의 `id_probe` MAC |
| POST `/device-identity/challenges/{challenge_id}/complete` | result_b64 | challenge_id,state,identity_receipt(또는 null),workflow_state; 현재 같은 문맥 + `id_result` MAC |
| POST `/device-identity/challenges/{challenge_id}/cancel` | client_request_id | challenge_id,state; 현재 같은 문맥. 별도 멱등 취소 키 |
| POST `/device-identity/challenges/{challenge_id}/finalize` | client_request_id | 아래 근태/세션 영수증. identity_receipt와 현재 같은 문맥·권한 + 실제 근태 TX |
| POST `/device-identity/context/revoke` | client_request_id | state=`revoked`; pending/정상 문맥 본인. 근태 자동 퇴근 아님 |

로그인 발급응답 유실은 토큰 자체를 잃은 상황이므로 동일 로그인 요청을 비밀번호와 원UUID로 다시 제출한다. 서버는 같은 로그인 요청/비밀번호 검증과 현재 계정 상태 확인 후 **이전 문맥을 폐기하고 새 pending 토큰만 회전**한다. 진행중 challenge는 무효화한다. 정상 세션을 만들지 않는다.
challenge 발급응답 유실은 위 요청UUID GET으로 먼저 조회한다. not_recorded 때만 같은 본문/UUID 재전송한다.24시간 이후410 `IDENTITY_LOOKUP_EXPIRED`이며 같은 UUID로 새 발급하지 않는다.
complete/finalize 응답 유실은 같은 문맥 GET 후 같은 원문/키를 재전송한다. 확정결과를 이미 받았어도 GET은 현재 활성 상태/접속/회수 권한을 다시 확인한다. 과거 영수증이 폐기된 접속을 되살리지 않는다.

오류:400 `IDENTITY_INVALID_INPUT`,401 `IDENTITY_CONTEXT_REQUIRED`,403 `IDENTITY_NOT_ALLOWED`,404 `IDENTITY_NOT_FOUND`(타 문맥도 동일),409 `IDENTITY_REQUEST_CONFLICT`/`IDENTITY_RESULT_CONFLICT`/`IDENTITY_TERMINAL`,410 `IDENTITY_EXPIRED`/`IDENTITY_LOOKUP_EXPIRED`,429 기존 로그인 제한,503 `IDENTITY_WORKFLOW_NOT_READY`/`IDENTITY_STORAGE_NOT_READY`.
미존재/타인 장치·mapping은 후보 정보/회원/slot을 노출하지 않는다.401/403/404의 메시지에는 키/식별 상세를 넣지 않는다.

## 3. 새 wire 2.0 — 네 가지 kind와 네 개의 별도 방향키

envelope의 정확한 필드는 `v,kind,device_id,key_id,payload_b64,mac_b64`이고 `v`는 JSON정수2다.
`kind`: `id_request`(서버→Pi), `id_probe`(Pi→서버), `id_approval`(서버→Pi), `id_result`(Pi→서버).
장치·epoch별 **서로 다른32byte identity 전용 난수키4개**를 별도 비공개 프로필로 설치한다. 각 kind의 키를 다른 kind/등록/v1에 쓰지 않는다.
프로필은 server/Pi에만 존재한다. PC는 키/경로·임의 대상URI·키회전값을 공급하지 않는다. epoch 변경/키 회수 때 기존 challenge는 무효다.
별도 비공개 프로필 DTO는 `device_id,key_id,key_epoch,enabled,minimum_slot,maximum_slot,capacity,keys`다.
`keys`는 네 kind 이름을 key로 하는32byte canonical base64url값이다. key_epoch는 위 문자열형, enabled는 JSON bool이며
`0 <= minimum_slot <= maximum_slot < capacity <= 2147483647`를 서버 등록 프로필과 일치시킨다.
같은 키값 중복·공개 vector key·v1/등록 key 재사용·부족/미지 필드는 설정 오류다. 프로필 파일 loader와 실제 설치 검사는 아직 없다.
공개 vector 키는 합성 검증 전용이며 실행 설정에서 사용하지 않는다. 비공개 설치와 ACL·TLS/서버 신뢰는 실제 서비스 준비 단계다.

```
MAC input = ASCII("RW-ID-v2\0" + kind + "\0" + device_id + "\0" + key_id + "\0" + payload_b64)
MAC = HMAC-SHA256(kind 전용키, MAC input)
ticket_digest / approval_digest = SHA256(해당 envelope의 전체 원문 bytes), 소문자 hex64
```

payload는 최대8KiB/전체16KiB UTF-8 object, 중복/미지필드·NaN·Infinity·UTF8 surrogate·비정규 UUID/base64url을 거절한다.
모든 UUID는 소문자 하이픈 정규형, revision/epoch는 `1..9223372036854775807`의 선행0 없는 **문자열**이다.
slot은0..2147483647 JSON정수(bool/실수/문자열 금지), 추가로 서버·Pi 양쪽에서 현재 승인 프로필의 minimum/maximum/capacity를 확인한다.
UTC는 `YYYY-MM-DDTHH:MM:SSZ`만 허용하며 만료시각과 같은 순간부터 만료다.

모든 payload 공통: `challenge_id,nonce,mapping_revision,key_epoch`.

| kind | 공통 외 정확한 필드 |
|---|---|
| id_request | expected_slot,purpose,session_ref,work_session_ref,work_date,issued_at,expires_at |
| id_probe | ticket_digest,probe_nonce,scope (`capture` 또는 `result`) |
| id_approval | ticket_digest,probe_nonce,scope,decision (`allow`),server_time,valid_until |
| id_result | ticket_digest,result_id,outcome,slot,failure_code,approval_digest |

nonce/probe_nonce/session_ref는 디코딩16..64byte의 불투명 난수다. Pi는 MAC 검증된 request/현재 승인에서만 expected_slot을 읽는다.
결과는 원요청 전체 바이트 digest와 공통 필드를 검사한다. 정상 형태이더라도 다른 목적/접속/일자/기대슬롯/epoch의 결과를 받아들이지 않는다.
matched/no_match는 직전 `scope=result` 승인 전체 원문 digest를 넣는다. 서버는 자신이 발급·저장한 승인과 그 만료/문맥을 대조한다.
나머지 실패는 approval_digest=null이며 인증 성공 근거로 사용할 수 없다. 승인 검증/발급 실패 자체는 서버 통신 실패로 처리하고 새 성공 결과를 만들어내지 않는다.

| outcome | slot | failure_code | 서버 challenge 상태 |
|---|---|---|---|
| matched | expected_slot | null | verified(본인인증 영수증만) |
| no_match | null | null | rejected |
| busy | null | SENSOR_BUSY | rejected |
| timeout | null | CAPTURE_TIMEOUT | rejected |
| cancelled | null | CAPTURE_CANCELLED | rejected |
| sensor_error | null | SENSOR_ERROR | rejected |

코어 different_finger/unregistered → no_match. 이름·타인 slot·점수·생체 원문은 송신하지 않는다.
busy도 같은 challenge를 끝낸다. 모든 실패 후 재시도는 **사용자 명시 선택 + 새 요청UUID/새 challenge**, 자동 재스캔/새 result_id 우회 금지다.

## 4. 온라인 승인·15초 입력과 잠금

1. 신뢰 설정에서 서버/장치/키를 선택하고 request MAC·profile·epoch를 검사한다. UART/OLED 공유 잠금을 잡은 **뒤** Pi RAM에서 새 probe를 만든다.
2. 같은 문맥으로 서명한 id_probe(capture)를 서버에 중계한다. 서버는 current 계정/비밀번호 폐기/접속/장치/등록된 활성 mapping·revision·근무목적·issued 상태를 잠금/현재 읽기로 확인한다. 읽기 응답만으로 승인하지 않는다.
3. 서버는 고정 challenge를 연장하지 않고 min(now+20초,challenge expiry)로 승인 원문을 저장/응답한다. 이전 probe 반복은 저장한 **같은 bytes**만 돌려주며 now 기준 새20초를 발급하지 않는다.
4. Pi는 RAM probe가 일치하고 RTT≤2초인 응답을1회만 받는다. 실패 응답/변조도 probe를 소모한다. trusted UTC 상한은 server_time+RTT+1.25초+수신후 단조시간이다.
5. 이전 손 제거부터 검색까지 합쳐 min(15초,승인/챌린지 보수적 남은시간)의 단일 예산을 쓴다. 각 blocking I/O 전후에 취소/수명을 검사한다. 해당 I/O 뒤 기한이 지났으면 성공을 폐기한다. await/잠금 대기 전 승인 재사용 금지다.
6. 입력 대기를 이어갈 경우 승인만료 **전에** 같은 challenge/ticket의 새 RAM probe를 보내 갱신한다. 갱신 실패/만료 뒤는 중단한다. 단일15초 I/O 예산을 갱신으로 다시 시작하지 않는다. 다른 캡처를 시작하는 갱신도 금지다.
7. matched/no_match를 얻은 뒤 새 `scope=result` probe로 현재 승인·권한을 다시 확인한다. 결과에는 이 승인 digest를 넣고 result_id와 원문 bytes를 한 번 고정한다.
8. complete 시 서버는 승인 발급 ledger/현재 기한·권한을 다시 검사한다. capture 승인을 result 승인으로 대체할 수 없다. 서버 성공 응답 전 OLED/PC는 ‘지문 일치·서버 확인 필요’까지만 표시한다.

승인 갱신은 issued 상태에만 허용한다. verified/rejected/cancelled/expired/revoked 이후 새 승인 불가.
타 작업이 busy면 그 작업을 멈추지 않는다. 조도 수신을 유지하고 등록/저장/삭제는 호출하지 않는다.
Pi 재시작·suspend를 커버하지 않는 시계/clock rollback·로그인 교체·취소·장치 변경은 RAM probe/승인을 폐기한다.
RAM을 잃은 재시작은 이전 결과를 재구성하지 않고 서버 GET으로 확정 상태만 조회한다. 미확정이면 명시 취소/만료 후 새 요청이다.

## 5. 소비·만료·결과 복구와 MySQL 구현 조건

상태: `issued → verified | rejected | cancelled | expired | revoked`. 모든 종단 상태는 되돌리지 않는다.
verified는 본인인증만이며 workflow_state=`pending`; 근태/세션 확정 뒤에만 workflow_state=`completed`다.
DB의 고정 result_id+SHA256(전체 result bytes)로 첫 valid 결과를 소비한다. **같은 bytes** 재전송은 같은 영수증, 다른 bytes/result_id는409.
종단화 이전까지 현재 상태·TTL을 검사하고, 종단화 후의 정확 재전송은24시간 내 같은 문맥/현재 권한에서만 영수증 조회로 처리한다. 만료 뒤 새로운 성공은 불가하다.
cancel/권한회수와 complete는 같은 잠금 순서를 사용한다. 취소가 먼저 commit되면 늦은 matched는409/403이며 정상 세션이 만들어지지 않는다.

실제 구현은 기존 공통 guard → 현재 actor/문맥 → 장치/매핑 → challenge → approval/receipt 순서의 잠금으로 통일한다.
계정·장치·매핑 갱신의 비협력 쓰기까지 막을 수 있다고 주장하지 않는다. 매핑 변경/회수 경로도 같은 잠금에 참여해야 한다.
DB에는 발급UUID tombstone/만료, 문맥 토큰hash/비밀번호 세대, request 원문/hash, 승인 원문/hash/nonce·기한, 결과 원문/hash/result_id/최소 영수증, 감사의 별도 보존을 영속한다.
장치별 활성 mapping의 사용자·slot·revision·epoch는 기존 등록 테이블과 FK/현재 검증을 대조한다. init-db 자동 추가/전역 schema version 변경 없이 별도 정식 migration 검토가 필요하다.
새 DDL/실제 MySQL 단일소비·동시성·재시작 검사는 **이번에 구현·검증되지 않았다**. 바이트 unit test를 이 근거로 쓰지 않는다.

## 6. 제한 로그인·근태와 정상 접속 전환

pending_token은 `IdentityPending` 스킴의 위 본인인증 API만 허용한다. `/auth/me`, 업무·사용자·공지·채팅·첨부/웹에는 로그인으로 인정하지 않는다.
재로그인/비밀번호 변경·초기화/비활성·문맥 폐기 때 이전 pending과 미완료 challenge를 무효화한다. 정상 토큰을 먼저 발급하고 창만 가리는 방법을 사용하지 않는다.
본인인증 기능을 실제 활성화할 때는 API/웹 양쪽 기존 비밀번호 로그인에서 정상 토큰을 발급할 수 없게 공통 흐름을 함께 연결하고 기존 세션 전환도 검토한다. 현재 guard 때문에 이 불완전한 활성화 자체가 불가능하다.

`complete matched` 응답은 identity_receipt=`{receipt_id,purpose,verified_at}`, workflow_state=`pending`이며 token/work_session 완료를 담지 않는다.
`finalize`는 현재 같은 문맥·미소비 영수증·실제 근태 상태를 다시 확인한다. 같은 MySQL TX에 근태 mutation/최소 workflow receipt/세션 전환을 넣고 해당 영수증을 단일소비한다.

- clock_in: 출근 기록·work_session 생성이 commit된 뒤 정상 access_token(또는 웹 HttpOnly cookie)을 제공. data: `state=completed,workflow_receipt_id,work_session_ref,work_date,access_token,token_type,expires_at`. 유실 시 같은 pending 문맥은 **해당 확정결과 조회/폐기만**24시간 허용하며 새 challenge·업무 권한은 없다. 기존 정상토큰 재전달을 위해 암호화 결과보관/서버 키관리도 구현 전제다.
- noon_check: 동일 현재 정상 접속과 서버 근무일의 정오 기록 commit. data: `state=completed,workflow_receipt_id,work_session_ref,work_date`.
- clock_out: 실제 퇴근 commit과 정상 세션 폐기를 같은 TX에 처리. data: 위4필드. 이후 해당 폐기 문맥은 해당 확정결과 조회만24시간 허용하고 새 승인/업무 호출은 금지. 그 외 철회/비밀번호 회수는 이 조회 권리도 철회한다.

C# `IdentityPendingConnection`의 `ExpiresAt`/`IsCurrent`는 신규 활동의 최초5분 제한을
유지한다. 첫 finalize 전송 직전에 RAM에 challenge/원 finalize UUID를 고정하며,
응답 유실 뒤5분이 지나도 같은 challenge의 GET과 원 UUID finalize 재확인만 허용한다.
최초 시도부터 최대24시간의 보수적 로컬 상한은 재전송으로 갱신하지 않고, 서버의
현재 문맥·권한·더 짧은 보관기한/401·403·410이 우선한다. 새 발급/승인/complete나
다른 UUID·challenge로 바꾸지 않는다. 로그인 세대/외부취소·늦은 응답 검사는 전후에
유지한다. 소유자가 활동 만료만으로 Dispose하면 이 RAM 복구 기록도 사라지므로 실제
화면 조립은 활동 수명과 결과 복구 수명을 구분해야 한다. 자동 UI 조립 완료는 아니다.

근태 provider/키/저장·트랜잭션 접점 미설치 또는 폐기 직후 결과 조회 권한/토큰 안전 재전달이 미구현이면 finalize는503이며 성공으로 대체하지 않는다.
수동 로그아웃/앱 종료는 문맥 폐기·장치 해제이고 자동 퇴근이 아니다. 지문 없는 관리자 예외/미등록 fallback은 만들지 않는다.
1번이 이 서버/DB·토큰 전환을 구현하며 5번에게 전체 매핑·서버 모델 설계를 되돌려 요구하지 않는다. 출퇴근·정오 화면/근태 저장 접점은5번과 연결한다.

## 인계·다음 확인

5번은 이 wire/벡터/실패 처리로 Pi adapter·C# exact bytes relay 대역을 준비하고, 코어15초와 승인20초·남은시간·result scope 차이를 대조해 회신한다.
서버 실제 endpoint/DDL·제한 로그인·근태 provider·비공개 키 설치/공유 환경은1번 후속 구현이며, 이 문서 게시나 코드PR 병합으로 완료 처리하지 않는다.
팀장은 기존 LOG1/5에 이번 계약게시/수신 대기와 제품 미구현을 취합한다. 팀원은 날짜LOG에 게시하지 않는다.
