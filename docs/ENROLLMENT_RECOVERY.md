# 실패한 지문 등록의 종료·명시 재발급 — 1번 서버 계약

2026-10-21, 1번 → 5번·팀장. 사용자가 서버 기능 구현을 요청한 **서명 결과·물리 store 증거가 없는 만료 작업**을 대상으로 한다. 현재 실패 사례처럼 ledger metadata만 있고 기존 발급 접속이 끝났을 때, 원래 작업을 삭제하거나 성공으로 바꾸지 않고 새 작업을 만드는 접점이다. 5번의 Pi/PC 소비 코드·실물 전환은 별도다.

## 순서와 책임

1. **5번/운영자**가 기존 Pi·PC writer를 지속 중지하고 잠금/백업을 유지한다. 그 후 새 read-only inventory로 해당 슬롯의 빈 상태와 관찰 시각을 확인한다. 기존 템플릿의 일괄 삭제·시험 초기화는 이 기능에 포함하지 않는다.
2. **1번 운영자**가 현재 서버 job/device/claim/revision/key·매핑·010 증거와 비공개 점검 자료를 대조한다. 전달 ZIP의 요약과 원본 Pi 보고서 해시를 구분한다. 원본을 직접 읽지 않았다면 5번의 원본 대조 보고를 수신한 것으로 기록하고 직접 검증했다고 쓰지 않는다.
3. 검토한 MySQL의 공통 schema1·007·010·[011](../RemoteWork.Server/migrations/011_enrollment_recovery.md), 실제 실행 SHA·부모 옵션·전환할 새 Pi/PC 코드를 확인한다. 새 recovery 옵션도 기본 off다. 구형 SQLite writer를 다시 켜서 새 모드를 사용하는 것은 금지한다.
4. 서버 콘솔에서 아래 CLI를 먼저 preview하고 `--apply`로 **운영자 종료 기록만** 남긴다. HTTP의 `slot_empty=true`만으로 복구 승인을 만들 수 없다.
5. 그 운영자가 **현재 새 관리자 로그인**에서 새 대상 회원 ID와 새 요청 UUID를 정해 재발급 API를 호출한다. 이전 세션을 인수하지 않는다. 5번은 반환된 새 job/ticket을 고정해 기존 [MySQL 등록 계약](ENROLLMENT_MYSQL_DURABILITY.md)의 capture→intent→store→physical-result→complete를 소비한다.

이 계약의 제출·검증만으로 현재 서버의 flag/DDL, 기존 예약, Pi 서비스나 센서가 변경되는 것은 아니다. 실제 종료·재발급·실물 성공은 각각 실행 결과를 확인해 별도 보고한다.

## 비공개 운영자 확인서

`--report-file`은 실제 대조한 원본 JSON 또는 5번에게 받은 운영자 점검 JSON이다. 그 파일의 **정확한 bytes**에 대한 SHA256을 `report_sha256`에 적는다. `source_report_sha256`은 보관 중인 **Pi 원본 보고서**의 SHA256이며 요약 자료일 때 두 해시는 다를 수 있다. 서버는 파일 해시·구조·순서·현재 DB 바인딩을 검사한다. 원격 센서의 빈 상태나 요약 속 원본 해시를 스스로 입증하지 않으므로 운영자가 내용과 지속 fencing을 확인한 뒤 승인해야 한다. 보고서 JSON의 문자열을 명령으로 실행하지 않는다.

확인서는 아래 **15필드만** 허용한다. 실제 ID/키/원문은 공개 GitHub에 올리지 않는다. revision/key_epoch는 정규 양의 BIGINT 문자열, slot은 JSON 정수다.

```json
{
  "enrollment_id": "00000000-0000-4000-8000-000000000001",
  "device_id": "00000000-0000-4000-8000-000000000002",
  "slot": 12,
  "operation_revision": "1",
  "claim_revision": "1",
  "key_epoch": "1",
  "report_sha256": "0000000000000000000000000000000000000000000000000000000000000000",
  "source_report_sha256": "0000000000000000000000000000000000000000000000000000000000000000",
  "fenced_at": "2026-10-21T00:00:00.100000Z",
  "observed_at": "2026-10-21T00:00:00.200000Z",
  "checked_at": "2026-10-21T00:00:00.300000Z",
  "writer_fenced": true,
  "slot_empty": true,
  "signed_result_present": false,
  "target_binding_verified": false
}
```

