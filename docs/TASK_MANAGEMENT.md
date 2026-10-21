# 업무 수정·재배정·관리자 변경

2026-10-20 팀장의 [A~E 확정 정책](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-management-policy-approved-20261020) 중 A/B/C의 2번 개발 구현입니다. 기존 공통 인증·현재 관계 검사·쓰기 guard·동일 요청 키·감사·성공 결과 보관을 재사용합니다. 다른 담당 제품 코드·공통 schema version·DDL은 변경하지 않습니다.

## 실행과 화면

서버에서 기존 업무 조회/공통 쓰기와 함께 `REMOTEWORK_ENABLE_TASK_MANAGEMENT_DRAFT=1`을 명시해야 합니다. 조회 옵션은 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`, 공통 쓰기 옵션은 기존 [WRITE_SUPPORT](WRITE_SUPPORT.md)의 실행 조건을 따릅니다. 신규 등록·수락·결과물의 옵션은 각 기능의 별도 조건입니다. 새 관리 옵션은 기본 off이고 off에서는 이 관리 모듈을 import하거나 관리 경로를 등록하지 않습니다. 옵션을 켜도 새 테이블을 만들거나 공유 DB에 migration을 적용하지 않습니다.

관리 업무 상세에서 **업무 수정 / 담당자 변경 / 관리자 변경**을 각각 엽니다. 최신 상세·write-context와 후보 선택을 확인한 뒤 별도 **변경 실행**을 누릅니다. 화면을 열거나 입력/대상을 확인하는 것만으로 저장하지 않습니다. 서버 성공을 확인한 뒤 현재 필터로 목록/상세를 다시 조회합니다. 현재 지정 관리자만 A/B/C를 실행할 수 있으며 관리자 역할만으로 타 업무를 수정하지 못합니다.

일반 수정과 재배정은 진행전만 허용합니다. 관리자 변경은 진행전·진행중·반려완료에서 허용하고 활성 관리자가 최소 한 명 남아야 합니다. 최초 등록과 달리 이후에는 등록자/본인도 다른 활성 관리자가 남으면 제외할 수 있습니다. 지정 관리자와 결과물 검토자는 서로 다른 관계입니다.

<a id="task-management-web"></a>
## 업무 웹 연결

위 옵션과 공통 cookie 로그인에서 `/tasks/<id>/manage/edit`, `/tasks/<id>/manage/reassign`, `/tasks/<id>/manage/managers`의 GET/POST를 제공합니다. 등록·수락 옵션을 함께 켤 필요는 없습니다. 현재 지정 관리자와 허용 상태인 상세에만 링크를 표시하며 직접 진입도 서버 권한 검사를 거칩니다. 취소 마커는 lifecycle 옵션을 껐더라도 현재 저장값으로 검사합니다. 업무 API 경로·JSON 응답·DB 구조와3번 제품은 변경하지 않습니다.

처음 GET은 같은 트랜잭션에서 잠근 업무/현재 배정/관리자/사용자의 변경 전 정보와 원래 버전을 서명한 초안을 표시합니다. 검색과 페이지 이동은 입력·선택과 원래 버전을 유지하며,명시적인 **최신 업무 다시 불러오기**만 입력을 초기화합니다. 초안에는 W1 요청 키가 없고 GET/검색은 업무·관계·감사·receipt를 저장하지 않습니다. 후보는 기존 공통 서비스를 사용하며 선택 밖 후보·사용 불가 기존 관리자도 유지해 직접 해제하게 합니다. 등록자/본인을 자동 포함하지 않습니다. 이 HTML의 선택100명 한도는 전역 업무/API 최대 정책이 아닙니다.

**변경 내용 확인**은 현재 권한·원래 버전·상태·취소·과거 수락/제출 자료와 후보를 검사한 뒤 W1 키와 확인폼을 준비합니다. 화면에 변경 전/후 내용을 표시하며 **변경 실행**에서 `execute_management`를 호출합니다. API도 같은 함수를 사용하므로 최종 상태·KST 날짜·후보·원자 저장 규칙은 동일합니다. form의 CSRF와 목적별 서명은 현재 로그인·action/target·본문·버전·키·복귀 필터를 고정합니다. 다른 로그인·변조·중복/미지원 입력·잘못된 복귀 조건은 거절하며 브라우저에 Bearer 토큰이나 내부 HTTP 호출을 추가하지 않습니다.

명시적 결과 조회에서 `not_recorded`가 확인된 때만 같은 키/본문 재전송을 제공합니다. 성공한 관리자 인계는 본인 제외 후 상세 권한 없음과 구분해 표시합니다. 성공 응답 유실 후403이면 티켓을 보존하고 현재 관리자에게 확인하도록 안내하며 이전 정보/권한으로 조회하거나 변경을 재전송하지 않습니다. 열린 확인폼만 복구하며 브라우저/폼을 닫은 뒤 요청 복원 저장소는 제공하지 않습니다. 자세한 화면·실행 제한은 [업무 웹](TASK_WEB.md#task-web-management)을 따릅니다.

## 실제 HTTP 계약

모든 경로는 `/api/v1` 아래이며 Bearer·공통 envelope·`Idempotency-Key`를 사용합니다. 요청 본문에 표의 필드 외 값을 넣거나 JSON 키를 중복하면 거절합니다. `task_id`·`expected_row_version`은 양의 signed BIGINT 정규 문자열이고 `expected_assignment_revision`은 양의 signed INTEGER JSON 정수입니다. 사용자 ID도 정수입니다.

| 경로·공통 action | expected 두 필드 외 입력 | 허용 상태 |
|---|---|---|
| `PATCH /tasks/{task_id}` · `task.updated` | `title`, `description`, `start_date`, `due_date` 및 선택 `attachment_ids`(아래 연결 차단 참조) | `not_started` |
| `POST /tasks/{task_id}/reassign` · `task.reassigned` | `assignee_id` | `not_started` |
| `PATCH /tasks/{task_id}/managers` · `task.managers_changed` | `manager_user_ids` 정수 배열 | `not_started`, `in_progress`, `returned` |

expected 두 필드는 `expected_row_version`, `expected_assignment_revision`입니다. 제목은 공백만 제외·Unicode 문자 200개 이내이고 본문은 빈 문자열을 허용합니다. 날짜는 `YYYY-MM-DD`/DB DATE이며 시작일≤마감일, 새 마감일≥수정 당일 KST입니다. 재배정 후보는 활성 직원/관리자, 관리자 후보는 활성 관리자입니다. 중복/빈 관리자 배열은 거절합니다. 동일 값은409 `TASK_CHANGE_NOT_NEEDED`이며 성공 receipt·감사를 만들지 않습니다.

성공200의 `data`는 다섯 필드만 반환합니다.

```json
{"task_id":"101","row_version":"2","assignment_revision":1,"task_status":"not_started","updated_at":"2026-10-20T01:00:00Z"}
```

매 성공은 row_version을 한 번 증가시킵니다. 실제 재배정만 assignment_revision도 한 번 증가시키고 새 배정의 accepted_at은 NULL입니다. 과거 배정을 보존하며 새 담당자가 명시적으로 수락해야 합니다. 수락/성공 제출 흔적이 있는 진행전 모순 자료는409 `TASK_MANAGEMENT_DATA_INCONSISTENT`로 차단하고 초기화하지 않습니다. 결과물 기능을 끈 2번 전용 DB에 제출 테이블이 없는 것은 허용합니다. 결과물 활성화 중 테이블 누락, 또는 활성화 여부와 관계없이 존재하는 테이블의 필수 열 부족은503으로 차단합니다.

<a id="task-management-attachment-selection"></a>
## 첨부 최종 목록 수용과 현재 차단 범위 — 2026-10-21

[팀장이 수용한 3번 제안](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member3-proposals-accepted-20261021)은 일반 수정의 `attachment_ids` 생략 시 기존 첨부 유지, `[]`면 전부 해제, 값이 있으면 최종 목록으로 교체입니다. 별도 삭제 배열을 만들지 않습니다. 최대 10개의 소문자 하이픈 포함 정규 UUID 문자열만 허용하며 중복·null·비문자열은 400입니다. 원문 필드 유무·배열 순서는 W1 본문에 그대로 보존합니다. 재배정·관리자 변경에는 이 필드를 허용하지 않습니다.

**현재 구현은 형식 검사와 안전한 차단까지입니다.** 첨부 필드를 생략한 일반 수정은 기존 경로로 실행해 첨부를 유지합니다. `attachment_ids`를 명시하면 빈 배열도 포함해, 현재 권한·버전·상태·과거 수락/제출 자료·날짜 검사 뒤 `503 TASK_ATTACHMENT_REPLACEMENT_NOT_READY`로 차단합니다. 업무 본문을 함께 바꾸려 해도 일부만 저장하지 않으며 업무/첨부/버전·감사·성공 receipt를 만들거나 변경하지 않습니다. 앞선 권한·버전 검사가 실패하면 그 오류가 우선합니다. WinForms·웹 관리 화면의 첨부 입력도 아직 연결하지 않았습니다.

첨부만 바뀌어도 `task.updated`로 한 번 처리하고, 본문과 최종 첨부 집합이 모두 같으면 `409 TASK_CHANGE_NOT_NEEDED`로 끝내는 정책은 수용됐습니다. 이를 실행하려면 같은 트랜잭션에서 현재 첨부 목록과 변경 차이를 읽는 3번 공개 함수(T3-1), 감사 최소 필드(T3-2), 기능 off/오류 조건(T3-3)이 필요합니다. 현재 `replace_task_files`의 변경 후 목록만으로 변경 전 값을 추정하거나, 3번 ORM·SQL을 직접 읽어 우회하지 않습니다. 따라서 이 정책을 실제 수정 구현 완료로 표시하지 않습니다. 신규 등록의 빈 기존 목록은 이 문제와 구분해 [실제 제공부 연결](TASK_CREATION.md#task-creation-attachment-selection)을 지원합니다.

## 권한·경합·결과 유실

서버는 공통 트랜잭션 안에서 인증된 현재 관리자 관계를 확인한 뒤 JSON 형태·필수 필드·타입/입력 형식을 검사합니다. 최초 변경에서는 업무/배정 버전→허용 상태→자료 보존·현재 후보/날짜 조건을 재검사합니다. 같은 요청의 성공 재조회는 현재 권한과 동일 본문을 확인하고 기존 결과를 반환하며, 이미 끝난 변경의 상태/이전 버전을 새 변경처럼 검사하지 않습니다. 업무·배정/관리자 관계·감사·receipt는 같은 트랜잭션입니다. 클라이언트의 준비/역할/전달 사용자 ID는 최종 권한이 아닙니다. 업무/배정 버전 충돌과 상태 충돌은409, 현재 관리 권한 없음은403, 로그인 만료는401, 기능/필수 테이블 준비 문제는503으로 구분합니다. 버전/배정 상한은409로 차단합니다.

응답이 유실되면 기존 불변 본문·요청 키를 같은 접속의 workflow에 보관합니다. 행이 사라지거나 목록이 비어도 **관리 변경 결과 확인**으로 복구할 수 있습니다. 명시 결과 확인이 `not_recorded`일 때만 같은 키/본문의 재전송을 허용합니다. 자동 재전송·새 키 발급은 하지 않습니다. 로그아웃/401·계정 교체/창 폐기 뒤 늦은 응답은 이전 화면에 반영하지 않습니다. workflow는 디스크에 저장하지 않습니다.

관리자 인계로 본인을 제외한 뒤200을 받았다면 성공입니다. 이후 상세 조회403을 변경 실패로 취급하지 않습니다. 응답 유실 뒤403이면 성공/실패를 추정할 수 없으므로 본문/키를 보존하고 현재 관리자에게 확인하도록 안내합니다. 과거 권한으로 결과 조회를 우회하지 않습니다.

## 연장 입력 준비와 남은 연결

기존 DeadlineExtensionForm은 확정 정책에 따라 진행중·반려완료의 희망일·사유를 **로컬 확인만** 합니다. 현재 기한보다 뒤이고 현재 KST 날짜 이상인지 확인하며, 마감이 지난 반려완료도 원래 상태/기한을 바꾸지 않습니다. 별도 TaskLifecycleForm/API에는 신청/철회·승인/반려·기한 저장을 구현했습니다. 현재 담당자·대기 신청 하나·다른 지정 관리자의 판단·승인일 날짜/버전은 서버가 다시 검사하며1·3번 실제 연결 확인 전 변경503입니다.

취소와 연장은 [lifecycle 구현](TASK_LIFECYCLE.md)으로 모델·저장·조회/요청함·폼·취소 표시/취소 포함 조회·철회를 제공합니다. 1번의 공통 등록·009 검토 SQL과 쓰기off 후 성공 결과 읽기는PR51로 제공됐으며,PR52가 alpha `cbda025`에 병합돼3번 취소 guard·대기 연장 facts 소비 코드도 포함됐습니다. 실제 변경 허용 전 같은 배포의 스키마/옵션·수명·제출/판단/첨부 차단과 경합 검증·통합 ACK가 필요합니다. 공용 첨부는 취소 트랜잭션의 업무 참조 해제와 신규 등록의 선택 목록 연결까지 소비하며, 수정의 최종 목록 교체는 위 기술 조건을 기다립니다. PR60의 `013_shared_attachments.mysql.sql` 제공을 공유 DB 적용 완료로 보지 않습니다. 취소·연장 연결 확인 전503을 유지하며2번이3번 SubmissionGate나 검토자/결과물 모델을 대신 수정하지 않습니다. 3번 관리자 이력은 기존 업무 감사 읽기를 재사용하며 새 성공 이벤트 구독·알림·공유 환경의 완료와 구분합니다.

## 검증

- 서버: `RemoteWork.Server/tests/test_task_management.py` — 현재 권한·다섯 상태·KST·버전/배정·원자 rollback·중복/동시성·기본 off 검사.
- 선택 첨부 경계: `RemoteWork.Server/tests/test_task_creation_attachments.py` — 기존 첨부 생략 유지, 빈/값 배열 명시 시503·무효과, 이전 버전409 우선. [소유 MySQL 실행기의 `--suite attachments`](../Tests/TaskWeb.Tests/README.md#task-creation-attachment-checks)로 기존 등록/관리/API/웹 회귀와 함께 실행하며, 최종 목록 교체 성공 검사는 아닙니다.
- 웹: `RemoteWork.Server/tests/test_task_web_management.py`62개 — cookie/CSRF·현재 지정 관리자·5상태·취소·불변 초안 버전·후보·W1 복구·인계 후403·이력 보존·rollback. 이는 검사 소스의 항목 수이며 최종 실행 수치는 별도 기록합니다. [소유 MySQL 실행 명령](../Tests/TaskWeb.Tests/README.md#task-web-management-checks)을 따릅니다.
- 기존 결과물 웹 진입: `RemoteWork.Server/tests/test_task_web_results_navigation.py`16개 — 본인/취소/기능off·현재 관계·선택 힌트·새로고침. 기존3번 제출/첨부 통합 완료 검사가 아닙니다.
- 제한된 실제3번 소비: `RemoteWork.Server/tests/test_task_web_member3_integration.py`2개 — 업무 웹 수정 감사→기존3번 관리자 이력,실제3번 제출→이전 관리자 변경 확인폼의409·무효과. 로컬 개발 검토자·첨부 없는 소유 MySQL 범위이며 공유 조직·공용 첨부·통합 ACK 검사가 아닙니다.
- [실제 MySQL·HTTP·WinForms](../Tests/TaskManagement.Http.Tests/README.md) — 실제 수정/재배정/인계, 원본 배정/수락·과거 제출/첨부/판단 보존, 기존3번 API 경계 확인.
- [명시 대역의 관리 workflow](../Tests/TaskManagement.Tests/README.md) — 응답 유실/불변 요청/수명 검사. 실제 DB 검사와 구분합니다.
- [로컬 연장 정책](../Tests/TaskDeadlinePolicy.Tests/README.md)과 기존 `Tests/TaskInputSession.Tests` — KST 자정·상태·입력/접속 수명 검사. 연장 서버 완료 검사가 아닙니다.

실제 실행 수치와 미실행은 [LOG 구현·검증](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)에 기록합니다. 소유 테스트 DB/합성 계정 결과를 공유 계정·두 PC·수동 GUI/물리 장비 검증으로 확대하지 않습니다.
