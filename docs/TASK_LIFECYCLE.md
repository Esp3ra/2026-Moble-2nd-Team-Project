# 업무 취소·마감 연장

<a id="task-lifecycle-recovery-display"></a>
## WinForms 원래 요청 확인 — 2026-10-22

응답 유실 뒤 같은 접속에서 복구창을 다시 열면 보관된 요청의 업무·동작·연장 신청 번호(있는 경우), 기존 마감일·희망일·사유를 읽기 전용으로 표시합니다. 현재 서버 조회값으로 표시하거나 이름·신청 사유를 새로 추정하지 않습니다. 원래 요청에 사유가 없는 승인·철회는 빈 사유를 유지합니다. 긴 사유는 스크롤로 확인합니다.

표시를 위해 조회·키 발급·자동 재전송을 추가하지 않습니다. 기존 결과 확인에서 `not_recorded`를 확인한 후 사용자가 원래 요청을 명시 재전송합니다. 권한 상실·접속 변경은 원문 표시를 지우고, 확정 실패 뒤 입력을 수정할 때 이전 원문으로 새 입력을 덮지 않습니다. 실제 폼/명시 서비스 대역 검사는 [검증 안내](../Tests/TaskLifecycle.Tests/README.md), 직접 실행 범위는 [화면 점검 기록](TASK_MANUAL_CHECK.md#task-recovery-display-check-20261022)을 따릅니다.

최신 업무를 읽은 뒤 입력·대상 확인용 조회에서403을 받는 경우에도 이전 업무·사유와 확인값을 지우고 명시 재조회를 요구합니다. 일시적 서버 오류는 입력을 보존합니다. 이전 조회 차수·접속·닫힌 창에서 늦게 도착한 오류는 현재 화면을 변경하지 않습니다.

2026-10-20 팀장의 [확정 D/E 정책](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-management-policy-approved-20261020)에 따른2번 소유 저장·조회·폼입니다. PR51의 공통 등록·성공 결과 읽기와009 검토 SQL, PR52의3번 취소·연장 대기 소비 코드가 alpha에 반영됐습니다. 현재 2번은 취소·연장 웹과 취소 시 업무 첨부 참조 해제까지 연결합니다. **같은 배포의 연결 검증과 통합 ACK 전 실제 변경은503으로 차단**하며, 다른 담당 제품 코드를 직접 수정하지 않습니다. 소유 환경·명시 대역 검증과 실제 공용 적용을 구분합니다.

<a id="task-lifecycle-web"></a>
## 웹 입력·실행·결과 확인

[업무 웹](TASK_WEB.md#task-web-lifecycle-writes)은 기존 정책·권한·W1 함수로 취소·연장 신청·승인·반려·철회를 연결합니다. 처음 연 업무와 배정 버전, 선택한 연장 신청 ID, 로그인 접속을 서명 초안에 고정하고, 입력 확인과 실제 실행에서 현재 조건을 다시 검사합니다. 실행 함수는 API와 공유하며 선택한 신청 ID까지 동일 요청 지문에 묶습니다. 자기 신청 판단 금지, 승인 전 기한 유지, 취소 후 복원 없음과 기존5상태 보존을 유지합니다.

ACK 미설치에서는 확인·실행을 차단하고 강제 POST도503입니다. 결과 읽기는 별도 현재 권한 검사로 이미 저장된 성공을 확인하므로 이후 ACK 해제나 명시 결과 읽기 전용 재시작에서도 보관된 결과를 조회할 수 있습니다. 읽기 전용에서는 신규 실행·키 발급을 허용하지 않습니다. 웹은 열린 확인폼의 원래 키/본문만 보존하며 브라우저 종료 후 자동 복원은 제공하지 않습니다. WinForms의 접속별 미해결 요청 보관과 구분합니다.

<a id="task-cancellation-attachments"></a>
## 취소와 업무 첨부 참조

취소의 같은 인증 트랜잭션에서 현재 권한·버전·취소 상태를 검사한 뒤, 취소 마커 저장과 업무 버전 증가 전에3번이 등록한 `release_task_files(db, task_id, expected=...)`를 호출합니다. 업무 첨부 참조만 해제하고 과거 제출 첨부와 다른 업무의 참조는 보존합니다. 참조·업무·신청·감사·성공 receipt는 함께 commit 또는 rollback하며, 물리 파일 정리는3번 제공부의 기존 commit 이후 처리에 맡깁니다.

제공부가 꺼져 있어도 저장된 공용 첨부 테이블을 확인합니다. 현재 업무 참조가 남아 있으면 취소를503으로 막아 참조를 남긴 채 취소 성공으로 처리하지 않습니다. 부분·잘못된 스키마도503이며 자동 DDL이나 공용 파일 모델 import로 복구하지 않습니다. 준비된 저장소에 현재 업무 참조가 없거나 공용 저장소 자체가 없는 기존 업무는 원래 취소 조건을 따릅니다.

2026-10-21 팀장이 선택 `attachment_ids`의 의미를 수용했습니다. 등록은 생략/빈 배열이면 첨부 없음, 수정은 생략이면 유지·빈 배열이면 해당 업무 참조 해제·값이 있으면 최종 전체 목록입니다. 2026-10-22 현재 [등록](TASK_CREATION.md#task-creation-attachment-selection)과 [일반수정](TASK_MANAGEMENT.md#task-management-attachment-selection)은 PR68의 공개 제공부를 같은 TX에서 소비하며, 전후 첨부 집합·감사·업무 버전·receipt를 함께 처리합니다. WinForms 등록/일반수정 파일 화면과 [웹 cookie 첨부 전송·결과물 진입/복귀](TASK_WEB.md#task-web-t3-consumers)도 연결했습니다. 명시 첨부 변경의503은 실제 제공부·스키마가 준비되지 않은 경우에 유지합니다. 이 연결이 취소·연장 운영 ACK나 공유 배포 검증을 완료한 것은 아닙니다. 최종 실행 결과는 [LOG/2번 구현·검증의 최신 회차](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)에 기록합니다.

## 실행 경계

기존 업무 조회/공통 쓰기와 함께 서버의 `REMOTEWORK_ENABLE_TASK_LIFECYCLE_DRAFT=1`이 필요합니다. 기본 off이며 쓰기와 별도 성공 결과 읽기 옵션이 모두 off인 startup에서는 lifecycle route/모델을 등록하지 않습니다. 아래의 읽기 전용 옵션은 새 쓰기나 키 발급을 허용하지 않습니다. 기본 테이블·공통 schema version을 변경하거나 공유 DB에 자동 migration을 실행하지 않습니다.

1번 PR51에서 [실제009 검토 SQL·적용 경계](../RemoteWork.Server/migrations/009_task_lifecycle.md)가 반영됐습니다. 현재2번 검증 실행기는 raw009를 사용하고 ORM의 두 테이블도 InnoDB·utf8mb4·utf8mb4_unicode_ci를 명시해 같은 기준을 따릅니다. [번호 없는 초안](../RemoteWork.Server/migrations/drafts/task_lifecycle.md)은 원문 비교용으로 보존하며009와 중복 적용하지 않습니다. 공통/002/004와 raw006/009를 준비한 소유 MySQL 검증을 공유 설치 승인으로 간주하지 않고 공통 schema1을 유지합니다.

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

모든 성공은 task.row_version을 한 번 증가시키며 assignment_revision과 업무5상태는 유지합니다. 반려완료/마감 경과도 연장을 신청할 수 있습니다. 승인 후에도 반려완료이므로 명시 재작업 시작→진행중→재제출이 필요합니다. 대기 중 제출 차단을 위한3번의 실제 신청 조회 소비 코드는 PR52로 제공됐으며 같은 공용 배포의 검증은 별도입니다. 자동 결과 제출은 없습니다.

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

응답이 불명확하면 같은 접속의 불변 본문/키를 보존합니다. WinForms는 빈 목록·화면 이동 후에도 명시 결과 확인을 제공하고 웹은 열린 원래 확인폼에서 복구합니다. not_recorded 확인 뒤 현재 쓰기가 허용될 때만 같은 요청/키를 명시적으로 재전송합니다. 자동 재전송/새 키 발급은 없습니다. PR51은 쓰기와 성공 결과 조회 authorizer를 분리합니다. 쓰기off 재시작에서 `REMOTEWORK_ENABLE_TASK_LIFECYCLE_RESULT_READS_DRAFT=1`을 명시하면 현재 권한·기존 키/보관기간·스키마를 검사해 보관된 성공 결과를 읽습니다. 새 키/쓰기나 통합ACK를 허용하지 않으며 기존 대역값을 일반 실행에 설치하지 않습니다. 현재 권한 상실·키 만료·미기록은 성공으로 추정하지 않고 현재 관리자에게 확인합니다. 로그아웃·401·계정 변경은 이전 자료를 폐기하고 늦은 응답을 반영하지 않습니다.

## 1·3번 제공 접점과 남은 통합

1번의 [공통 쓰기/DDL·등록 대조](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-lifecycle-member1-connection)는 PR51로 코드 제공됐습니다.2번의 `register_lifecycle_actions`와 `register_lifecycle_result_readers`를 startup에서 연결하며 기존 requested callback·현재 권한과8필드 최소 결과를 재사용합니다. 결과 읽기 등록은 신규 쓰기와 분리됐고,기존 owner callback을 덮지 않습니다. 최신 회신의 실제006/009/013 적용·37테이블/schema1 보존·백업/격리 복구 보고는 수신했으므로 같은 미적용 요청을 반복하지 않습니다. 이는1번의 실행 보고이며2번의 새 공유 검증이 아닙니다. 같은 배포 SHA·옵션·파일 저장 권한·검토자 관계·실계정/두PC·3번 소비 경합과 통합 ACK 확인은 별도로 남습니다.

3번의 [취소·연장 대기 소비 경로](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-lifecycle-member3-connection)는 PR52에 반영됐습니다.2번 helper는 `ensure_not_cancelled(db,task_id)`와 `pending_extension_facts(db,task_id,assignment_revision,lock=False)`이며3번 쓰기에서는 잠금 옵션을 소비합니다. 실제 pending 어댑터는 미준비를False로 숨기지 않습니다. 기존 `ExternalFacts`의 검토자 원본은 유지하며 취소를 pending=True로 위장하지 않습니다. 제공된 제출·판단·첨부 쓰기 연결과 같은 공용 배포에서의 활성화·동시성 증거를 구분합니다. 과거 결과물/이력/다운로드는 현재 합의된 읽기 권한으로 유지합니다.

실제 공용 배포에서3번 제출/첨부와 취소·신청의 경합, 공유 DB/실계정·두PC·수동 GUI/다중DPI는 별도 확인 대상입니다.4번 [알림 A1~A6·협업 B1~B7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member4-policy-adopted-20261021)은 진행 기준으로 채택됐으며2번의 [보수적인 버전 이력 reader](TASK_QUERIES.md#task-continuity-reader)는 알려진2번 행동의 완전한 구간만 판정합니다. 제출·반려 구간까지의 전체 연속성 증명은 아직 후속이며 불명은None을 유지합니다. [현재 본인 기한 사실](TASK_QUERIES.md#own-deadline-current-facts)과 [기한 변경의 같은 TX 결합 위치](TASK_QUERIES.md#deadline-mutation-boundary)는 제공했지만 실제 사건 저장·확인 writer·API/DDL·초기 자료 전환은1·2·4번 기술 대조가 남습니다. 3번의 웹 첨부/관리 읽기/명시 복귀와2번 일반수정 첨부 교체·화면은 연결됐으며 위 운영 검증과 구분합니다. 별도 검토 요청은 이번 확정에 포함되지 않습니다. 회신만을 위한 새 MD·브랜치·추가 PR은 요구하지 않습니다.

## 검증 안내

[소유 MySQL·실제 HTTP 및 서버 검사](../Tests/TaskLifecycle.Integration.Tests/README.md)는 TESTING의 명시 연결 대역을 사용합니다. [클라이언트 제어 검사](../Tests/TaskLifecycle.Tests/README.md)는 공통 ApiClient/실제 폼과 명시 HTTP·workflow 대역을 사용합니다. 실행 수치와 미실행은 [LOG 구현·검증](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)에 별도로 기록합니다.

기존 서버 검사는 실제 results blueprint도 등록해 정상 결과물 GET을 확인한 뒤,유효한 통합ACK가 없을 때 취소·연장5경로가503·저장 무효과인지 검사했습니다. 이것은 **미연결 차단 검증**이며 실제3번 guard/pending 소비·쓰기 성공/경합 검증으로 세지 않습니다. 다른 MySQL 기본 엔진/문자셋에서도 ORM 두 테이블이009 옵션과 한글·이모지를 유지하는 검사를 포함했습니다. 현재 웹·취소 첨부 소비의 추가 검사 대상과 최종 실행 결과는 [업무 웹 검증 안내](../Tests/TaskWeb.Tests/README.md) 및 LOG 최신 회차에서 구분합니다.
