# 지문 등록 서버 API·DB 개발 구현

1번 작성 → 5번·팀장 / 2026-10-20.
[5번 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6281140854) · [코드 PR44](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/44).

사용자가 규격 회신만 진행하던 범위를 **실제 API·DB 구현**으로 변경 승인했다.
서버 API·전용 DB·짧은 온라인 승인과 수신 검증 참고 코드를 구현했다. Pi 어댑터·PC 비동기 중계/버튼·실물 채취·공유 DB 적용은 별도다.
5번의55검사/Pi 실행은 작성자 보고이며 미게시 소스는 직접 검증하지 못했다.

## 권한·담당 경계

- 기존 Bearer의 **현재 활성 관리자 + 해당 장치 `ResourceGrant(enrollment_device, 장치UUID)`**만 허용한다. 관리자 역할만으로 불가하다.
- 발급 관리자와 당시 로그인 접속에 결합한다. 재로그인/다른 관리자는 자동 인수하지 않는다. 대상 회원도 활성 상태여야 한다.
- 공통 인증 transaction에서 현재 권한·접속·장치·대상·claim을 다시 검사한다. DB transaction 안에서 센서를 기다리지 않는다.
- 운영자가 실측 프로필과 **비어 있음을 확인한 후보 슬롯만** 명시한다. Pi도 쓰기 직전에 실제 빈 슬롯을 확인해야 한다. 용량300만으로0/299 등 전체를 허용하지 않는다. 기존11개 템플릿은 건드리지 않는다.
- 장치/회원에 처리 이력이 있으면 다른 요청으로 자동 재등록하지 않는다. claim 해제/삭제/재등록/이관/키교체/다른 접속 복구는 별도 협의·구현 대상이다.
- `sensor_stored`는 센서 저장 보고다. 서버 `completed`를 받은 뒤에만 화면에서 등록 완료로 표시한다. 기존 로그인·활성 상태는 변경하지 않는다.

## 설치·초기화: 별도 검토된 개발 DB

`REMOTEWORK_ENABLE_DEVICE_ENROLLMENT_DRAFT=0` 기본off, 다른 기능 플래그와 독립이다. MySQL 전용이며 SQLite 활성화는 거절한다.
등록 전용 metadata를 사용하므로 일반 `init-db`는 이 테이블을 생성하지 않는다. 전역 schema version1을 유지한다.

1. 기존 SETUP대로 별도 MySQL 개발 DB·공통 테이블을 준비한다. 사용 중인 DB/서버를 초기화하지 않는다.
2. 서로 다른32바이트 무작위 방향 키를 서버/Pi에만 안전하게 배포한다. PC/저장소에 키를 주지 않는다. 공개 fixture 키는 운영에 쓰지 않는다.
3. 저장소 밖의 서버 전용 비공개 JSON 경로를 `REMOTEWORK_ENROLLMENT_KEYS_FILE`에 설정한다.
   JSON 최상위는 장치UUID→객체다. 객체 필드는 `key_id`(ASCII 영숫자/밑줄/하이픈1~64), `key_epoch`(양의 BIGINT JSON정수), `request_key_b64`, `result_key_b64` 네 개다. 키는 패딩 없는 base64url, 각각32바이트이며 서로 달라야 한다.
4. 해당 개발 프로세스의 독립 플래그를1로 설정하고 아래 명령을 수행한다.

```text
cd RemoteWork.Server
python -m flask --app remotework init-enrollment-draft
python -m flask --app remotework add-enrollment-device --help
python -m flask --app remotework grant-enrollment-device --help
```

`add-enrollment-device` 필수 옵션: `--device-id`, `--display-name`, `--minimum-slot`, `--maximum-slot`, `--capacity`, `--candidate-slots`(쉼표 구분 정수).
이 명령이 센서를 읽어 빈 슬롯을 확인하는 것은 아니다. 운영자 사전 확인과 Pi 쓰기 직전 확인이 모두 필요하며 기존 프로필을 덮어쓰지 않는다.
`grant-enrollment-device --device-id <장치UUID> --login-id <활성관리자ID>`는 해당 장치 권한만 부여한다. HTTP 권한 상승 경로는 없다.
초기화와 [raw007 SQL](../RemoteWork.Server/migrations/007_device_enrollment.mysql.sql)을 중복 적용하지 않는다.

개발 기본값 발급180초·결과조회86400초. 앱 설정 `ENROLLMENT_TICKET_TTL_SECONDS`(5~3600), `ENROLLMENT_RESULT_TTL_SECONDS`(5~604800)로 별도 개발 앱에서 조정한다.
실측 최악 시간·운영 보존 합의 전의 **개발값**이며 최종 운영 합의/업무 receipt 정책 상속이 아니다.

## HTTP 계약

