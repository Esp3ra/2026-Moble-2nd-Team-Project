# 5번 인계: RW-ID-v2 본인인증 계약 2.0

## 2026-10-22 서버 구현 후속 — 기존 아래 계약 이력보다 우선

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

근태 provider/키/저장·트랜잭션 접점 미설치 또는 폐기 직후 결과 조회 권한/토큰 안전 재전달이 미구현이면 finalize는503이며 성공으로 대체하지 않는다.
수동 로그아웃/앱 종료는 문맥 폐기·장치 해제이고 자동 퇴근이 아니다. 지문 없는 관리자 예외/미등록 fallback은 만들지 않는다.
1번이 이 서버/DB·토큰 전환을 구현하며 5번에게 전체 매핑·서버 모델 설계를 되돌려 요구하지 않는다. 출퇴근·정오 화면/근태 저장 접점은5번과 연결한다.

## 인계·다음 확인

5번은 이 wire/벡터/실패 처리로 Pi adapter·C# exact bytes relay 대역을 준비하고, 코어15초와 승인20초·남은시간·result scope 차이를 대조해 회신한다.
서버 실제 endpoint/DDL·제한 로그인·근태 provider·비공개 키 설치/공유 환경은1번 후속 구현이며, 이 문서 게시나 코드PR 병합으로 완료 처리하지 않는다.
팀장은 기존 LOG1/5에 이번 계약게시/수신 대기와 제품 미구현을 취합한다. 팀원은 날짜LOG에 게시하지 않는다.
