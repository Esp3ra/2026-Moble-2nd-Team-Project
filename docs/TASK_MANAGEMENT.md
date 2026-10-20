# 업무 수정·재배정·관리자 변경

2026-10-20 팀장의 [A~E 확정 정책](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-management-policy-approved-20261020) 중 A/B/C의 2번 개발 구현입니다. 기존 공통 인증·현재 관계 검사·쓰기 guard·동일 요청 키·감사·성공 결과 보관을 재사용합니다. 다른 담당 제품 코드·공통 schema version·DDL은 변경하지 않습니다.

## 실행과 화면

서버에서 기존 업무 조회/공통 쓰기와 함께 `REMOTEWORK_ENABLE_TASK_MANAGEMENT_DRAFT=1`을 명시해야 합니다. 조회 옵션은 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`, 공통 쓰기 옵션은 기존 [WRITE_SUPPORT](WRITE_SUPPORT.md)의 실행 조건을 따릅니다. 신규 등록·수락·결과물의 옵션은 각 기능의 별도 조건입니다. 새 관리 옵션은 기본 off이고 off에서는 이 관리 모듈을 import하거나 관리 경로를 등록하지 않습니다. 옵션을 켜도 새 테이블을 만들거나 공유 DB에 migration을 적용하지 않습니다.

관리 업무 상세에서 **업무 수정 / 담당자 변경 / 관리자 변경**을 각각 엽니다. 최신 상세·write-context와 후보 선택을 확인한 뒤 별도 **변경 실행**을 누릅니다. 화면을 열거나 입력/대상을 확인하는 것만으로 저장하지 않습니다. 서버 성공을 확인한 뒤 현재 필터로 목록/상세를 다시 조회합니다. 현재 지정 관리자만 A/B/C를 실행할 수 있으며 관리자 역할만으로 타 업무를 수정하지 못합니다.

일반 수정과 재배정은 진행전만 허용합니다. 관리자 변경은 진행전·진행중·반려완료에서 허용하고 활성 관리자가 최소 한 명 남아야 합니다. 최초 등록과 달리 이후에는 등록자/본인도 다른 활성 관리자가 남으면 제외할 수 있습니다. 지정 관리자와 결과물 검토자는 서로 다른 관계입니다.

## 실제 HTTP 계약

모든 경로는 `/api/v1` 아래이며 Bearer·공통 envelope·`Idempotency-Key`를 사용합니다. 요청 본문에 표의 필드 외 값을 넣거나 JSON 키를 중복하면 거절합니다. `task_id`·`expected_row_version`은 양의 signed BIGINT 정규 문자열이고 `expected_assignment_revision`은 양의 signed INTEGER JSON 정수입니다. 사용자 ID도 정수입니다.

| 경로·공통 action | expected 두 필드 외 입력 | 허용 상태 |
|---|---|---|
| `PATCH /tasks/{task_id}` · `task.updated` | `title`, `description`, `start_date`, `due_date` | `not_started` |
| `POST /tasks/{task_id}/reassign` · `task.reassigned` | `assignee_id` | `not_started` |
| `PATCH /tasks/{task_id}/managers` · `task.managers_changed` | `manager_user_ids` 정수 배열 | `not_started`, `in_progress`, `returned` |

expected 두 필드는 `expected_row_version`, `expected_assignment_revision`입니다. 제목은 공백만 제외·Unicode 문자 200개 이내이고 본문은 빈 문자열을 허용합니다. 날짜는 `YYYY-MM-DD`/DB DATE이며 시작일≤마감일, 새 마감일≥수정 당일 KST입니다. 재배정 후보는 활성 직원/관리자, 관리자 후보는 활성 관리자입니다. 중복/빈 관리자 배열은 거절합니다. 동일 값은409 `TASK_CHANGE_NOT_NEEDED`이며 성공 receipt·감사를 만들지 않습니다.

성공200의 `data`는 다섯 필드만 반환합니다.

```json
{"task_id":"101","row_version":"2","assignment_revision":1,"task_status":"not_started","updated_at":"2026-10-20T01:00:00Z"}
```

매 성공은 row_version을 한 번 증가시킵니다. 실제 재배정만 assignment_revision도 한 번 증가시키고 새 배정의 accepted_at은 NULL입니다. 과거 배정을 보존하며 새 담당자가 명시적으로 수락해야 합니다. 수락/성공 제출 흔적이 있는 진행전 모순 자료는409 `TASK_MANAGEMENT_DATA_INCONSISTENT`로 차단하고 초기화하지 않습니다. 결과물 기능을 끈 2번 전용 DB에 제출 테이블이 없는 것은 허용합니다. 결과물 활성화 중 테이블 누락, 또는 활성화 여부와 관계없이 존재하는 테이블의 필수 열 부족은503으로 차단합니다.

## 권한·경합·결과 유실

서버는 공통 트랜잭션 안에서 인증된 현재 관리자 관계를 확인한 뒤 JSON 형태·필수 필드·타입/입력 형식을 검사합니다. 최초 변경에서는 업무/배정 버전→허용 상태→자료 보존·현재 후보/날짜 조건을 재검사합니다. 같은 요청의 성공 재조회는 현재 권한과 동일 본문을 확인하고 기존 결과를 반환하며, 이미 끝난 변경의 상태/이전 버전을 새 변경처럼 검사하지 않습니다. 업무·배정/관리자 관계·감사·receipt는 같은 트랜잭션입니다. 클라이언트의 준비/역할/전달 사용자 ID는 최종 권한이 아닙니다. 업무/배정 버전 충돌과 상태 충돌은409, 현재 관리 권한 없음은403, 로그인 만료는401, 기능/필수 테이블 준비 문제는503으로 구분합니다. 버전/배정 상한은409로 차단합니다.

응답이 유실되면 기존 불변 본문·요청 키를 같은 접속의 workflow에 보관합니다. 행이 사라지거나 목록이 비어도 **관리 변경 결과 확인**으로 복구할 수 있습니다. 명시 결과 확인이 `not_recorded`일 때만 같은 키/본문의 재전송을 허용합니다. 자동 재전송·새 키 발급은 하지 않습니다. 로그아웃/401·계정 교체/창 폐기 뒤 늦은 응답은 이전 화면에 반영하지 않습니다. workflow는 디스크에 저장하지 않습니다.

관리자 인계로 본인을 제외한 뒤200을 받았다면 성공입니다. 이후 상세 조회403을 변경 실패로 취급하지 않습니다. 응답 유실 뒤403이면 성공/실패를 추정할 수 없으므로 본문/키를 보존하고 현재 관리자에게 확인하도록 안내합니다. 과거 권한으로 결과 조회를 우회하지 않습니다.

## 연장 입력 준비와 남은 연결

기존 마감 연장 폼은 확정 정책에 따라 진행중·반려완료에서만 로컬 입력을 확인합니다. 현재 기한보다 뒤이고 현재 KST 날짜 이상인 희망일·사유를 확인하며, 마감이 지난 반려완료에서도 원래 상태·기한을 변경하지 않습니다. **신청/철회·승인/반려·기한 저장은 아직 연결하지 않았습니다.** 실제 서버에서는 현재 담당자·대기 신청 하나·다른 현재 관리자의 판단·승인일 날짜/버전을 다시 검사해야 합니다.

취소와 연장은 정책이 확정됐습니다. 실제 저장을 노출하기 전 1번의 모델/DDL·기능 준비 검사/action 등록, 3번의 취소·대기 신청 제출 차단/유효 기한 읽기를 함께 연결해야 합니다. 2번이3번 `SubmissionGate`나 검토자/결과물 모델을 직접 수정하지 않습니다. 취소의 별도 표시/취소 포함 조회·연장 요청함·철회, 공용 이벤트/알림·웹·공유 환경은 후속입니다. 새 감사 기록이3번 화면 이력의 새 이벤트 종류까지 연결됐다는 뜻은 아닙니다.

## 검증

- 서버: `RemoteWork.Server/tests/test_task_management.py` — 현재 권한·다섯 상태·KST·버전/배정·원자 rollback·중복/동시성·기본 off 검사.
- [실제 MySQL·HTTP·WinForms](../Tests/TaskManagement.Http.Tests/README.md) — 실제 수정/재배정/인계, 원본 배정/수락·과거 제출/첨부/판단 보존, 기존3번 API 경계 확인.
- [명시 대역의 관리 workflow](../Tests/TaskManagement.Tests/README.md) — 응답 유실/불변 요청/수명 검사. 실제 DB 검사와 구분합니다.
- [로컬 연장 정책](../Tests/TaskDeadlinePolicy.Tests/README.md)과 기존 `Tests/TaskInputSession.Tests` — KST 자정·상태·입력/접속 수명 검사. 연장 서버 완료 검사가 아닙니다.

실제 실행 수치와 미실행은 [LOG 구현·검증](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)에 기록합니다. 소유 테스트 DB/합성 계정 결과를 공유 계정·두 PC·수동 GUI/물리 장비 검증으로 확대하지 않습니다.