접두사 `/api/v1/device-enrollments`. 기존 `ok,data,request_id`/`ok:false,error,request_id` 응답을 사용한다.
신규/중복 발급 모두 정상200이다. 정확한 필드만 허용하고 중복 JSON키도 거절한다.
UUID는 소문자 정규형, ID는 양의 signed INTEGER JSON정수(bool 불가), revision/epoch는 정규 양의 signed BIGINT 문자열이다.
HTTP의 envelope는 **패딩 없는 base64url 문자열**이다. PC는 decode한 원문 바이트를 재직렬화하지 않고 중계한다.

| 경로 | 정확한 입력 | 동작 |
|---|---|---|
| GET `/eligible-devices?target_user_id=...` | 이 query 하나, 정규 양의 정수 표기 | 명시적 권한·키·대상이 유효한 장치 ID/표시명/key_epoch |
| POST 접두사 자체 | `client_request_id`, `target_user_id`, `device_id` | 슬롯 예약/서명 티켓. 같은 발급자·접속·요청ID/본문은 같은 작업, 다른 본문409 |
| GET `/by-request/{client_request_id}` | 경로 UUID | 발급 응답 유실 복구. 새 슬롯/작업 생성 없음 |
| GET `/{enrollment_id}` | 경로 UUID | 현재 작업·최소 결과 조회. 센서 시작 없음 |
| POST `/{enrollment_id}/approval` | 아래7필드 | 현재 권한·claim·기한 확인 후 별도 서명 승인 |
| POST `/{enrollment_id}/complete` | `operation_revision`, `result_envelope` | 방향별 MAC/작업·digest 확인 후 매핑·결과·감사 원자 저장 |
| POST `/{enrollment_id}/cancel` | `operation_revision` | 새 완료 차단/claim 격리. 센서 중단·물리 삭제 완료는 아님 |

작업 응답: `enrollment_id,operation_revision,reserved_slot,claim_revision,key_epoch,client_request_id,device_id,state,expires_at,recovery_required,request_envelope,result`.
진행 중일 때만 request_envelope가 있고 그 외null이다. result는 null 또는 `result_id,state,failure_code`만 포함한다.
최종 결과 캐시에 회원ID/nonce/접속참조를 반복하지 않고 원래 서명 티켓 digest로 결합한다.

상태: issued/completed/failed/recovery_required/cancelled. 미완료 기한 초과는 조회에서 expired로 표시한다.
첫 완료는 기한 내 issued에 한한다. 취소/만료 뒤 저장 보고는409 `ENROLLMENT_RECOVERY_REQUIRED`, 자동 매핑하지 않는다.
서버가 확정한 동일 **원문 결과 바이트** 재전송은 원래 결과를 반환한다. 원래 티켓 만료 뒤에도 최종 저장부터 결과 조회 창 안이면 가능하다.
다른 결과 바이트409, 조회 창 만료410. 조회로 TTL/감사/매핑을 새로 만들지 않는다. claim은 자동 삭제/재사용하지 않는다.
DB에는 원문 결과/nonce/지문 이미지/템플릿을 저장하지 않고 결과 해시와 최소 결과를 보존한다.

주요 오류는 400 `INVALID_INPUT`/`INVALID_ENROLLMENT_MESSAGE`, 401 `AUTH_REQUIRED`, 403 `FORBIDDEN`/`ENROLLMENT_DEVICE_FORBIDDEN`/`ENROLLMENT_SESSION_MISMATCH`, 404 `ENROLLMENT_NOT_FOUND`/`ENROLLMENT_DEVICE_NOT_FOUND`다.
409는 `ENROLLMENT_` 접두사의 REQUEST_CONFLICT, ALREADY_LINKED, TARGET_REQUIRES_REVIEW, NO_FREE_CLAIM, UNAVAILABLE, CLAIM_CHANGED, REVISION_CONFLICT, BINDING_MISMATCH, RESULT_CONFLICT, WRITE_NOT_ALLOWED, ALREADY_TERMINAL, RECOVERY_REQUIRED다.
410 `ENROLLMENT_RESULT_EXPIRED`/`ENROLLMENT_APPROVAL_EXPIRED`, 503 `ENROLLMENT_KEYS_UNAVAILABLE`/`DATABASE_UNAVAILABLE`. 플래그off는404, 기능on/테이블·키 미준비는503이다.

## Pi 온라인 승인·시각 참고 구현

별도 domain `RW-ENROLL-APPROVAL-v1\0`, `kind=enroll_approval`, 요청 방향 키를 사용한다.
외부 envelope 기본 필드/MAC 조합은 [기존 바이트 계약](ENROLLMENT_WIRE_DRAFT.md)과 같다. domain/kind가 달라 request/result로 재사용할 수 없다.

