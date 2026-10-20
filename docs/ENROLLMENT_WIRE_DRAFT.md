# AS608 등록 접점 회신·enroll-v1 대조안

> 2026-10-20 후속 정정: 아래는 최초 오프라인 제안 당시 기록이다. 이후 사용자 승인으로 **실제 서버 API·DB·온라인 승인**을 구현했다. 최신 실행 계약/200응답/base64url 전송/복구 오류는 [DEVICE_ENROLLMENT_API.md](DEVICE_ENROLLMENT_API.md)가 우선한다. codec의 검사 범위는 그대로이며 현재 권한/소비는 새 서버 서비스가 검사한다. Pi/PC 버튼·실물 연결은 별도다.

1번 → 5번·팀장 / 2026-10-20 / C10, D5-01.a·b, D5-02.a·b, D5-05.h.
[5번 후속 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6281140854), [기존 화면/API 제안](USER_FINGERPRINT_ENROLLMENT_DRAFT.md).

**상태: 1번 기술 수용/수정안·실행 가능한 바이트 대조 도구 제공 / 5번 최종 수신·공동 계약/운영 정책 대기.**
서버 등록 API/DB·버튼·Pi 어댑터·실물 채취를 구현한 문서가 아니다. 아래 `draft`를 제품 인증 함수로 직접 연결하지 않는다.
현재 User/Bearer·업무 API/HTTP12초·schema1·002~006과 기존 identity request/result를 변경하지 않는다.

## 네 요청에 대한 답변

| 요청 | 1번 답변/현재 제공물 | 다음 확인/결정 시점 |
|---|---|---|
| 단일 예약 슬롯 | **수용.** 서버 예약 claim의 단일 슬롯만 서명 티켓에 넣고 Pi가 시작 전/쓰기 직전 빈 슬롯을 확인. 자동 번호 배정/다른 슬롯 대체/점유 슬롯 덮어쓰기 금지 | 5번 실제 코어/단일 UART 소유·센서 lock 전달 검토. 실물 활성화 전 |
| 별도 MAC/원문·벡터 | 아래 enroll-v1 필드·바이트·방향 키·epoch·digest를 대조안으로 제공. Python 엄격 검사/바인딩과 독립 C# 벡터 포함 | 5번 어댑터가 이 원문을 수용/차이 회신한 뒤 계약 고정 |
| 시간/재시작/조회 창 | 서버 UTC가 최종 판정. 개발 대조 후보는 발급180초/성공 조회24시간, 실제 설정/운영 확정 아님. Pi90/120초 I/O 제한과 구분 | 5번 최악 시간 계측·시각/온라인 현재성 어댑터 회신 후 공동 고정 |
| API/DDL/버튼/복구 경계 | 기존1번 제안 경로/화면 접점 유지. 아래 claim·불변 결과·비활성 매핑·재시작 캐시 조건을 추가 | 1번 실제 API/DDL/버튼과5번 어댑터/중계는 계약/정책 대조 뒤 구현. 이번 등록 제품 기능은 없음 |

5번 보고인 AS608·용량300·등록11은 실물 읽기 근거로 수신했다. **0번/299번 새 등록·삭제는 미검증**이며 파서의 INT 상한이나 대역 설정을 운영 슬롯 범위로 승인하지 않는다.
로컬 코어 `4c2961e`/`4d7227e`와33검사 통과는5번 작성자 보고다. 이번 확인 당시 원격 push/코드PR이 없어 실제 소스/테스트를 읽거나 재실행하지 못했다.

## 단일 슬롯·claim·충돌