빈칸/가짜 해시를 실제 확인서에 그대로 쓰지 않는다. 크기는 보고서64KiB·확인서16KiB 이하, 중복 JSON 키/추가 필드/문자열 bool은 거절한다. 시각은 UTC `Z` 또는 `+00:00`, 최대 소수6자리이며 `fenced < observed <= checked <= server_now`여야 한다. 관찰은 원래 ticket 만료+5초 및 마지막 job 변경 이후여야 한다. 기존 permit이 끝나기 전 관찰을 나중에 가져와 재사용할 수 없다. 승인 전부터 새 writer 전환까지 중지·단일 잠금을 계속 유지하고, 끊겼다면 최신 관찰과 검토로 다시 준비한다. 오래된 확인서만 보고 현재 빈칸을 추정하지 않는다.

```text
# 비공개 설정을 사용하는 서버 폴더/환경에서. 실제 관리자 ID를 사용한다.
python -m flask --app remotework close-enrollment-for-recovery --login-id <관리자ID> --attestation-file <비공개확인서.json> --report-file <대조한비공개자료.json>
# preview_only/ready를 확인한 뒤 명시 적용한다.
python -m flask --app remotework close-enrollment-for-recovery --login-id <관리자ID> --attestation-file <비공개확인서.json> --report-file <대조한비공개자료.json> --apply
```

현재 활성 admin+해당 장치 등록 grant가 필요하다. 서버 콘솔 권한이 필요한 운영자 행위이며 일반 직원/PC 입력이 대신 실행하지 않는다. 같은 확인서 재실행은 기존 recovery_id만 반환하고 감사·종료·시간을 중복 생성하지 않는다. 다른 운영자/확인서로 이미 종료한 작업을 덮어쓰지 않는다.

적용 시 원래 job은 `recovery_required`, claim은 `quarantined`가 된다. ticket·UUID·원발급자/접속·revision·expires_at·completed_at·원문 결과는 변경하지 않는다. 특히 completed_at을 새로 써서 기존 결과 읽기 창을 늘리지 않는다. 복구 행에는 원래 상태/운영자/두 보고 해시/확인서 해시/시각을 저장하고 `enrollment.operator_closed` 감사를 같은 transaction에 남긴다. reason은 서명·store 증거 없는 만료 작업의 빈 슬롯 관찰이다. 과거 기록은 삭제하지 않는다.

## 현재 로그인에서 조회·재발급

공통 API envelope/관리자 Bearer를 사용한다. base는 `/api/v1/device-enrollments/recoveries`다.

| 경로 | 동작 |
|---|---|
| GET `/{recovery_id}` | 운영자가 종료한 최소 상태와 source/new ID를 조회. 새 등록 권한을 발급하지 않음 |
| POST `/{recovery_id}/reissue` | 아래 정확한2필드로 새 작업 발급. `data.recovery`와 `data.job` 반환 |

```json
{"client_request_id":"00000000-0000-4000-8000-000000000003","target_user_id":123}
```

target_user_id는 **회원 생성 응답의 실제 활성 users.id**인 JSON INTEGER다. 장치·슬롯은 운영자 승인에서 고정되며 HTTP에서 바꾸거나 빈 슬롯 확인을 추가하지 않는다. 최초 재발급은 **같은 운영자**의 현재 새 로그인에서 가능하다. 기존 발급 접속이 끝나도 원래 session_hash를 편집하지 않는다. 첫 재발급 후에는 그 새 작업을 발급한 접속으로 고정된다.

