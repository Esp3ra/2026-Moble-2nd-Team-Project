# 2번 업무 목록·상세 조회 개발 계약

<a id="own-deadline-current-facts"></a>
## PR54 후속 — 현재 본인 업무의 전체 사실 읽기

`remotework.features.tasks.deadline_facts`는 1·4번이 **명시적으로 호출할 내부 읽기 제공부**이다. HTTP 경로·wire DTO·옵션/extension 자동 등록·기한 사건/확인 저장소를 추가하지 않는다. 기존 `list_tasks`/단건 lifecycle의 본인·관리 합집합을 알림 대상의 증명으로 전용하지 않는다.

```python
from remotework.auth import authenticated_transaction
from remotework.features.tasks.deadline_facts import (
    read_own_deadline_facts, read_own_deadline_fact,
)

with authenticated_transaction() as db:
    facts = read_own_deadline_facts(db)  # 완전한 tuple, task_id 오름차순
    # 동일 TX의 서버 UTC/실제 사건·확인 처리는 1·4번 계약에서 연결한다.
    # 확인 실행 시에는 같은 확인 TX 안에서 정확한 대상 사실을 다시 읽는다.
    # current = read_own_deadline_fact(db, original_task_id)
```

- 호출자는 기존 공통 guard/현재 계정·접속 재검사를 수행하는 `authenticated_transaction`을 소유한다. 다른 session/일반 app context/인증 TX 밖 호출은 `RuntimeError`이다. 함수는 별도 TX·commit·자료 변경·감사/receipt를 만들지 않는다.
- 사용자 ID를 입력받지 않고 같은 TX의 `g.user`에 고정한다. **관리자도 본인에게 현재 배정된 업무만** 반환한다. 단건의 다른 사람/관리 관계만 있는 업무는403, 없는 업무는404이다.
- 각 frozen `TaskDeadlineFact`는 `task_id`, `title`, `assignee_id`, `assignment_revision`, `row_version`, `task_status`, `due_date`, `is_cancelled`를 복사한다. 내부 ID/버전은 signed DB 정수, 기한은 Python `date`, 취소는 bool이다. HTTP 직렬화가 필요하면 1번이 기존 BIGINT 정규 문자열/INTEGER 정수 계약을 적용한다. 단건 입력은 양의 DB int 또는 정규 BIGINT 문자열이다.
- **5상태와 취소 사실을 전부 반환**한다. 4번이 기존 A1의 진행전/진행중/반려완료·미취소 및 실제 사건/확인 상태를 소비한다. 제출완료/승인완료/취소도 현재 무효화 근거로 보존하며 여기서 날짜 창/팝업·확인 여부를 추정하지 않는다.
- current revision outer join의 locking read로 현재 본인 후보를 잡고, 기존 lifecycle `_current(mode="own")`의 현재 업무·배정·관리자·정렬 사용자 및 취소 locking read를 재사용한다. 호출자의 공통 guard를 사용하는 변경과 직렬화되는 범위이며 직접 DB 변경/다른 잠금 규칙까지 보증하지 않는다.
- 기본·최대 상한은 `MAX_OWN_DEADLINE_TASKS=1000`이다. `max_tasks`를 그 이하로 낮출 수 있지만 상한 초과는 **503 `TASK_DEADLINE_FACTS_LIMIT` 전체 실패**다. 페이지/조용한 잘림/부분 성공이 없다. 이는 보관·알림 운영 정책이 아닌 이 읽기 제공부의 자원 상한이며 더 큰 전체 조회 계약은 별도 대조한다.
- 현재 배정 누락으로 본인 소유인지 알 수 없는 업무가 있거나 사실 정합성이 깨지면 **503 `TASK_DEADLINE_FACTS_INCOMPLETE`**다. 모르는 자료를 정상 빈 목록으로 바꾸지 않는다. 취소/연장009 스키마가 없거나 잘못되면 기존 `TASK_LIFECYCLE_SCHEMA_REQUIRED`503이며, 빈 목록/옵션OFF에도 이를 검사한다. 미준비를 `is_cancelled=False`로 추정하거나 DDL을 복구하지 않는다. 정상적인 본인 업무0건만 `()`이다.
- invalid `max_tasks`는 내부 사용 오류 `ValueError`; 잘못된 단건 ID는 기존400이다. 실제 인증401·DB503은 공통 처리 경로를 유지한다.

**1·2·4번 기술 대조 필요:** 실제 기한 사건 identity/현재 사건 연결, 최초/기존 자료·감사 누락의 전환 기준, 재배정/중간 제출·복귀/취소 시 수신자·사건 무효화, 같은TX 서버UTC·계정 확인/W1·OFF 결과 읽기/90일 화면과 물리 보존. 이 제공부에는 `event_id`/`due_revision`/확인 상태/서버 기준시각을 만들지 않는다. 생성 감사에 최초 `due_date`가 없고 `updated_at`은 기한 외 변경에도 갱신되므로 사건 근거로 대신하지 않는다. 일반수정/승인 연장의 보존된 전후 기한은 원본 증거로만 재사용한다.

2026-10-22 새 `RemoteWork.Server/tests/test_task_deadline_facts.py`를 실제 소유 MySQL8.2의 원본002/004/006/009 DDL 위에서 실행해 **52통과·실패/오류/skip0**을 확인했다. 전체5상태/취소·관리자 own-only·읽기 전후 DB 전체 무변경·복사/불변성·2^53 초과 BIGINT·낮춘 상한2의 경계/초과·빈 목록과009 누락·orphan·잘못된 TX·오래된 RR/ORM 캐시 뒤 별도 guarded commit6건·현재 사용자/세션 무효화4건을 포함한다. DB·프로세스·임시 경로 정리를 확인했다. 시험 전용 HTTP route는 제품 경로가 아니다.

재실행은 삭제 가능한 전용 MySQL의 `MYSQL_TEST_URL`과 `PYTHONPATH=RemoteWork.Server`를 명시한 환경에서 `python -X utf8 -B -m pytest -q -p no:cacheprovider RemoteWork.Server/tests/test_task_deadline_facts.py`를 사용한다. 이 fixture는 `remote_work_test_` 이름의 전용 DB에 원본 DDL/시험자료를 만들고 비우므로 공유 DB를 지정하지 않는다. 소유 MySQL 실행기는 기존 `Tests/TaskQueries.Http.Tests/run_mysql_http_check.py`의 검증/정리 경로를 재사용해 별도 audit에서 실행했다. 기존5000 서버·.env·공유 DB·운영 옵션은 변경하지 않았다.

실제 시험·미실행/회신은 [LOG/2번 구현·검증](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)과 [PR54의 2번 통합 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305519634)에 취합한다. [4번 원 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6288083731)을 기준으로 하며 실제 마감 API/DB/source·확인 저장·초기자료 전환/공유 배포·두PC 완료를 뜻하지 않는다.

<a id="deadline-mutation-boundary"></a>
## PR54 후속 — 기한 변경 작성부와 같은 TX의 결합 위치

2026-10-22 [1번 14:06 갱신 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305220140)의 **사건 저장 제안에 대한 2번 원본 대조**이다. 현재 facts 제공은 완료됐지만 사건 저장 함수·등록 hook·HTTP API/DDL은 아직 제공/확정되지 않았다. 아래는 1번이 실제 저장부를 제공할 때 결합할 위치와 필요한 실패 계약이며, 이를 자동 활성화한 구현으로 읽지 않는다.