- 장치 현재 프로필과 실제 inventory를 읽고 서버가 슬롯 하나를 예약한다. DB의 `(device_id, slot)` 유일 claim은 **등록 중/활성/물리 저장 불명 격리**를 함께 보호해야 한다. 이력은 별도로 보존한다.
- `claim_revision`은 정규 양의 BIGINT문자열인 예약 세대다. `operation_revision`은 이 등록 요청의 불변 바인딩 버전이다. 같은 작업 재조회/재전송/센서 진행 때문에 둘을 자동 증가시키지 않는다. 새 예약/재발급은 새 작업ID와 새nonce, 새claim세대로 별도 승인한다.
- 예약/실물 점유가 다르면 **쓰기 전에 slot_conflict**로 거절한다. 같은 작업으로 다른 슬롯을 고르거나 새 result_id로 숨기지 않는다. 실제 점유/소유를 재확인하고 orphan이면 격리한다.
- 센서 저장 이후 또는 저장 여부 불명 시 서버의 TTL·취소만으로 claim을 해제하지 않는다. 이미 사용 중인 슬롯은 신규 계정에 재할당하지 않는다.
- 같은 프로세스의 인증/등록은 기존 `FingerprintService.lock` 한 개를 공유한다. 프로세스가 둘이면 Python lock으로 UART 경합을 막지 못하므로 실행 서비스의 단일 소유를 확인한다.
- 센서가 슬롯 목록에 번호를 반환한 것만으로 직원의 지문 등록 증명이 완성되지는 않는다. 검증된 티켓·같은 지문 두 채취/비교·해당 저장 명령/결과·복구 증거·서버 현재 관계를 함께 대조한다.

## 메시지 형식·원문·키

코드: [enrollment_protocol_draft.py](../RemoteWork.Server/remotework/enrollment_protocol_draft.py).
공개 합성 자료: [enrollment_protocol_draft.json](../Tests/Fixtures/enrollment_protocol_draft.json).
이 키/식별자/과거 시각은 **누구나 아는 가상 시험값**이다. 실제 장치/키 배포에 사용하지 않는다.

outer envelope는 기존과 같은 필드명 `v,kind,device_id,key_id,payload_b64,mac_b64`만 허용하되 **다른 domain/kind**다.
`v` JSON정수1, `kind`는 `enroll_request` 또는 `enroll_result`, 장치ID는 소문자 UUID, key_id는 `[A-Za-z0-9_-]{1,64}`.
원문 UTF-8 payload 최대8KiB, envelope 최대16KiB. 공통 HTTP32KiB 제한을 늘리지 않는다.
base64url은 패딩/공백 없는 정규형, MAC은 SHA256 전체32바이트다. JSON 중복/미지 키·NaN/Infinity·잘못된UTF-8·surrogate·bool을정수로사용·비정규ID/버전은 거절한다.

```text
MAC input = ASCII("RW-ENROLL-v1") || 0x00 || ASCII(kind) || 0x00
          || ASCII(device_id) || 0x00 || ASCII(key_id) || 0x00
          || ASCII(payload_b64)
mac_b64 = canonical_base64url(HMAC-SHA256(direction_key, MAC input))
ticket_digest = lower_hex(SHA256(request MAC input))
```

**5번의 `ticket_digest: 원문 티켓 SHA256` 표현은 위 MAC input의 SHA256으로 맞춰 달라.** envelope JSON 전체 바이트나 재직렬화한 payload의 SHA256이 아니다.
PC는 payload/envelope를 다시 직렬화하지 않고 바이트를 중계한다. fixture의 `payload_json`/`mac_input_hex`는 설명용 부가 필드이며 실제 envelope에 넣지 않는다.

장치/epoch별 서버→Pi 요청 키와 Pi→서버 결과 키를 별도 선택하는 방향을 제안한다. 실제 무작위 root/방향 키 배포·파생/교체 방식은 공동 대조 대상이며 이 도구가 키를 생성/설치하지 않는다.
키는 서버/Pi 비공개 설정에만 두고 key_id/epoch를 이유로 임의 키를 신뢰하지 않는다. PC에는 키를 주지 않고 Pi에는 관리자 Bearer를 주지 않는다. MAC이 TLS/인증서 검증이나 현재 권한 검사를 대체하지 않는다.

| 구분 | 정확한 payload 필드 |
|---|---|
| 공통5필드 | `enrollment_id` UUID, `operation_revision` BIGINT문자열, `reserved_slot` 비음수INTEGER, `claim_revision` BIGINT문자열, `key_epoch` BIGINT문자열 |
| request 추가6필드 | `target_user_id` 양의signed INTEGER JSON정수, `nonce`16~64바이트base64url, `purpose="fingerprint_enrollment"`, `issued_at`, `expires_at`, `session_ref` |
| result 추가5필드 | `ticket_digest` 소문자64hex, `result_id` UUID, `outcome`, `failure_code`, `elapsed_ms` 비음수signed INTEGER JSON정수 |