성공200의 `data.job`은 기존 발급 결과와 동일한 공통필드/새 request_envelope를 가진다. 새 job UUID·새 로그인·새 대상·새 client_request_id·새 만료가 생기고 operation_revision은1, **claim_revision은 이전 값+1**이다. 원래 job과 source/new 복구 링크를 보존하며 현재 claim 포인터를 새 job으로 이동한다. 원래 작업/예약 행을 삭제하지 않는다. 작업·claim·복구 링크·`enrollment.reissued` 감사는 모두 같은 transaction이다.

- 동일 접속·recovery_id·요청 UUID·대상 재전송은 같은 job/현재 상태를 반환한다. TTL/nonce/revision을 새로 발급하지 않는다. HTTP 요청 추적 request_id는 요청마다 다를 수 있다.
- 다른 UUID/대상으로 재시도하면409, 첫 발급 후 다른 로그인은403이다. 결과 보관기간이 끝나면 기존410을 유지하고 재실행하지 않는다. 다음 실패의 복구가 새 claim을 만든 뒤 오래된 API 재전송도409로 막는다.
- 같은 요청이 동시에 도착해도 공통 guard와 현재 행 잠금으로 하나만 생성한다. commit 실패는 job/claim/링크/감사를 함께 되돌린다.
- 최초 재발급 전 현재 장치 grant/활성/키/프로필/후보 슬롯, 승인 당시 바인딩·원래 job 상태/수정시각, 현재 claim·새 대상활성을 다시 검사한다. 다른 슬롯·unrelated 과거 작업/매핑을 자동 무시하지 않는다.
- 기존 일반 발급은 만료 claim을 자동 회수하지 않는다. 새 재발급도 센서 store를 수행하거나 성공 매핑을 바로 만들지 않는다. 실제 두 채취/010 증거/complete가 필요하다.

## 거절·담당 경계

실패 상태라는 이유만으로 전부 이 경로에 넣지 않는다. **서명 원문/결과 hash·010 intent 또는 결과·기존 매핑(비활성 포함)**이 하나라도 있으면 `ENROLLMENT_PHYSICAL_RECOVERY_REQUIRES_REVIEW`로 별도 검토한다. 이번 실제 사례와 다른 ‘store 후 응답 유실’, ‘signed 실패 결과 있음’, ‘다른 회원 슬롯’ 복구를 이 코드로 자동 처리하지 않는다. 늦게 원문이 발견되면 비공개 보존하고 기존 보관/claim 검사를 유지한 별도 검토로 넘긴다. 원문 없는 실패를 서명·성공으로 꾸미지 않는다.

주요 오류: schema 미준비503, 현재 권한/운영자/접속 불일치403, 미존재404, 증거·키·프로필/claim 변경·중복본문409, 이전 ticket의 마지막 승인보다 이른 관찰409, 만료된 새 job 결과410. 구체 코드와 비공개 상태를 확인하고 DELETE·직접 SQL·다른 손가락 새 요청으로 우회하지 않는다.

1번은 서버/DDL/운영자 접점과 검증을 담당한다. 5번은 이 새 재발급 결과의 Pi/PC 소비, 기존 증거 보존/새 MySQL 모드 전환과 실물 점검을 담당한다. 코드 게시·원본 병합·실제 DDL/설정 적용·운영자 종료·새 발급·센서 저장·회원 매핑 완료를 구분해 회신한다. 서버만 구현했다고 5번 실물 등록까지 완료로 기록하지 않는다.

## 재현 검증

소유 MySQL 전용 runner에서 `python -m pytest -q tests/test_enrollment_recovery.py tests/test_enrollment_durability.py tests/test_device_enrollment.py`를 실행한다. 테스트의 합성 키/회원/장치·시계를 사용한다. 사용자 서버/DB·실제 센서를 대상으로 실행하지 않는다. recovery 검사는 raw011·권한/현재성·해시/시각·미리보기/종료 멱등·새 로그인·단일 재발급 경합·rollback·새010→complete와 매핑·반복 실패 이력·실제 dump→새DB 복원을 포함한다. 실물 센서·두 PC·5번 신규 코드와 실제 서버의 결합은 별도 검증이다.
