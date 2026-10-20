# 업무 취소·마감 연장

2026-10-20 팀장의 [확정 D/E 정책](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-management-policy-approved-20261020)에 따른2번 소유 저장·조회·폼입니다. 취소와 연장 신청/철회·승인/반려를 구현하되, **1번 공통·3번 제출/첨부 차단 연결 확인 전 실제 변경은503으로 차단**합니다. 다른 담당 제품 코드를 수정하지 않습니다. 테스트 대역으로2번 기능을 검증한 것을3번 실제 통합·공유 적용 완료로 표시하지 않습니다.

## 실행 경계

기존 업무 조회/공통 쓰기와 함께 서버의 `REMOTEWORK_ENABLE_TASK_LIFECYCLE_DRAFT=1`이 필요합니다. 기본 off이며 off startup에서는 lifecycle route/모델을 등록하지 않습니다. 기본 테이블·공통 schema version을 변경하거나 공유 DB에 자동 migration을 실행하지 않습니다.

1번 PR50에서 `009_task_lifecycle.mysql.sql` **번호 예약**을 확인했습니다. 이번 제공물은 [번호 없는 개발 SQL](../RemoteWork.Server/migrations/drafts/task_lifecycle.mysql.sql)과 모델입니다.009의 SQL 내용·정식 버전·공유 설치 승인으로 간주하지 않습니다. 공통/002/004와 이 초안을 함께 준비한 소유 MySQL에서만 자체 검증합니다. 기존 결과물 자료 보존 확인용 raw006은 실제3번 쓰기 연결과 구분합니다.

스키마와 옵션만으로 저장을 허용하지 않습니다. 다음 배포 확인은1·3번이 해당 소비 경로를 실제 연결·검증한 뒤 설치합니다.

```json
{"contract":"member2-task-lifecycle-v1","schema":"task-lifecycle-draft-v1","mode":"integrated","member1":"common-write-and-ddl-v1","member3":"cancel-and-extension-submit-guards-v1"}
```

연결 위치는 `app.extensions["task_lifecycle_integration_ack"]`입니다. 문서 열람만으로 설정하거나 사용자 요청에 값을 받아 설정하지 않습니다. 테스트 전용 `mode=test-double`/member1·member3=`explicit-test-double`은 TESTING=true·실제 results blueprint 비활성에서만 허용됩니다. 일반 실행/실제3번 경로에 테스트 대역을 사용할 수 없습니다. GET 문맥의 `write_enabled=false`이면 화면도 실행을 막고 기술 연결 필요를 안내합니다.

## 확정 행동

| 행동 | 실행자·상태 | 결과 |
|---|---|---|
| 업무 취소 | 현재 활성 지정 관리자, 진행전/진행중/반려완료, 필수 사유 | 별도 취소 표시.5상태·배정/과거 결과 보존.대기 연장은 cancelled로 종료.복원 없음 |
| 연장 신청 | 현재 담당자, 진행중/반려완료, 마감 전·후 가능 | 현재 기한보다 뒤/오늘 KST 이상인 희망일·필수 사유, 다른 활성 지정 관리자 필요.현재 배정의 대기 신청 한 개 |
| 승인 | 현재 지정 관리자, 신청자 본인 금지, pending | 신청한 기한 그대로/승인일 KST 이상.유효 마감일만 변경 |
| 반려 | 현재 지정 관리자, 신청자 본인 금지, pending | 기존 기한 유지.판단 사유 내용은 선택 |
| 철회 | 현재 담당자인 신청자 본인, pending | 기존 기한 유지.재신청 가능 |

모든 성공은 task.row_version을 한 번 증가시키며 assignment_revision과 업무5상태는 유지합니다. 반려완료/마감 경과도 연장을 신청할 수 있습니다. 승인 후에도 반려완료이므로 명시 재작업 시작→진행중→재제출이 필요합니다. 대기 중 제출 차단은3번이 실제 신청 조회를 연결해야 완성됩니다. 자동 결과 제출은 없습니다.

## API