시각은 서버 UTC `YYYY-MM-DDTHH:MM:SSZ`, expires_at>issued_at. session_ref는 서버 발급 불투명 `[A-Za-z0-9_-]{1,128}` 참조이며 토큰해시/실제Bearer를 노출하지 않는다.
예약슬롯은 JSON형식 상0..2147483647이지만 실제 실행에서는 **승인된 해당 장치의 min/max/capacity/현재claim을 추가 검사**한다. 이 파서가0/299 사용을 승인하지 않는다.
관리자 actor는 현재 서버 접속에서 정하며 target_user_id와 별개다.

result에는 raw 직원ID·nonce·session_ref를 반복하지 않고 **인증된 ticket_digest로 원래 대상/nonce/접속을 결합**한다. 공통5필드·digest가 원래 티켓과 같아야 한다.
이 방식은5번이 원하지 않는 raw 회원ID/nonce를 최종 결과 캐시에 추가하지 않도록 하는 수정 제안이다. 이름/지문원본/템플릿/점수도 없다.
5번 코어의 EnrollmentOperation에는 검증된 request에서 필요한 기존 필드만 추출하고 claim/epoch/nonce/시각은 바깥 어댑터가 유지·재검사한다. 실제 SensorReceipt를 최종 wire 결과로 변환하는 작업은5번 담당이다.

`outcome`은 `sensor_stored`, `failed`, `recovery_required`다. **completed/서버매핑완료라는 outcome은 없다.**
sensor_stored의 failure_code는null. 나머지 실패분류는 busy/cancelled/timeout/sensor_full/slot_conflict/duplicate_finger/different_finger/capacity_mismatch/sensor_error/storage_uncertain/ledger_error/authorization_lost를 제안한다.
`storage_uncertain`/`ledger_error`는 보수적으로 recovery_required로 분류한다. 어댑터는 쓰기 이후의 어떤 불확실성도 단순 failed로 축소하지 않는다. elapsed_ms는 고정 증거로 보존하되 서버 만료 판정/효력 갱신에 쓰지 않는다.

`verify_payload`는 MAC/정확한DTO/기대epoch만, `verify_completion`은 추가로 request/result의 공통5필드·digest 바인딩만 검사한다.
**과거 티켓과 반복 호출도 바이트가 맞으면 통과한다.** 실제 서비스의 현재 actor/접속·장치/키·대상·claim·시각·nonce 단일소비/transaction과 혼동하지 않는다.

## 서버 시각·만료·재시작·조회

개발 계측 대조 후보는 발급180초다(로컬최대I/O120초+전달/회수 여유 후보). **실물 최악시간 미계측이므로 승인/실행 설정값이 아니다.** 최초90초/최대120초는 센서 I/O 상한이며 HTTP12초나 서버TTL이 아니다.
공통 HTTP를 길게 붙잡지 않고 전용 비동기 시작→짧은 진행/결과 조회로 준비한다. DB transaction 안에서 센서를 기다리지 않는다.

서버는 최초 완료/시작 승인 때 자기 UTC와 현재 접속·권한·claim을 검사한다. Pi는 MAC만 맞는 과거 티켓으로 시작하면 안 된다.
1번 제안은 **재시작 뒤 cached 승인 금지 + 검증 가능한 현재 시각/온라인 현재성 확인이 안 되면 authorize=false**다. 5번은 신뢰 UTC/허용오차를 보장할지, Pi의 새 난수에 묶인 온라인 승인 왕복으로 대체할지 어댑터안을 회신해야 한다. 새 온라인 승인 메시지/경로는 이번2종codec에 구현하지 않았다.
진행/쓰기 전에도 authorize·취소·현재 작업/claim을 재검사한다. 진행 중 UART는 즉시 멈추지 않을 수 있으므로 반환 후에도 검사하고, 쓰기 뒤 무효화는 recovery_required로 남긴다. 권한 회수와 물리 저장이 완전히 원자적이라고 보장하지 않는다.

성공 결과 조회24시간은 **등록 전용 개발 후보**로만 제안하며 업무receipt24시간/인증기록1년에서 자동 상속하지 않는다. 정책/계측 회신 뒤 기간을 정한다.
완료 동일 결과 조회는 원래 최소 결과를 반환할 뿐 새 매핑/revision/감사/TTL을 만들지 않는다. 현재 접근 권한은 다시 검사한다.
미완료 만료는 신규 완료410/새스캔자동금지다. 이미물리저장됐을가능성이있으면 별도복구 대상으로 남기고 취소/만료만으로 슬롯을 풀지 않는다.
불명/orphan 증거·claim은24시간이 지났다는 이유로 자동삭제/재사용하지 않는다. 최종 보관/삭제/복구 책임자는 팀장·1·5번의 정책 결정사항이다.