| 원본 경로/공개 실행 함수 | 실제 기한 확정과 결합 위치 |
|---|---|
| `tasks/management.py::execute_management(db, target=..., action="task.updated", body=..., key=...)` | `_update`가 기존 기한과 검증된 새 기한을 비교해 `changed_fields/before_due_date/due_date`를 만든다. `task.row_version` 증가와 `flush/refresh` 뒤부터 기존 `audit`·`WriteResult` 반환 전까지가 저장된 새 버전/기한을 대조할 같은 mutation 경계다. **전후 DATE가 실제로 다를 때만** 변경 근거가 된다. 본문·제목·시작일·첨부-only와 같은 기한 저장은 새 기한 변경이 아니다. |
| `tasks/lifecycle.py::execute_lifecycle(db, ..., action="task.deadline_extension_approved", extension_id=...)` | `_mutate`가 현재 pending/원기한/신청자·관리자·버전을 검사한 뒤 승인에서만 기한을 바꾼다. 기존 `before.due_date`와 flush/refresh한 현재 기한·배정·두 버전·승인된 extension을 같은 mutation 안에서 전달할 수 있다. 요청/반려/철회/취소가 기한 변경 사건을 만들지는 않는다. |
| `tasks/creation.py::execute_creation(db, body=..., key=...)` | 최초 ID는 task flush 뒤, 현재 배정/관리자는 뒤이은 flush 뒤에 확정된다. 현재 원본의 기한은 그 자리에서 읽을 수 있지만 기존 `task.created` 감사에는 최초 기한이 없다. **신규 생성이 최초 사건이 되는 계약과 기존 자료 baseline 전환은 별도**이며, 현재 사실 재조회로 과거 최초 기한/사건을 복원하지 않는다. |

**인증·잠금·W1 순서:** 호출자가 기존 `authenticated_transaction`의 같은 `get_db()` 세션을 소유한다. 공통 guard→현재 actor→현재 auth session 이후, 기존 관리 authorizer/lifecycle의 task→현재 assignment→manager 행/정렬 사용자→필요한 extension 행 잠금을 유지한다. W1은 현재 접근 재검사→기존 receipt 조회→미기록일 때만 mutation→최소 receipt 추가→호출자 commit 순서다. 실제 저장부는 이 같은 mutation 안에서 기존 원본 잠금 뒤에 결합해야 하며 별도 session/commit·TX 중 네트워크·after-commit 저장·요청 전후 별도 GET으로 대체하지 않는다. 새 사건/현재연결/확인 행의 추가 잠금 순서는 **1번 제공부와 공통 검증 후 확정**한다.

- 2번에서 전달 가능한 사실은 원 업무ID·원 action·현재 actor/assignee·assignment_revision·변경 전후 DATE/row_version·기존 mutation에서 잡은 서버 UTC와 승인 extension 식별이다. 이는 DB 내부 정수/date/datetime이며 최종 wire/UUID 필드 계약이 아니다. 사건 UUID·원 mutation 식별/중복 제약·현재 사건 연결은 1번 저장부가 명시해야 한다. 같은 TX에서 얻은 시각도 commit 완료 시각으로 주장하지 않는다.
- **W1 replay**는 원래 mutation을 실행하지 않으므로 사건도 다시 기록하면 안 된다. 같은 key에 다른 본문은 기존409다. 사건 기록/현재연결 갱신 실패나 후속 감사·receipt/commit 실패는 원 업무 변경까지 모두 rollback하고, 예외를 삼켜 업무만 성공시키지 않는다. A→B→A는 서로 다른 실제 mutation 두 건이며 두 사건을 만들 근거가 된다. 날짜·row_version hash나 감사 PK를 사건 UUID로 대체하지 않는다.
- 1번의 `UNIQUE(event,user,assignment_revision)` 보완안은 같은 계정 재배정 구분에 필요한 방향이다. 같은 배정에서 제출/반려·복귀한 기존 확인 유지, 옛 성공 결과의 현재 열람, 신규/기존 자료의 최초 사건과 명시 baseline 범위/시간·실행/백업은 아직 1·2·4번 대조/팀장 결정 항목이다. 이 문서가 이를 확정하거나 원본을 변경하지 않는다.
- **다음 제공물:** 1번은 실제 같은-session 사건 기록 함수의 입력/출력·미설치/실패/중복·현재연결/잠금 계약을 기존 PR54에 한 번으로 제공하고, 4번은 불투명 사건ID+배정세대·전체 snapshot/원래 성공 receipt 소비와 중간상태 차이를 답한다. 2번은 그 제공부에 두 실제 변경 경계를 연결하고 실제 MySQL 원자성/경합을 검증한다. 알림 source/확인 writer·운영 DDL/옵션은 이 단계에서 설치하지 않는다.

**이번 독립 준비/실행:** `RemoteWork.Server/tests/test_task_deadline_write_boundaries.py`를 새로 추가해 소유 MySQL8.2의 raw002/004/006/009에서 **17통과·실패/오류/skip0,36.96초**를 확인했다. 실제 creation/management/lifecycle/W1을 호출하고 시험에서만 기존 audit 경계에 `test.deadline_write_probe` 관찰 행을 같은 TX로 넣었다. A→B→A 두 변경·제목/본문/동일 기한/첨부-only 비변경·요청/반려/철회와 승인 구분·원키 replay1효과·creation/edit/approval의 probe 실패/receipt INSERT 후 실패6건 전체 rollback·실제 AdminGuard SQL 대기 뒤 최신 기한을 확인했다. 생성 감사에는 최초 기한이 없다는 근거도 유지한다.

시험 probe는 **실제 사건/UUID/저장부·등록 callback이 아니며**, 첨부-only는 명시 provider 대역이다. raw013·실제 파일/1번 신규 저장부·실제 확인 writer/운영 통합 검증으로 확대하지 않는다. 시험 자원과 소유 DB/프로세스/runtime 정리를 확인했다. 재실행은 앞 절과 같은 명시 환경의 **새 전용 DB**에서 `python -X utf8 -B -m pytest -q -p no:cacheprovider RemoteWork.Server/tests/test_task_deadline_write_boundaries.py`를 사용한다. 기존52건은 이전 현재facts 검사이며 이번17건과 합산하지 않는다.

<a id="deadline-state-evidence"></a>
## PR54 후속 — 상태·배정 원본과 사건 근거의 범위, 2026-10-22

[4번14:26 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6288083731)은 현재 facts의 실제 소비 완료와 다음 원본 근거 요청입니다.2번은 아래 기존 제품 경로를 대조합니다. 새 마감 사건 공급 함수·기록 hook·알림 API·확인 DB·baseline을 이 문서로 등록하지 않습니다. 담당별 다음 조건은 [기존 LOG 취합](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-deadline-boundary-reply-20261022)을 따릅니다.