모든 경로는 `/api/v1` 아래입니다. 공통 Bearer·envelope·`Idempotency-Key`를 사용합니다. ID와 업무 버전은 signed BIGINT 정규 문자열, 사용자 ID/배정 차수는 signed INTEGER JSON 정수, 날짜는 DATE/`YYYY-MM-DD`, 시각은 UTC ISO8601입니다. `expected` 두 필드는 `expected_row_version`, `expected_assignment_revision`입니다. 중복/추가 JSON 키·타입 변형·중복 쿼리는 거절합니다. 사유는 UTF-8 유효 문자열·최대60,000바이트의 TEXT 기술 상한을 적용하며 원본 공백을 보존합니다. 별도로 기존 공통 요청 본문 전체32KiB 상한을 유지하며 JSON escape를 포함한 실제 직렬화 크기도 검사합니다.

| 요청·action | expected 외 본문 |
|---|---|
| `POST /tasks/{id}/cancel` · `task.cancelled` | `reason` |
| `POST /tasks/{id}/deadline-extensions` · `task.deadline_extension_requested` | `requested_due_date`, `reason` |
| `POST /tasks/{id}/deadline-extensions/{extension_id}/approve` · `task.deadline_extension_approved` | 없음 |
| `POST /tasks/{id}/deadline-extensions/{extension_id}/reject` · `task.deadline_extension_rejected` | `reason` 문자열, 빈 내용 허용 |
| `POST /tasks/{id}/deadline-extensions/{extension_id}/withdraw` · `task.deadline_extension_withdrawn` | 없음 |

공통 key의 target은 모두 task_id입니다. 결정 경로의 extension_id도 내부 fingerprint에 포함하여 같은 task의 다른 신청에 같은 키를 사용할 수 없습니다. 결과200의 data는 아래8필드만 포함합니다. 취소의 extension_id/requested_due_date는 NULL이며 사유·임의 신청 상태를 receipt에 저장하지 않습니다.

```json
{"task_id":"101","row_version":"4","assignment_revision":1,"task_status":"in_progress","updated_at":"2026-10-20T06:00:00Z","extension_id":"2","requested_due_date":"2026-10-23","due_date":"2026-10-21"}
```

`GET /tasks/{id}/lifecycle`은 `data.context`를 반환합니다. 필드는 task_id/row_version/assignment_revision/task_status/start_date/due_date/updated_at/assignee_id/manager_user_ids/is_cancelled/cancellation/pending_extension/write_enabled입니다. cancellation은 NULL 또는 cancelled_by/cancelled_at/reason 객체, pending_extension은 NULL 또는 아래 신청 레코드입니다. 현재 담당자/지정 관리자만 조회할 수 있습니다.

`GET /tasks/extension-requests?scope=own|managed&status=...`는 `data.items`를 반환합니다. status 생략은 전체이고 pending/approved/rejected/withdrawn/cancelled를 지원합니다. 현재 배정만 대상으로 own은 현재 담당자이면서 본인이 신청한 행, managed는 현재 지정 관리자 업무를 조회합니다. 과거 행을 삭제하지 않으며 이전 관계만으로 열람 권한을 부여하지 않습니다. 초기 반환은 전체 목록/extension_id 내림차순이며 지원하지 않는 페이지 쿼리를 받지 않습니다.

신청 레코드는 extension_id/task_id/assignment_revision/requested_by/original_due_date/requested_due_date/reason/status/created_at/decided_at/decided_by/decision_reason입니다. 마지막 세 판단 필드는 pending에서 NULL입니다.

기존 목록의 선택적 `include_cancelled=true`는 취소 업무도 표시합니다. 생략/false는 취소 제외, 잘못된 값은400입니다. 취소 저장소가 있으면 요약/상세에 is_cancelled를 추가하고 기존 클라이언트 DTO의 기본값은false를 유지합니다. 취소 상세도 기존 현재 조회 권한을 검사합니다. 기능 옵션을 끄더라도 DB에 남은 취소 표시는 기본 목록/기존2번 새 쓰기에 계속 적용합니다.