## 동일 결과·부분 성공·코드 경계

5번의durable store_pending→고정result_id/receipt 보존 방향을 수용한다. **Pi전용SQLiteledger는 장치복구용이며 서버주DB는MySQL 유지**다.
실제전원/SD장애의증거내구성은 별도시험이고, interrupted/capturing/store_pending에서 보수적으로 새등록을 막는 정책의 해제/이관은 운영협의가 필요하다.

1. 센서 증거가 확정되면 최종 signed envelope를 **첫 전송 전에** 비공개 저장소에 고정하고 같은 바이트로 재전송한다. JSON필드순서·시간·elapsed/result_id를 재시도마다 바꾸지 않는다. 새 키로 이전 결과를 재서명하지 않는다.
2. 위 result는 raw 직원ID/nonce가 없어 기존ledger의 최소정보 원칙과 충돌을 줄인다. nonce를 포함한 요청원문·키/토큰/생체자료는 일반ledger/감사/Git에 저장하지 않는다. private 결과캐시의권한/원자쓰기·저장실패처리/보존은5번 구현·공동검토 대상이다.
3. 센서 저장/서버 실패면 계정 유지·매핑 비활성/복구 필요. 같은 결과를 조회/재전송하고 새 슬롯/새채취를 하지 않는다. 서버commit/응답유실은 같은 작업 조회로 기존 성공을 확인한다.
4. 인증 오류·다른actor/작업·변조결과는 정상 작업을 소비시키지 않는다. 같은 결과 단일효과/다른결과409·만료/취소·현재권한/claim 검사를 실제DB transaction에서 구현해야 한다.
5. 삭제/재등록은 서버무효화와물리삭제확인을 분리한다. 오프라인이면 삭제대기를 남기고 자동rollback삭제/일괄삭제/계정활성변경을 하지 않는다.

| 실제 담당 | 이번 제공 / 다음 구현 |
|---|---|
| 1번 | PR44의CreatedUserEnrollmentTarget/관리자Session, 별도codec/벡터/회신 제공. 실제발급/완료/조회API·claim/매핑/감사모델DDL·버튼은후속구현 |
| 5번 | 원격미게시코어는수신보고만확인. 실제코드게시후대조,검증된티켓→EnrollmentOperation어댑터·authorize/방향서명·결과바이트캐시·C#릴레이/진행·실물시간/복구시험 |
| 팀장·1·5번 | 허용장치/등록승인·미등록/부분실패계정허용·복구/이관/삭제·보관정책. 실제API/DDL/등록버튼활성화전결정 |

현재 실제 API/테이블/flag/전용초기화명령은 여전히 없다. SQL번호도미예약, 기존002~006/공통schema1불변이다.
UI 전달은 기존target+현재관리자Session+허용device+작업generation+취소토큰. 최종 UI 완료는 서버매핑commit결과에서만,선택/로그인/장치/창변경뒤지연결과는반영하지않는다.

## 대조 명령·실행 범위

```powershell
# RemoteWork.Server 폴더
.\.venv\Scripts\python.exe -m pytest -q tests/test_enrollment_protocol_draft.py tests/test_device_protocol_draft.py

# 프로젝트 루트
dotnet run --project Tests/EnrollmentProtocol.Tests/EnrollmentProtocol.Tests.csproj -c Release
```

엄격JSON/ID/타입·방향/키/domain/epoch·원문/digest·다른직원/nonce/슬롯/claim/작업결과바인딩과공개벡터를검사한다.
독립C#은테스트전용검산이며제품PC에서키를보유/서명하게만드는코드가아니다.
제품DB/권한·시각/소비/등록API·센서/실물삭제·Pi배포/전원장애·두PC·공유환경은 이번 미구현/미검증이다.

2026-10-20 최종 실행: Python130통과(새등록/기존identity 두파일), 독립C#바이트16, 기존사용자UI14시나리오/46검증·ApiClient25/125·IntegrationSupport31통과, Release0경고0오류. 반복/포함수치를합산하지않는다.