| 원본 변화 | 확인할 현재 사실·보존 근거 | 사건/확인 정책과 구분할 점 |
|---|---|---|
| 신규 등록 | tasks의 현재 기한, 최초 assignment revision1, task.created 감사 | 현재 생성 감사에는 최초 due_date가 없습니다. 현재값으로 과거 최초 사건을 복원하지 않습니다. |
| 일반수정의 기한 변경 | task.updated의 changed_fields·before_due_date/due_date·전후 row_version | 실제 DATE 변화만 새 기한 변화의 근거입니다. A→B→A는 두 mutation이며 날짜·버전·감사PK를 사건UUID로 대신하지 않습니다. |
| 재배정 A→B→A | task_assignments의 보존된 세대1→2→3, current_assignment_revision, task.reassigned 전후 담당자 | 같은 사용자로 돌아와도 다른 배정입니다. 기한이 같아도 옛 계정 확인을 승계하지 않습니다. 배정 세대가 마감 사건ID는 아닙니다. |
| 수락·재작업 | task.accepted/task.rework_started 감사의 전후 상태·버전·배정, 최초 accepted_at | 수락은 진행전→진행중, 재작업은 반려완료→진행중이며 재작업은 최초 수락시각·기한을 보존합니다. 새 기한 변화로 추정하지 않습니다. |
| 제출·판단 | 실제3번 submission/decision/ResultEvent와 submission.* 감사의 task_id·round_no·최종 row_version | 진행중→제출완료→반려완료/승인완료. ResultEvent UUID는 결과물 사건이며 마감 사건ID가 아닙니다. 전후 업무버전·기한 전체 체인을 제공하는 reader로 전용하지 않습니다. |
| 관리자 변경 | task.managers_changed 감사·현재 manager 목록·row_version | 배정 차수/담당자·기한을 바꾸지 않습니다. 관리자 목록이나 row_version 증가만으로 재배정·새 기한 사건을 추정하지 않습니다. |
| 연장 신청·반려·철회 | task_deadline_extensions의 원기한/희망기한·상태와 해당 감사 | pending 등은 업무5상태와 별개이며 희망기한은 실효기한이 아닙니다. 승인 전/반려/철회는 현재 기한을 바꾸지 않습니다. |
| 연장 승인 | approved 행, 실제 task.due_date, 해당 감사의 before/after·버전 | 승인된 새 기한만 실제 변화입니다. 원본 writer TX 안의 사실이며 commit 완료 시각을 주장하지 않습니다. |
| 업무 취소 | task_cancellations의 사유·시각·현재 is_cancelled, pending 연장의 cancelled 종결 | 여섯 번째 업무상태가 아닙니다. 기존5상태·제출/판단 자료·기한은 보존하며 현재 알림 후보 무효화와 과거 확인/receipt 보존은 구분합니다. |

**입증의 한계:** 현재 facts는 현재 Task/Assignment/Cancellation을 잠금 조회할 뿐 과거 감사의 존재·완전성을 증명하지 않습니다. 자료 누락을 현재 due_date/updated_at으로 보정하거나 관리자용100건 페이지를 전체 같은TX 이력으로 사용하지 않습니다. 기존 `collaboration_continuity_reader`는3번 구간·누락·미지에서None을 유지합니다. 같은 배정의 반려→재작업 복귀에서 옛 확인을 유지할지, 검증된 구간만 지원할지 명시 baseline을 만들지는 아직 협의사항입니다.

**검사 근거:** 새 [상태·배정 근거 검사](../Tests/TaskDeadlineEvidence.Tests/README.md)는 실제2번·3번 경로와 소유 MySQL을 사용해 현재 facts/원본 기록을 대조합니다. 제출/판단 제품·공통/4번 구현은 수정하지 않습니다. 기한 사건 저장·알림 확인 동작을 구현하거나 검증한 결과로 확대하지 않습니다. 정확한 실행 수치와 실패/미실행은 해당 README와 LOG 구현검증에 기록합니다. 기존52개 facts 검사·17개 변경 경계 검사와 합산하지 않습니다.

<a id="captured-own-task-navigation"></a>
## 로그인에 고정된 내 업무 탐색 접점

2026-10-22 현재. 2번은 기존4번 `TaskRequested(string taskId)`에서 소비할 콜백을 제공하며, PR64의1번 MainForm이 로그인별 명시 탐색 조립을 제공합니다. 실제 알림 source·확인 저장·공유 환경 검증은 후속입니다.2번이 다른 담당 코드를 대신 수정하지 않습니다.

```csharp
// 살아 있는 업무 화면의 UI 스레드에서 로그인별로 캡처합니다.
Func<string, Task<bool>> navigate = tasksControl.CaptureOwnTaskNavigator();
// 실제 이벤트 연결은 1번 조립에서 수명/해제를 관리합니다.
bool opened = await navigate(taskId);
```