## 저장·권한·복구

별도 task_cancellations와 task_deadline_extensions를 사용합니다. 취소 marker는 task_id 기본키이며 신청은 과거 task_assignments와 복합 FK로 연결합니다. nullable generated pending_revision의 UNIQUE(task_id,pending_revision)가 닫힌 신청을 보존하면서 대기 한 개를 강제합니다. 기존 tasks/배정/결과물/첨부를 ALTER·삭제하지 않습니다. 준비 검사에서 PK·정확한 STORED 생성식·단일 대기 unique·현재DB FK·signed 식별자/필수 열을 대조합니다. 부분/잘못된 스키마는503이고 DDL을 자동 복구하지 않습니다.

모든 쓰기는 공통 guard/인증 재검사와 부모 업무→현재 배정/관리자→정렬 사용자→취소/신청 행 잠금 안에서 권한·버전·상태·KST 날짜를 검사합니다. 업무/신청/취소·감사·성공 receipt는 같은 트랜잭션입니다. 현재 활성 관리자 중 다른 판단자가 없으면 신청을 거절하며 현재 관계를 서버에서 재검사합니다. 취소는 이후 수락·재작업·일반 수정·재배정·관리자 변경/연장 신규 쓰기를 차단합니다.

응답이 불명확하면 같은 접속의 불변 본문/키를 보존하고 빈 목록·화면 이동 후에도 명시 결과 확인으로 복구합니다. not_recorded 확인 뒤에만 같은 요청/키를 재전송합니다. 자동 재전송/새 키 발급은 없습니다. 현재 권한 상실·410/기능 중단으로 결과가 불명확하면 현재 관리자 확인이 필요합니다. 과거 권한으로 결과 조회를 우회하지 않습니다. 로그아웃·401·계정 변경은 이전 자료를 폐기하고 늦은 응답을 반영하지 않습니다.

## 1·3번 제공 접점과 남은 통합

1번은 [공통 쓰기/DDL·등록 대조](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-lifecycle-member1-connection)를 확인합니다.2번은 새 취소/철회 action과 현재 접근 callback을 공개 registrar로 제공하며 기존 requested callback을 재사용합니다. builtin approved/rejected는 기존 owner callback을 덮지 않고2번 callback을 제공하므로 공통 설치/키·결과 조회 수명을 대조해야 합니다.

3번은 [취소·연장 대기 소비 경로](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-lifecycle-member3-connection)를 확인합니다.2번 helper는 `ensure_not_cancelled(db,task_id)`와 `pending_extension_facts(db,task_id,assignment_revision,lock=False)`입니다. 실제 pending 어댑터는 미준비를False로 숨기지 않습니다. 기존 `ExternalFacts`의 검토자 원본은 유지하며 취소를 pending=True로 위장하지 않습니다. 제출·판단·legacy multipart 및 JSON 업로드 start/chunk/complete의 쓰기에 취소 guard를 같은TX/잠금으로 연결해야 합니다. 과거 결과물/이력/다운로드는 현재 합의된 읽기 권한으로 유지합니다.

실제3번 제출/첨부와 취소·신청의 경합, 공유 DB/실계정·두PC·수동 GUI/다중DPI는 아직 검증 완료가 아닙니다.4번 알림 수치/기한 revision·공용 이벤트·웹/공용 첨부·별도 검토 요청 정책은 이번 구현에 포함하지 않습니다. 새 회신 MD/브랜치/추가 PR은 요구하지 않습니다.

## 검증 안내

[소유 MySQL·실제 HTTP 및 서버 검사](../Tests/TaskLifecycle.Integration.Tests/README.md)는 TESTING의 명시 연결 대역을 사용합니다. [클라이언트 제어 검사](../Tests/TaskLifecycle.Tests/README.md)는 공통 ApiClient/실제 폼과 명시 HTTP·workflow 대역을 사용합니다. 실행 수치와 미실행은 [LOG 구현·검증](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)에 별도로 기록합니다.