입력7필드: `operation_revision,reserved_slot,claim_revision,key_epoch,ticket_digest,probe_nonce,scope`.
Pi가 전송 직전에 새 난수16~64바이트 probe_nonce를 만들고 RAM에 왕복 바인딩/발신 monotonic 시각을 둔다. PC는 자신의 Bearer로 중계하고 Pi에 Bearer를 넘기지 않는다.
서명 payload: 공통5필드 + `ticket_digest,probe_nonce,scope,decision="allow",server_time,valid_until`.
sensor_write는 issued/기한 내에서만, result_read는 취소/만료 뒤에도 현재 권한/조회 창이 유효하면 가능하다. 읽기 승인은 새 채취/쓰기를 승인하지 않는다.

[ApprovalProbe/FreshApproval](../RemoteWork.Server/remotework/enrollment_approval_draft.py)은 실행 가능한 수신 참고 구현이다.
MAC·장치/키·공통5·digest·nonce·scope를 모두 확인한다. 왕복2초 초과/역행·비정상 시계/일회용 probe 재사용은 거절한다.
서명 유효기간 최대5초. 보수적인 현재 UTC 상한은 `server_time + 1.25초(초 단위 절삭·드리프트 여유) + 측정RTT + 수신후경과`다.
명목5초를 전부 쓰지 못하며 만료 전 새 probe로 승인받아야 한다. Pi에서는 suspend 포함 monotonic(CLOCK_BOOTTIME 등)을 쓰거나 절전 복귀 시 무효화한다.
재시작/로그인·대상·장치·작업 변경/취소 시 probe와 승인을 폐기한다. 과거 MAC/오프라인 True 공급자를 허용하지 않는다.

5번 연결: 최초 새 승인→신뢰 UTC 공급자 확보→어댑터 시작→핵심 단계마다 새 승인/쓰기 허용·시각 재확인→센서 저장→서명 결과 원문 영속화→PC complete 재전송.
느린 승인 이후에도 trusted_utc를 다시 읽는다. 참고 클래스는 HTTP/센서/UART/취소를 구현하지 않으며 실제 callback 연결/기존55검사 확장은5번 담당이다.
권한 회수와 물리 저장은 완전히 원자적이지 않다. 짧은 승인 창·단계별 재확인으로 제한하고 불명 저장은 복구 대상으로 남긴다.
5번 현재 캐시에는 만료 티켓 읽기 예외가 없다. result_read를 별도 캐시 읽기에 연결해야 하며 쓰기 current_approval에 그대로 넣지 않는다.
공통 HTTP12초는 유지한다. PC 비동기 시작/진행/취소·동일 결과 재전송·completed 표시/닫기 뒤 응답 무시는 연결 단계에서 검증한다.

## 검증·남은 확인

`test_device_enrollment.py`는 전용 MySQL에서 현재 권한/접속·잘못된MAC/binding·동시 재전송·매핑/결과/감사 rollback·취소/만료 격리·승인 scope·raw007/복합FK·실제dump/새DB복원을 검증한다.
`test_enrollment_approval_draft.py`와 기존 wire 두 파일은 별도 순수 프로토콜 검증이다. 실제 실행 결과는 PR 본문에 최종 코드 기준으로 기록한다.

재현 환경은 운영DB와 분리된 loopback MySQL8 전용DB(`remote_work_test_` 접두사), 새 테스트DB 생성/삭제 권한, PATH의 mysql/mysqldump다.
비공개 환경에 `MYSQL_TEST_URL`과 `MYSQL_MIGRATION_TEST_URL`을 그 전용DB URL로 설정하고 `REMOTEWORK_RUN_TASK_MIGRATION_DRAFT_TESTS=1`, `PYTHON_DOTENV_DISABLED=1`로 실행한다. 일반 `.env`/사용 중인 DB URL은 넣지 않는다.
`RemoteWork.Server`에서 `python -m pytest -q tests/test_device_enrollment.py tests/test_enrollment_approval_draft.py tests/test_enrollment_protocol_draft.py tests/test_device_protocol_draft.py`를 실행한다.
전체 모듈 실행은 기존 채팅 raw005 테스트가 빈 테이블을 요구하므로 `test_chat_migration_draft.py`를 먼저 실행한 뒤 나머지 모듈을 실행한다. 기본 순서 의존은 변경 전 코드에도 재현되며 제품 수정으로 숨기지 않는다.

완료: 서버 API·독립 DDL/초기화·장치/관리자 명시 설정·온라인 승인·참고 수신 검증.
미완료/미검증: Pi 실제 callback/만료 결과 캐시·PC 등록 버튼/중계·실물센서·두PC·TLS/키배포·공유적용·수동 전체GUI.
협의 대기: 5번 수신/최종 바이트 대조·실측 최악 시간, 팀장 운영 권한 부여 담당/미등록 계정/복구·삭제·재등록·이관/보존기간. 운영 활성화 전에 확인한다.
날짜LOG는 팀장만 게시한다. 협업 회신은 기존 PR43 댓글 한 건으로 전달하고 팀장이 LOG1/5에 취합한다.