- 캡처는 HTTP 요청을 보내지 않습니다. 현재 `HttpTaskQueryService`의 공통 접속을 빌려 쓰며 토큰이나 쓰기 서비스를 외부에 넘기지 않습니다. 생성·호출은 해당 컨트롤의 UI 스레드에서 합니다. 비 UI 스레드 호출·화면 폐기·이전 로그인 콜백은 `false`로 끝나고 HTTP 요청을 시작하지 않습니다. 로그인 전/미설정 화면에서 캡처하면 예외입니다.
- 호출은 **내 업무 / 전체 상태 / 취소 제외**로 명시 이동합니다. 같은 캡처 접속으로 새 own 목록을 받은 후 정확한 ID의 행만 선택하고 기존 상세 GET을 실행합니다. 목록에 대상이 없으면 선택을 비우고 안내하며 첫 행을 대신 열거나 managed 범위로 확대하지 않습니다. 기존 일반 새로고침의 필터/선택 유지 동작은 그대로입니다.
- ID는 정규 양의 BIGINT 문자열이며 `9007199254740993`과 최댓값도 원문 문자열로 전송합니다. 잘못된 ID는 UI/HTTP를 변경하지 않고 `false`입니다. 기존 `ITaskQueryService`의 불투명 ID 규약을 변경한 것은 아닙니다.
- `true`는 같은 접속·현재 선택의 요청 업무 상세를 표시했다는 뜻입니다. 없음·403/404/503·취소·새 탐색/필터/행 선택·접속 변경 이후의 옛 응답은 성공으로 반환하지 않습니다. 401은 기존 공통 인증 만료 처리와 함께 화면을 무효화합니다. 오류 안내는 기존 조회 분류를 재사용합니다.
- 목록과 상세는 별도 GET입니다. 알림의 현재 본인 배정·현재 사건 재검사나 확인 저장의 원자성을 이 콜백으로 증명하지 않습니다. 작업 실행 권한은 서버가 실행 시 다시 검사합니다. 관리 업무의 결과물 읽기·명시 복귀는 [업무 웹](TASK_WEB.md), 일반수정 첨부 교체는 [업무 변경](TASK_MANAGEMENT.md#task-management-attachments-ui)대로 연결했습니다. 실제 알림 source와 공유 배포 검증은 후속입니다. MainForm의 명시 탐색 조립은 PR64에 제공됐고, 등록 첨부 연결 범위는 [등록 안내](TASK_CREATION.md)를 따릅니다.
- 선택 업무의 첨부는 기존 선택 흐름을 유지합니다. 첨부 응답이 지연돼도 성공한 업무 상세와 행동은 사용할 수 있고, 첨부 준비 오류만으로 업무 상세를 지우지 않습니다. 반환 작업은 기존 상세/첨부 조회가 종료된 뒤 완료됩니다.

검사 명령과 경계는 [HTTP/화면 검사 안내](../Tests/TaskQueries.Http.Tests/README.md)를 따릅니다. 실제 서버·공유 DB·알림 팝업/MainForm 조립 검증과 명시 HTTP 대역의 컨트롤 검증은 구분합니다.

<a id="task-attachments-and-continuity"></a>
<a id="task-attachment-read-consumer"></a>
<a id="task-continuity-reader"></a>
## 현재 첨부 읽기와 기존 협업 이력 접점 — 2026-10-21

업무 상세가 성공하면 기존 업무 행동과 새로고침을 먼저 사용할 수 있고, 선택적인 첨부 목록은 별도 로딩 상태를 유지합니다. 첨부503이 업무 상세 성공을 취소하지 않으며 현재401·로그아웃은 기존처럼 화면과 행동을 무효화합니다. 행을 바꾸면 새 상세가 성공하기 전까지 행동을 사용할 수 없습니다. [지연·선택 변경 회귀와 화면 점검](TASK_MANUAL_CHECK.md#task-independent-ui-check-20261021)을 확인합니다.

WinForms 상세의 첨부 목록/저장 버튼은 기존3번 `/api/v1/results/shared/tasks/{task_id}`와 파일 조각 GET을 캡처된 공통 로그인 세션으로 조회합니다. 빈 목록·취소·인증 만료·권한 없음·미준비·서버/형식 오류를 구분하고, 행 선택/재로그인 뒤 늦은 응답을 버립니다. UUID/이름·25MiB·최대10개·조각 순서/종료·전체 SHA256을 검사하고, 해당 업무의 현재 파일 목록을 전송 전후와 저장 직전에 다시 조회합니다. 검증 완료된 바이트만 새 파일로 저장하며 기존 파일은 덮지 않습니다. [소비 검사](../Tests/TaskAttachments.Tests/README.md)는 대역과 실제 연동을 구분합니다.

2026-10-21 팀장이 `attachment_ids` 선택 필드와 생략/빈 배열 의미를 수용했습니다. [등록 API/C# DTO와 전송 서비스](TASK_CREATION.md)는 제공됐으며, [일반수정 첨부](TASK_MANAGEMENT.md#task-management-attachments-ui)는 PR68의 공개 현재 목록/교체 접점을 같은 TX의 전후 집합 비교·감사·W1에 연결했습니다. 제공부·스키마 미준비일 때만 명시 첨부 변경을503으로 차단합니다. 웹 cookie/CSRF 어댑터와 WinForms 등록·수정의 파일 선택/명시 전송·삭제 화면도 연결했습니다. 취소 TX의 업무 첨부 참조 해제는 [lifecycle 계약](TASK_LIFECYCLE.md)을 따릅니다.

기존 QUERY+COMMON_WRITE opt-in에서 `collaboration_continuity_reader(db, task_id, since_version, current_version)`를 등록하되 기존 reader를 덮지 않습니다. 같은 현재 인증 트랜잭션/guard에서 Task·배정·AuditEvent를 현재 잠금 조회합니다. 완전한2번 단위 버전 체인과 상태/배정을 대조해 계속 진행중이면True, 완전한 체인에 중단이 증명되면False입니다. 누락·중복·미지 행동·3번 제출 구간·범위/2048행 상한·현재 버전 불일치는None입니다. 과거/현재 차수만으로 중간 제출·반려가 없었다고 추정하지 않습니다. 이 함수는 협업 현재 관계나 원업무·첨부 열람 권한을 부여하지 않으며4번 bridge의 기존 권한 검사도 유지합니다.


## 업무 웹 읽기 화면 — 2026-10-21

같은 `list_tasks`/`get_task`·현재 권한을 재사용하는 [업무 웹 목록·상세](TASK_WEB.md)를 제공합니다. 기존 조회 opt-in에서 `/tasks`·`/tasks/<id>`의 내/관리 업무·전체/5상태·취소 표시와 상세 복귀를 연결했습니다. query-only 서버는 읽기 전용이며,공통 쓰기와 등록/시작 옵션을 별도로 켠 서버에는 [등록·수락·재작업 시작](TASK_WEB.md#task-web-create-start)도 연결했습니다. 웹은 기존 cookie/CSRF,API는 Bearer 인증을 유지하며 API DTO·DB 계약은 바꾸지 않습니다. 공통 업무 메뉴는1번 PR59 병합본을 보존해 query on이면 `/tasks`로 이동하고 off면 준비 화면을 유지합니다. 최초 읽기 회차의31검사와 아래 날짜별 기록은 당시 검증이며,현재 실행 범위·결과와 미실행은 [전용 검증 안내](../Tests/TaskWeb.Tests/README.md)를 따릅니다.

후속으로 기존 lifecycle 옵션에서만 **웹 마감일 연장 신청함 조회**를 제공합니다. 기존 API 요청함의 현재 관계/배정·5처리 상태·정렬을 같은 읽기 함수로 재사용하고 날짜/사유와 업무 상세 링크를 표시합니다. 업무5상태와 연장5처리 상태를 구분하며 API DTO·쓰기·ACK는 변경하지 않습니다. [제공/실행 경계](TASK_WEB.md#후속-마감일-연장-신청함-읽기)를 확인합니다.

같은 옵션의 웹 상세에는 [취소·연장 대기 사실](TASK_WEB.md#task-web-current-lifecycle-detail)을 표시합니다. 인증 조회의 이전 MySQL snapshot과 현재 배정이 섞이지 않도록 업무/배정/사용자와 취소/대기를 같은 현재 잠금 읽기로 구성합니다. query-only GET·API DTO와 권한은 유지합니다. 별도 lifecycle 옵션의 취소·연장 웹 쓰기도 제공하지만 같은 배포의 통합 검증·ACK 전 실행503을 유지합니다.3번 소비 코드 제공과 공유 검증은 구분합니다.

<a id="captured-read-session"></a>
## 다른 담당자에게 제공하는 접속 고정 읽기 — 2026-10-20

4번의 F4-01 회신을 바탕으로, 새로운 정책 없이 제공할 수 있는 읽기 접점을 추가했습니다. 공통 로그인 완료 후 `HttpTaskQueryService.CaptureReadSession()`을 명시적으로 호출하면 공개 `TaskReadSession`을 받습니다. 이 객체는 `ITaskQueryService`와 읽기 전용 `IsCurrent`·`Cancellation`·`ThrowIfChanged()`만 제공하며 준비/등록/쓰기 서비스 인터페이스나 토큰을 제공하지 않습니다. 공통 클라이언트/접속의 소유권을 빌리며 직접 Dispose하지 않습니다.

기존 GET·DTO 변환·전체/5상태·취소 포함·날짜/오류 분류를 그대로 재사용합니다. source 설명과 실제 HTTP는 생성 당시 접속에 고정됩니다. 로그아웃·401·동일 계정 재로그인·서버 변경 뒤 이전 reader는 무효이며 새 로그인에서 명시적으로 다시 생성해야 합니다. 요청 전후 성공 경로의 호출자 취소·접속 현재성을 검사해 옛 성공 데이터를 반환하지 않습니다. 401은 인증 만료 분류를 유지하고403·503·형식/통신 실패를 빈 성공이나 권한 회수로 추정하지 않습니다.

**소비자는 성공과 오류 모두 화면에 적용하기 직전에 현재 접속/화면 수명을 다시 확인해야 합니다.** 이미 완료된 옛 접속의 오류가 새 로그인 뒤 continuation에서 전달될 수 있고, 마지막 검사 이후 UI 표시까지 경합도 남습니다. `Cancellation`은 취소 관찰용이며 공유 세션/클라이언트의 수명 제어권이 아닙니다. 서버는 매 GET의 권한을 최종 검사하고 로컬 `IsCurrent`가 현재 배정/관리자 관계를 증명하지 않습니다.

범위별 관계 확인은 해당 `own` 또는 `managed`의 성공한 현재 전체 목록을 기준으로 해야 합니다. 상세는 본인 또는 지정 관리자의 합집합 권한이므로 특정 scope의 관계 증명으로 재사용하지 않습니다. 기존 목록 DTO에는 버전/배정 revision·관계 ID·서버 시각이 없으며, 이 접점은 이를 새로 약속하거나 lifecycle 쓰기 문맥에 의존하지 않습니다. 추가 최소 읽기 정보와 보장 수준은 공통 담당과 별도 합의합니다.

타이머·후보 큐·알림 대상/시각/중복키·저장·협업 공개/파일 권한·MainForm 주입은 추가하지 않았습니다. 실제4번 서비스·서버/DB·공유 환경 연결도 완료가 아닙니다. [명시 HTTP 대역의 전용 검사](../Tests/TaskReadSession.Tests/README.md)는6시나리오92검사를 통과했고, 기존 조회 화면172·HTTP132검사도 통과했습니다. 명시 업무 대역은 기본 취소 제외와 `IncludeCancelled=true`의 포함을 전체/5상태·본인/관리 조건과 함께 지키도록 보완했습니다. 대역 필터는 서버 권한 검사가 아닙니다.

## 현재 쓰기 기준 — 2026-10-20

[팀장 확정 A~E와 실제 A/B/C 연결](TASK_MANAGEMENT.md)을 따릅니다. 일반 수정·재배정은 진행전, 관리자 변경은 진행전/진행중/반려완료에서 현재 지정 관리자가 실행합니다. [취소·연장](TASK_LIFECYCLE.md)은2번 모델/저장·조회/요청함·폼을 제공하며1번 공통·PR52의3번 차단 코드 제공과 별개로 같은 공유 배포의 통합 검증·ACK 확인 전 변경503입니다. 선택적 include_cancelled와 is_cancelled로 취소를 표시하고5상태를 유지합니다. 기존 GET 필수 필드·5필터·현재 조회 권한·페이지 없는 반환 계약을 유지하며 클라이언트에는 IncludeCancelled=false·IsCancelled=false의 호환 기본값을 추가했습니다. 아래의 날짜별 초기 회신/검증은 당시 기록이며 현재 관리/lifecycle 정책·미실행은 각 안내와 LOG 최신 기록을 우선합니다.

최신화: 2026-10-20 · **1번 수용 조회 계약 / 취소 표시의 선택적 확장 / 확정 관리 정책 / 정식 공유 적용은 별도**

1번의 [공통 연결 규칙](INTEGRATION.md)을 재사용한다. 관리 범위는 2026-10-17 사용자 회신에 따라 **업무별 지정 관리자만** 허용한다. 초기 조사에서 기존 업무 데이터·합의된 업무 API/DB가 없다는 사용자 회신을 기준으로 조회 초안을 준비했고, 이후 받은 1번의 수용 범위는 아래에 구분한다.

2026-10-17 [1번 M1-01~10 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/7b0c81588db667deeccc8ad67f3249090f0276de/1번/2026-10-17_업무조회_API_DB_규약확정_회신.md)으로 조회 API·DTO·권한·오류와 상태/날짜·FK·보존 기준을 수용했습니다. 이후2번 등록·시작·관리/lifecycle 쓰기는 각각 별도 개발 계약으로 연결했고 정책은2026-10-20 A~E를 따릅니다. 정식 DDL/공유 적용과 새 lifecycle의1·3번 기술 소비 통합은 별도입니다.3번의 제출·파일·검토·회차·이력 코드는2번이 대신 구현하지 않습니다.

## API

모든 경로는 `/api/v1` 아래이며 Bearer 인증이 필요하다. 성공·실패는 공통 envelope를 그대로 사용한다. 공유 적용 게이트 통과 전까지 서버의 **`REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1` 명시적 개발 활성화**가 필요하다. 기본값은 꺼짐이며 기본 서버의 업무 경로404는 화면에서 연결 필요로 표시한다. 이 등록 방식과 기본값도 1번이 수용했다.

새 프로세스에서 활성화하지 않으면 업무 Blueprint와 모델을 import하지 않는다. 따라서 일반 서버의 `--init` 또는 `init-db`가 이 초안 때문에 업무 테이블을 새로 만들지 않는다. 활성화와 DB 초기화는 삭제 가능한 개발·테스트 DB에서만 함께 사용한다. 이미 업무 모델을 import한 프로세스의 환경변수만 바꿔 초기화를 반복하는 방식은 사용하지 않는다.

| 요청 | data |
|---|---|
| `GET /tasks?scope=own` | `{"items":[요약...]}` |
| `GET /tasks?scope=managed` | `{"items":[요약...]}` |
| `GET /tasks/{task_id}` | `{"task":요약+description}` |

`scope` 생략 시 `own`이다. `status` 생략은 전체이며 지정하면 아래 코드로 서버에서 필터링한다. 빈 값·반복·잘못된 scope/status·지원하지 않는 쿼리 키는 400이다. 사용자 ID·역할 쿼리로 조회 권한을 확장할 수 없다.

첫 연결에서는 기존 `ITaskQueryService`의 전체 목록 반환 구조를 유지한다. 서버는 해당 범위·상태에 해당하는 **전체 목록**을 `due_date ASC, task_id ASC`로 반환한다. 페이지·검색 기능은 아직 제공하지 않으며 `page/limit`도 받지 않는다. 취소 포함은 `include_cancelled=true`를 명시하고 생략/false는 취소 업무를 제외한다. 취소는 is_cancelled 별도 표시이며 과거 상세는 현재 조회 권한을 검사한다. 첫 페이지만 받아 전체라고 표시하거나 일정 개수 뒤를 조용히 생략하지 않는다. 페이지를 도입할 때는 공통 `PageResult`·UI 표시·조회 경계부터 함께 합의한다.

아래 기존 요약 필드는 모두 필수입니다. 취소 저장소가 준비된 응답에는 bool is_cancelled를 추가하며, 호환 클라이언트는 이전 응답의 누락을 false로 해석합니다. include_cancelled=true/false는 선택 조건이고 기본은 취소 제외입니다.

```json
{"task_id":"101","title":"업무 제목","assignee_id":2,"assignee_name":"담당자","task_status":"in_progress","start_date":"2026-10-17","due_date":"2026-10-20"}
```

상세는 같은 필드에 문자열 `description`을 추가한다. `task_id`는 DB 식별자의 문자열 표현이며 화면은 불투명 키로 취급한다. `assignee_id`는 공통 사용자 정수 ID이다. 시작일·현재 마감일은 NULL 없는 `YYYY-MM-DD` / DB DATE / 화면 DateOnly로 개발한다. 기록 시각 UTC와 구분하고 UTC 자정으로 변환하지 않는다.

| 표시 | 1번 수용 DB/JSON 코드 |
|---|---|
| 진행전 | `not_started` |
| 진행중 | `in_progress` |
| 제출완료 | `submitted` |
| 승인완료 | `approved` |
| 반려완료 | `returned` |

5개 의미와 문자열 코드는 사용자 확정·1번 수용 기준이며 enum 정수 순서에 의존하지 않습니다. description은 누락/NULL을 거절하고 빈 문자열을 허용합니다. 조회 코드는 상태를 변경하지 않습니다. [신규 등록](TASK_CREATION.md)의 진행전/업무 버전1/배정1/accepted_at NULL과 [수락·재작업](TASK_START_PREPARATION.md)의 KST 시작일~마감일 당일 내 허용을 유지합니다. 관리 변경·취소/연장은 확정 A~E 정책을 따르고 새 취소/대기 연장 사실의3번 쓰기 소비 코드는 PR52에 반영됐으며, 같은 공유 배포의 통합 검증은 별도입니다.

## 권한

- `own`: 인증된 직원 또는 관리자의 **현재 본인 배정** 업무만 조회한다.
- `managed`: 인증 사용자가 현재 `admin`이고 해당 업무의 `task_managers`에 지정된 경우만 조회한다. 직원 요청은 403이다.
- 상세: 현재 담당자이거나 위 지정 관리자여야 한다. 목록과 별도로 검사하므로 직접 task ID를 입력해도 우회할 수 없다.
- 재배정 후 과거 담당자는 배정 이력만으로 조회할 수 없다. 관리자 역할이나 공통 grant 하나만으로 전체 업무를 허용하지 않는다.
- 협업 참여 관계·동일 부서·등록자·상위 검토자를 지정 관리자와 동일시하지 않는다.2026-10-21 B2에 따라 협업은 최소 공개의 별도 목록을 사용하고 own/managed·원업무/파일 권한을 자동 확대하지 않는다. 추가 읽기 경로/DTO·구현은4번과 기술 대조한다.

업무 존재·유효한 현재 배정 확인과 권한 검사는 서버에서 한다. 업무가 없으면 404 `TASK_NOT_FOUND`, 존재하지만 허용되지 않으면 403 `FORBIDDEN`을 반환한다. 미등록 경로 `HTTP_404`와도 구분한다. 1번은 현재 정책과 중복 ResourceGrant가 필요 없는 권한 원천을 수용했다. 팀장이 존재 은닉 정책을 요청하면 오류 코드·서버·화면·테스트를 함께 바꾸는 후속 계약으로 다룬다.

## 화면과 오류

### 선택 업무의 결과 제출 연결 — 2026-10-20

실제 HTTP의 내 업무에서 최신 상세 상태가 진행중이면 `결과 제출` 진입을 제공합니다. 직원·관리자 본인 모두 같은 기준이며 관리 업무의 타인 제출 진입은 숨깁니다. 진행전은 수락, 반려완료는 재작업 시작이 먼저이며 제출완료/승인완료는 이 버튼을 활성화하지 않습니다. 이 조건은 진입 안내이며 실제 권한·기한·검토자는3번 서버가 다시 검사합니다.

2번은 기존3번 `SubmissionForm`에 선택 업무ID와 같은 캡처 세션/조회 서비스를 전달합니다. 실제 저장 또는 결과 확인 성공의 `DialogResult.OK`이고 원래 접속/업무 화면이 살아 있을 때만 목록·상세를 GET 재조회합니다. 취소·접속 변경·화면 폐기 뒤에는 성공 후 재조회를 실행하지 않습니다. 기존 범위/5상태 필터는 유지하므로 제출 후 진행중 필터에 행을 다시 끼워 넣지 않습니다. 상태 변경 POST·자동 제출·별도 검토 대기 저장은2번에 추가하지 않습니다.

기존 `검토 요청`은 미정인 별도 요청이며 새 제출 진입과 구분합니다. 제출 결과가 불명확한 채 새로고침하여 서버의 제출완료를 받으면 새 버튼이 비활성화될 수 있습니다. 이때 **결과물 메뉴 → 제출 결과 확인**으로 기존 pending 요청을 복구합니다. 새 버튼을0행/모든 상태의 pending 복구 진입으로 표시하지 않습니다.

현재 MainForm은 메뉴 이동 시 이전 화면을 Dispose하고 업무 메뉴 복귀 때 새 TasksControl로 최신 GET을 수행합니다. 이 연결에서 MainForm·ResultsSaved 전역 구독·3번 폼/서버를 대신 변경하지 않았습니다. [반복 가능한 기존 제출 통합 검사](../Tests/TaskResults.Integration.Tests/README.md)는 유지합니다. 업무 수정/재배정·취소/연장 정책은 확정됐습니다. 새 lifecycle 차단의 3번 소비 코드는 병합됐으며 같은 배포/공유 환경 확인은 별도입니다. 공용 성공 이벤트 payload/구독·기한 알림의 실제 source와 일반수정 첨부 교체는 후속입니다.

### 로그인과 화면 수명

실제 HTTP 업무 화면은 명시적 목록 재조회마다 TaskQuerySession으로 같은 공통 캡처 세션의 조회·준비·등록 workflow를 연결합니다. 시작·관리/lifecycle도 각 captured session 수명을 따릅니다. 목록/상세는 별도 FeatureUiScope operation과 취소 토큰을 사용하고 HTTP 완료·DTO 변환·UI await 복귀 뒤 현재성을 검사합니다. 공통 ApiClient·세션 store는 빌린 자원이며 화면이 Dispose하지 않습니다. 기존 공개 생성자·ITaskQueryService 호출과 필수 DTO 필드는 유지하고 취소 조회/표시에 선택적 호환 필드를 추가했습니다.

로그아웃·401·동일/다른 계정 재로그인·서버 교체 때 이전 목록·상세·선택/입력 대상은 UI 큐에서 폐기한다. 진행 중 요청이 없어도 정리하며, 이미 HTTP가 끝나 UI 복귀를 기다리는 결과도 폐기한다. 이전 접속의 정리 callback은 새 접속으로 재조회한 화면을 지우지 않는다. `SelectionChanged` 중 행 삭제는 현재 셀 전환이 끝난 뒤 수행한다. 닫힌 화면에는 반영하지 않는다.

새 로그인에서 `다시 조회`/F5를 누르면 목록·상세와 준비/등록 세션을 함께 캡처한다. 다른 접속의 이전 선택을 복원하지 않고 현재 역할의 본인/관리 범위를 다시 사용한다. 같은 접속에서 같은 조건으로 재조회할 때는 기존 선택 복원 규칙을 유지한다. `auth/me`의 정상 갱신 자체는 로그아웃으로 취급하지 않으며, 서버의 현재 권한 검사가 최종 기준이다. 입력 확인이나 재조회가 자동 저장/수락을 실행하지 않는다.

4번 회신의 assignment_revision은 signed INTEGER/JSON 정수이며 task_id·row_version의 BIGINT 문자열과 구분합니다.2026-10-21 알림 A1~A6·협업 B1~B7 진행 기준을 채택했으며,새 due_revision/사건 식별·관계/서버 시각의 DTO와 API/DB·실제 구현은 기술 대조 대상입니다.5번 장치/근태는 업무 조회·등록·수락의 추가 조건으로 연결하지 않습니다. 수정/재배정·취소/연장 정책은 확정됐고,3번의 취소/연장 대기 차단과 공용 파일 제공부,2번의 파일 참조·등록/일반수정 소비는 연결됐고 공유 환경의 적용·검증은 별도입니다. 실제 알림 사건·확인 저장 계약은 기존 업무 감사/결과물 이력과 구분합니다.

목록과 상세는 서로 다른 요청 시점의 결과다. 선택한 같은 ID의 최신 상세 요약이 목록 행의 제목·담당자 표시명·상태·시작일·마감일과 다르면 최신 상세는 정상 표시하고 목록 정보 변경 안내 및 `목록 새로고침` 버튼을 제공한다. 목록 행·선택·필터를 임의로 수정하지 않는다. 버튼은 기존 목록 전체 재조회로 연결하며 동일 조건의 선택 복원 규칙을 유지한다. F5도 목록 전체 재조회다. 새 목록과 상세가 같아지면 일반 조회 완료 안내로 돌아간다. 상태 필터 목록 조회 뒤 상세 상태가 바뀐 경우도 서버 오류로 취급하지 않는다.

이 안내는 화면에 표시할 요약값의 비교이며 row_version 검사·쓰기 경합 방지·서버 변경 이력 조회가 아니다. 상세 본문만 바뀐 경우는 목록에 비교할 본문이 없으므로 변경 안내의 대상이 아니다.

실제 로그인한 MainForm만 공유 `AppServices.Api`를 받은 `HttpTaskQueryService`를 주입한다. Designer·미리보기와 기본 TasksControl 생성자는 미구성 서비스를 유지한다. 출처 표시와 기존 행 선택·전체/5상태·로딩·빈 목록·오류·재시도를 재사용하며 실패 시 샘플로 대체하지 않는다.

401은 공통 인증 만료 처리에 맡기고 조회 오류도 인증 만료로 매핑한다. 403은 권한 없음, `TASK_NOT_FOUND` 상세404만 업무 없음이다. 미등록 경로 `HTTP_404`는 연결 필요이며 빈 목록으로 처리하지 않는다. 서버/DB/통신/형식 오류는 이용 불가, 호출자·접속 변경 취소는 취소로 전달한다. 공통 클라이언트를 개별 화면에서 Dispose하거나 메시지 문자열을 파싱하지 않는다.

같은 내 업무/관리 업무·상태 조건에서 목록을 다시 조회하면 현재 선택 ID가 새 목록에 있을 때 유지하고, 사라졌으면 첫 행을 선택한다. 범위/상태 조건을 바꾸면 선택을 초기화한다. 상세 조회 실패 시 `상세 재시도`는 선택한 업무의 상세만 요청하며, 성공 후에는 전체 목록 다시 조회로 돌아간다. F5는 자식 입력 컨트롤에 포커스가 있어도 전체 목록을 다시 조회한다.

접속 변경 등 공통 클라이언트의 취소가 화면 자체 토큰 취소와 별도로 전달되면 이전 목록/상세를 비우고 조회 취소로 안내한다. 이미 교체된 조회·선택·종료의 늦은 취소는 새 화면을 바꾸지 않는다. 실제401은 기존 인증 만료 처리와 구분한다.

알 수 없는 상태, 누락/NULL 필수 필드, 빈/중복 ID, 잘못된 날짜, 날짜 역전, 상세 ID 불일치는 성공 결과로 표시하지 않습니다. 목록에서 요청한 상태와 다른 항목이 있으면 전체 응답을 이용 불가로 처리하고 부분 성공·로컬 재필터링으로 숨기지 않습니다. 상세는 목록 이후 상태가 달라질 수 있으므로 이전 상태와 다르다는 이유만으로 거절하지 않습니다. 현재 상세는 [입력/대상 준비](TASK_INPUT_PREPARATION.md)와 별도 시작·관리/lifecycle 실행창에 전달합니다. 각 개발 옵션·현재 권한/상태에 따라 명시 실행을 제공하고 취소된 업무의 새 쓰기를 막습니다. 취소·연장 변경은 같은 배포의 통합 검증·ACK 확인 전503입니다. 첨부 읽기는 위 기존 shared 계약을 소비하고, 등록 첨부는 별도 확정 계약을 따릅니다. 일반수정 첨부 교체는 확정된 선택 필드·공개 T3 접점으로 연결했고 별도 검토 요청은 협의사항으로 유지하며 보관된 시작·관리/lifecycle 요청의 결과 확인은 선택 행 없이도 가능합니다.

## DB와 적용 경계

공통 Base를 사용한 `tasks`, `task_assignments`, `task_managers`의 읽기 모델이다. 현재 담당자는 `(task_id, current_assignment_revision)`과 배정 `(task_id, revision)`을 함께 연결해 판정한다. 담당자·배정자·관리자 FK는 공통 `users.id`를 참조한다. 지정 관리자는 조회 관계이며 3번의 검토자 계층이 아니다.

명시적으로 활성화한 개발 프로세스에서는 공통 init-db가 import된 업무 모델의 새 테이블을 발견할 수 있지만 기존 열 수정·정식 migration·권한 이관을 대신하지 않습니다. 기본 실행에서는 업무 모델을 등록하지 않습니다. 사용자 FK는 signed INTEGER, 업무 ID/FK는 signed BIGINT이며 일괄 cascade 삭제를 추가하지 않습니다. 등록·시작·관리/lifecycle은 현재 부모 업무 잠금·버전/배정 검사·같은 요청 결과 복구를 각 계약대로 구현합니다. 기존 제출 FK/포인터와 정식 DDL·공유 적용은 해당 소유자 확인을 따르며, row_version 존재나 조회 성공만으로 전체 동시성·공유 통합 완료를 판단하지 않습니다.

1번이 **`002_task_queries.mysql.sql`을 예약**했다. [SQL](../RemoteWork.Server/migrations/002_task_queries.mysql.sql)과 [데이터 영향·검증·복구안](../RemoteWork.Server/migrations/002_task_queries.md)이 있고 새 개발 DB의 DDL은1번 기술 수용을 받았다. 정식 migration·공유 적용 승인은 별도다. **공통 schema version은1을 유지**하며 실제/요구 버전 비교는 기존 공통 지원을 재사용한다. 버전1 기록이나 health 성공만으로 업무 테이블 준비를 판단하지 않는다. 버전2 행만 추가하거나 공통 검사를 대신 구현하지 않는다. 공유 적용은 대상/실행자·전역 버전·백업/복구·팀장 일정 확인 후 진행한다. 일반 실행에 업무를 자동 seed하지 않는다. 신규 등록과 최초 관리자 지정은 별도 기본off [등록 API와 기존 화면](TASK_CREATION.md)에 연결돼 있다.

테스트 DB에 주입한 업무는 명시적 fixture이다. 실제 SQL/HTTP 검증과 운영 업무 데이터 연결·공유 서버·두 PC·웹 검증을 구분한다.

## 환경 선행 조건 점검

2026-10-19 팀장 확인: **3번과 함께 사용할 공유 검증 서버 및 직원·관리자 계정은 아직 준비 전**이다. [3번 실행 환경 요청에 대한 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#3번-실행-환경-요청-회신)을 따른다. 실제 주소·가동 시간과 계정은1번/서버 운영 담당자가 준비한 뒤 비공개로 전달한다.

서버 운영 담당자는 [기존 SETUP](SETUP.md)의 Python·공통 설정과 승인된 MySQL 개발 DB를 준비한 뒤 다음 명령을 실행할 수 있다. 명령은 기존 설정을 읽으며 설정 파일·환경변수·DB를 만들거나 수정하지 않는다. 준비되지 않은 DB에 `init-db`나002 SQL을 임의로 실행하는 대체 절차가 아니다.

```powershell
cd .\RemoteWork.Server
.\.venv\Scripts\python.exe -m remotework.features.tasks.readiness
```

승인된 개발 프로세스의 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`이 필요하다. 기본off이거나 이 프로세스의 GET 등록이 없으면 DB에 접속하지 않는다. MySQL만 점검하며 SQLite DEMO 관리 기능은 추가하지 않았다. 점검은 다음 범위의 SELECT·메타데이터 조회만 수행한다.

- 현재 명령 프로세스의 조회 flag와 목록/상세 GET 등록, MySQL 여부.
- 조회에 필요한 공통/업무 테이블·열의 존재와 기존 공통 버전1 기록을 각각 확인.
- 업무 수, 현재 배정 누락, 배정·관리자의 업무/사용자 참조 누락 건수. 업무0건은 유효하며 샘플을 생성하지 않음.

표준 출력은 `status`, `code`, `message`, `checks`, `not_checked`가 있는 JSON 한 줄이다. 조건에 따라 `missing_tables`, `missing_columns`, `counts`를 추가한다. 고정된 테이블/열 이름·진위·건수만 출력하며 계정/업무 ID·내용·연결값·원본 오류를 출력하지 않는다. 성공은 종료값0, 준비 필요/실패/중단은1이다.

| code | 의미·후속 |
|---|---|
| `TASK_QUERY_LOCAL_PREREQUISITES_OK` | 이 프로세스의 제한된 읽기 선행 조건 확인. 업무0건도 가능 |
| `TASK_QUERY_DISABLED` / `TASK_QUERY_ROUTES_REQUIRED` | 개발 활성화/GET 등록 확인. DB 미접속 |
| `MYSQL_REQUIRED` | MySQL 개발 설정 필요. DB 미접속 |
| `TASK_QUERY_SCHEMA_REQUIRED` / `COMMON_SCHEMA_VERSION_REQUIRED` | 테이블/열 또는 공통 버전 기록 준비 필요.1번과 적용 조건 확인 |
| `TASK_QUERY_RELATIONS_INVALID` | 현재 배정/참조 누락. 자동 보정 없이 담당자 확인 |
| `TASK_QUERY_DATABASE_UNAVAILABLE` / `TASK_QUERY_CONFIGURATION_ERROR` | DB 조회 권한/접속 또는 기존 공통 설정·의존성 확인 |
| `TASK_QUERY_CHECK_CANCELLED` | 중단. 준비 완료로 처리하지 않음 |

**성공해도 전체 DDL 호환성(타입·NULL·PK/index/FK/CHECK·엔진/collation), 실행 중 서버와 설정 일치·HTTP, 실제 로그인·본인/지정 관리자 권한·계정별 기대 업무, 공유 적용 승인/백업·두 PC, 3번 결과물 화면 연결은 확인하지 않는다.** `not_checked`에도 이 제한을 항상 포함한다. 테이블 생성·seed·자료 보정·업무 상태 전환·로그인·서버 시작을 하지 않는다. 스키마 검토와 실제 계정의 HTTP/화면 검증을 별도로 이어가야 한다.

통제 검사는 서버 폴더에서 `python -m pytest -q tests/test_task_query_readiness.py`로 실행한다. 새27개 검사는 실제 ORM 조회의 전체 SELECT 열 포함·누락 조기 거절·무접속 경계·오류 숨김·metadata 미등록을 확인한다. 별도 소유 MySQL8.2 검증은 정상/빈 목록·현재 배정 단절·기능off·공통 버전/업무 테이블/JSON에 없는 accepted_at을 포함한 필수 열 누락8가지에서 명령 전후 행·DDL·버전이 동일함을 확인했다. 검증 fixture의 DDL/합성 자료 준비는 점검 명령과 구분하며 공유 서버 실행 증거로 사용하지 않는다.

## 재현 가능한 실제 HTTP 검증

[MySQL 통합 실행기 안내](../Tests/TaskQueries.Http.Tests/README.md)의 `run_mysql_http_check.py`는 명시적인 `--allow-disposable-mysql`·MySQL 설치 경로로 새 MySQL 프로세스/datadir·고유DB·합성 사용자·loopback API를 준비한다. 공통7테이블과 검토용002 실제 SQL을 사용하며 공통 버전1은 유지한다. 실제 서버의 datadir·포트를 생성한 인스턴스와 대조한 뒤 자기 DB만 다룬다. 기존 DB URL이나 `.env`를 연결 정보로 사용하지 않는다.

기존 직원/관리자의 C# HTTP·WinForms 검사로 정확한 허용 ID 집합·정렬·전체/5상태·행 상세·수락/수정/연장 준비 창·접속 폐기를 검증한다. 추가 실제 HTTP 검사는 빈 목록·401/403/404·지정 관리자 회수·재배정 후 현재 조회 범위를 다룬다. 업무/공통 행은 PK 순서의 해시로 비교하며 권한 변경은 fixture 내부 준비 코드에서만 수행한다. 제품의 업무 쓰기 API가 아니다.

일반 초기화·API·DTO·권한·공통 버전1·기본 off는 유지한다. 검증용 서버/DB·사용자는 격리된 임시 데이터이며 정식 DDL/버전 지원·공유 DB·두 PC·웹·3번 제출/검토 완료로 표시하지 않는다. 이전 `run_http_check.py`는 DEMO용 SQLite 이력이며 SQLite의 별도 제품 기능/스키마 관리는 추가하지 않는다.

## 남은 합의

| 항목 | 상태 |
|---|---|
| 업무별 지정 관리자만 관리 조회 | 사용자 확정, 서버에서 같은 조건 검사 |
| 협업 참여 업무의 조회 | 2026-10-21 B2 채택: 별도 협업 목록을 제공하고 own/managed에 자동 편입하지 않음. 현재 원업무/파일 권한을 자동 확대하지 않으며 추가 읽기 계약·기능은 후속 |
| 경로·DTO·필수/NULL·상태 문자열·DATE/DateOnly 형식·상세403/404 | 1번 회신 수용, 기존 조회 유지 |
| 초기 상태·수락/재작업 날짜·후속 상태 저장 | 등록 진행전/1/1/acceptedNULL은1번 수용. 시작 KST 기간과 관리·취소·연장 A~E 정책은 팀장 확정·2번 구현. 새 lifecycle 실제 변경은 같은 배포의 통합 검증·ACK 확인 전503 |
| 사용자 INTEGER·업무 BIGINT FK·공통 Base/트랜잭션·이력 보존 | 1번 회신 수용. 각2번 개발 쓰기는 잠금·버전·같은 TX/복구를 구현. 3번 새 lifecycle 소비 코드는 제공됐으며 정식 DDL·같은 배포의 경합/공유 검증은 별도 |
| migration 번호·DDL·schema version·공유 DB 적용 | 002 예약·DDL 초안 준비. 정식 DDL·1번 버전 지원·공유 적용은 별도 확인 필요 |
| 전체 목록·due_date/task_id 정렬·미지원 조건400 | 1번 회신 수용. 페이지·검색 도입은 후속 공동 결정 |
| 상위 검토자·반려 후 복귀·제출 상태 갱신 | 반려완료→명시적 재작업 시작→진행중→재제출 성공→제출완료 확정. 기존3번 제출 폼/API 연결 존재. 검토자 미확정 정책·새 lifecycle 소비·공용 이벤트/통지는 별도 |
| 자동 검토 요청·마감 후 직접 제출 | 질문으로 유지. 자동 결과 제출은 제외 |
| 공유 서버·실제 업무·두 PC·웹 | 공유 서버/계정 준비 전(팀장 확인). 기존 개발 등록 UI/API로 실제 업무를 명시 등록한 뒤 계정별 HTTP·화면 검증 필요 |

확인한 테스트와 구현 기록은 [LOG/2번](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG/2번)에 남긴다. 공통 규칙은 INTEGRATION, 조회 동작은 이 문서로 확인한다.

다른 담당의 수정·연결은 [2번 영역 수정 협업 규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_2번_영역수정_협업규약.md)을 따른다. 이 문서의 계약을 바꾸면 서버·클라이언트·테스트·문서를 함께 갱신하고 영향 담당의 공동 확인을 기록한다.
