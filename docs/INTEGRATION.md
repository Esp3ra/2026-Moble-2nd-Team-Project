# 팀 공통 서버 연결 규칙

팀원 1은 인증, 사용자, DB 연결, 공통 응답, 공통 이력, 자원 접근 검사 도구를 제공합니다. 팀원 2~5는 자신의 테이블·API·화면을 연결하고, 실제 업무·채팅방·파일의 접근 조건을 서비스에서 적용합니다. 현재 기능 API가 연결되지 않은 상태를 전체 업무 통합 완료로 취급하지 않습니다.

## 공통 주소와 응답

기본 주소는 `/api/v1`입니다. UTF-8 JSON, snake_case 필드명, UTC ISO 8601 시각(`2026-10-16T03:00:00Z`)을 사용합니다. 한국 시간 표시는 각 클라이언트에서 변환합니다. 내부 DB 시각도 UTC이며 `datetime.now()` 대신 `models.utcnow()`을 사용합니다.

```json
{"ok": true, "data": {"user": {"id": 1, "role": "employee"}}, "request_id": "요청번호"}
```

```json
{"ok": false, "error": {"code": "FORBIDDEN", "message": "접근 권한이 없습니다."}, "request_id": "요청번호"}
```

| 상태 | 의미 |
|---|---|
| 400 | 입력값 오류 |
| 401 | 로그인 필요·만료·폐기 |
| 403 | 로그인했지만 권한 없음 |
| 404 | 대상 없음 |
| 409 | 중복 아이디·사번 또는 데이터 충돌 |
| 413 / 415 | 요청 크기 초과 / JSON 필요 |
| 429 | 반복 로그인 실패 제한 |
| 503 | DB 연결·초기화 문제 |

클라이언트는 서버의 `error.message`를 보여주고 요청 번호로 문제를 확인합니다. 비밀번호·Authorization 헤더·접속 증표를 로그에 남기지 않습니다. 서버는 다른 출처에 대한 CORS를 기본 허용하지 않습니다. 웹은 같은 Flask 서버의 템플릿을 사용합니다.

## 제공 API

| 메서드 | 경로 | 권한 및 입력 |
|---|---|---|
| GET | `/health` | 공개, DB와 스키마 상태 |
| POST | `/auth/login` | 공개, `login_id`, `password`; `access_token`, `expires_at`, `user` 반환 |
| GET | `/auth/me` | 로그인, 실제 사용자 정보·접속 만료 |
| POST | `/auth/logout` | 로그인, 현재 접속 폐기 |
| POST | `/auth/password` | 로그인, `current_password`, `new_password`; 전체 접속 폐기 |
| GET | `/users?page=1&limit=20` | 관리자, 사용자 목록·총 개수; limit 최대 100 |
| POST | `/users` | 관리자, `login_id`, `password`, `display_name`, `employee_no`, `department`, `role` |
| PATCH | `/users/{id}` | 관리자, 이름·사번·부서·역할·활성 여부 변경 |
| POST | `/users/{id}/password` | 관리자, `new_password`; 전체 접속 폐기 |
| GET | `/audit-events` | 관리자, page·limit·resource_type·resource_id 필터 |

역할 값은 `admin`, `employee`입니다. 클라이언트에서 role·사용자 ID를 보내더라도 인증 근거로 사용하지 않습니다. API는 `Authorization: Bearer {access_token}`만으로 인증합니다. 브라우저 쿠키로 API 인증을 대신할 수 없습니다.

## 기능 모듈 추가

예를 들어 팀원 2는 `remotework/features/tasks/`에 모델과 라우트를 만들고 `features/registry.py`의 `BLUEPRINTS`에 등록합니다. 모델 파일도 그 모듈에서 import해야 최초 `init-db`가 테이블을 발견합니다. 팀원 3~5도 같은 방법을 사용합니다. 아직 구현되지 않은 기능을 자동으로 성공시키는 예시 API는 등록하지 않았습니다.

다음은 복사해 변형할 수 있는 연결 예시입니다. 실제 업무 테이블과 조회는 팀원 2가 구현합니다.

```python
from flask import Blueprint, g
from remotework.auth import login_required
from remotework.common import success, require_role, require_resource_access, audit, grant_resource
from remotework.db import get_db, transaction

bp = Blueprint('tasks', __name__, url_prefix='/api/v1/tasks')

@bp.get('/<int:task_id>')
@login_required
def detail(task_id):
    require_resource_access('task', task_id, allow_admin=False)
    # 이 검사 이후 실제 업무를 조회하고 공개 가능한 필드만 반환합니다.
    # grant가 없는 요청은 관리자도 403입니다. 배정·관리 범위 모델과 grant 동기화는 별도 구현·합의가 필요합니다.
    ...
```

관리자 전용 변경 라우트에는 `@require_role('admin')`을 사용합니다. 이 도우미에는 로그인 검사도 포함되어 있습니다. 직원의 승인 완료 변경은 팀원 2·3의 상태 전환 서비스에서 별도로 거부해야 합니다. 역할 검사만으로 업무 상태 규칙을 대신하지 않습니다.

## 자원 접근 권한

공통 `resource_grants`는 사용자별로 접근 가능한 업무·채팅방·파일의 연결을 보관합니다. `(user_id, resource_type, resource_id)`가 유일합니다. 공개된 권한 부여 API는 없으며 서버의 업무 서비스만 변경합니다.

```python
with transaction() as db:
    # 업무 담당자 저장, 채팅방 참여자 추가 등 실제 업무 변경
    grant_resource(db, assignee_id, 'task', task.id)
    audit('task.assigned', 'task', task.id, {'assignee_id': assignee_id})
```

- 담당자 변경·협업 탈퇴 시 기존 grant를 같은 트랜잭션에서 지우고 새 연결을 만듭니다.
- 채팅방은 관리자가 자동 열람하면 안 되는 정책이라면 `require_resource_access('chat', room_id, allow_admin=False)`를 사용합니다.
- 파일 다운로드는 먼저 파일의 존재와 연결된 업무·제출물을 확인하고 해당 자원의 권한을 검사합니다. 파일별 grant 또는 연결 업무의 grant 중 정책을 합의해서 일관되게 적용합니다. 추측한 파일명으로 저장 경로를 직접 여는 방식을 사용하지 않습니다.
- 이 공통 도우미의 허용·거절·권한 회수는 테스트했습니다. 실제 업무·채팅·파일 API에 적용하는 검증은 각 기능이 도착한 뒤 진행합니다.

## DB와 감사 이력

`get_db()`는 요청별 SQLAlchemy Session을 반환합니다. `transaction()`은 성공 시 commit, 오류 시 rollback합니다. 원시 SQL을 쓸 때는 반드시 바인딩 매개변수를 사용합니다. SQL 문자열에 사용자 입력을 합치지 않습니다.

`audit(action, resource_type, resource_id, details)`는 commit하지 않으므로 **업무 변경과 이력 저장을 하나의 트랜잭션**으로 묶습니다. 인증한 수행자는 `g.user.id`를 사용합니다. details에는 변경 전·후의 업무 값만 전달하며 비밀번호·접속 증표·원본 파일·민감한 장치 데이터를 넣지 않습니다. password/token/secret/credential 이름의 키는 공통 도우미에서 추가 마스킹합니다.

스키마 버전은 현재 1입니다. 이후 DB 구조 변경은 담당자끼리 조정하고 `migrations/`에 번호가 있는 SQL을 추가해 테스트 DB에서 검증하고 백업 후 공유 DB에 적용하세요. `init-db`는 존재하는 테이블의 열을 자동 수정하지 않습니다. 승인·반려의 action 명칭과 상세 구조는 팀원 2·3이 같은 값으로 합의하세요.

공통 요청 크기는 32KiB입니다. 팀원 3의 파일 업로드 라우트는 별도의 파일 크기·확장자·실제 콘텐츠 제한을 구현하고 해당 라우트의 `request.max_content_length`를 명시적으로 조정해야 합니다.

## C# 화면에서 호출

`AppServices.Api`가 공통 클라이언트입니다. 모든 기능 화면에서 이를 재사용하면 접속 증표와 오류 처리가 함께 적용됩니다. 외부에서 별도 HttpClient로 인증을 우회하지 않습니다.

```csharp
using RemoteWork.WinForms.Infrastructure;

try
{
    var page = await AppServices.Api.GetAsync<PageResult<UserInfo>>("users?page=1&limit=20");
    grid.DataSource = page.Items;
}
catch (ApiException ex)
{
    MessageBox.Show(this, ex.Message, "요청 확인");
}
catch (OperationCanceledException)
{
    // 호출자가 취소했거나 로그아웃·재로그인으로 이전 접속의 요청이 무효화됨.
}
```

API 경로에는 `/api/v1/`을 다시 붙이지 않습니다. 예시는 관리자 화면에서 실제 동작하는 사용자 조회이며 업무 조회 모델은 팀원 2가 추가합니다. 생성·변경은 `PostAsync<T>`, `PatchAsync<T>`를 사용합니다. 401이면 공통 메인 창이 로그인 화면으로 돌아갑니다. 자동 재시도로 중복 생성·제출을 만들지 않습니다.

웹은 `templates/base.html`을 상속하고 공통 로그인·메뉴·사용자 표시를 재사용합니다. POST 폼에는 `<input type="hidden" name="csrf_token" value="{{ csrf_token() }}">`를 넣으세요. `/api/` 이외의 모든 변경 요청에는 새 Blueprint를 포함해 공통 CSRF 검사가 자동 적용됩니다. 웹 인증에는 `rw_session` 쿠키를 사용합니다.

## 통합 확인 순서

1. 기능 담당자가 테이블·API·화면과 권한 조건을 완성합니다.
2. 테스트 DB에서 관리자 1명과 서로 다른 직원 2명으로 허용·거절을 확인합니다.
3. 권한 없는 업무, 비참여 채팅방, 다른 직원 파일의 직접 요청을 거절하는지 확인합니다. 공통 grant 거절은 403이며 업무 상세를 404로 숨길지는 별도 합의합니다.
4. 서버 재시작·로그아웃·접속 만료·DB 연결 실패를 확인합니다.
5. 두 PC와 웹에서 업무 배정 → 수락 → 제출 → 반려 → 재제출 → 승인을 시연합니다. 실제 장치는 팀원 5와 함께 확인합니다.

## 참고한 공식 문서

- [Flask 애플리케이션 팩토리](https://flask.palletsprojects.com/en/stable/patterns/appfactories/)
- [Flask 웹 보안과 쿠키·CSRF](https://flask.palletsprojects.com/en/stable/web-security/)
- [PyMySQL 연결 설정](https://pymysql.readthedocs.io/en/latest/modules/connections.html)

## 2번 조회 인터페이스와 연결할 때

alpha 기준 커밋 `8f09183ef992606ec4c2b7e3da79ef07983ba019`의 `ITaskQueryService`, DTO, `TaskQueryFailure`, 명시적 테스트 대역과 기존 테스트를 보존했습니다. 기본 `TasksControl`은 계속 `UnconfiguredTaskQueryService`를 사용합니다. 업무 목록·상세 API와 HTTP 어댑터는 이번 공통 코드에 등록하지 않았습니다.

- `ApiClient.ServerUri`와 `REMOTEWORK_API_URL`에는 `http://서버IP:5000/`처럼 서버 루트만 넣습니다. `/api/v1`을 붙인 주소는 현재 URL 검증에서 거절합니다.
- `GetAsync<T>("users?page=1&limit=20", cancellationToken)`처럼 `/api/v1/`이 없는 상대 경로를 넘기면 클라이언트가 접두사를 한 번 붙입니다.
- 후속 HTTP 조회 서비스의 생성자에 `AppServices.Api`를 전달하고 기존 `TasksControl`의 서비스 주입 생성자를 사용합니다. 공유 클라이언트를 기능 화면에서 Dispose하지 않습니다.
- 현재 `TaskSummary.TaskId`는 불투명 문자열, 시작일·마감일은 `DateOnly`입니다. 업무 PK·wire 필드·NULL·페이지·정렬·상태 매핑·날짜 의미는 아직 협의가 필요합니다. C# enum의 정수 순서나 이름을 DB/wire 코드로 확정하지 않습니다.
- `TaskQueryFailure`의 인증 만료·권한 없음·미존재·이용 불가 매핑은 실제 업무 API 계약과 함께 정합니다. 404 일반 경로 오류를 곧바로 업무 없음으로 표시하지 않습니다.
- 공통 `require_resource_access`의 기본 `allow_admin=True`는 관리자의 grant 검사를 생략합니다. 실제 업무 목록·상세에는 인증 사용자 기준의 별도 관리 범위 조건 또는 `allow_admin=False`와 올바른 grant 관리가 필요합니다. 이 도우미 하나만으로 Own/Managed 정책이 완성되지 않습니다.

실제 인증·공통 사용자 관리는 연결되었지만 업무 조회·저장·상태 변경·파일·제출·승인과 두 PC/웹 업무 통합은 별도입니다. 협의 항목은 팀 LOG 브랜치의 루트 `1번/`에 기록합니다.

## PR #2 수정: 관리자 변경과 접속 세대

공통 사용자 생성·수정·비밀번호 초기화는 `auth.admin_transaction()`을 사용합니다. `admin_guard` 잠금을 획득한 뒤 수행자와 접속을 `SELECT ... FOR UPDATE`로 다시 읽고 `populate_existing=True`로 기존 ORM 객체도 갱신합니다. 잠금 대기 중 접속이 폐기·만료되거나 계정이 비활성화됐으면 401 `AUTH_REQUIRED`, 접속은 유효하지만 관리자 역할이 아니면 403 `FORBIDDEN`을 반환합니다. 변경과 감사 이력은 함께 rollback하며 거절된 변경의 성공 이력을 남기지 않습니다. 관리자 수 검사는 이전 인증 조회의 스냅샷 COUNT를 사용하지 않고 현재 활성 관리자 ID를 잠금 조회합니다. 본인 강등·비활성화의 400 `SELF_LOCKOUT`과 최종 관리자 보호의 409 `LAST_ADMIN`은 유지합니다.

이 처리는 공통 사용자 관리의 동시성 보호입니다. 업무 Own/Managed·상위 검토자·파일 접근 정책을 대신하지 않으며 업무 관련 합의 항목은 그대로 남습니다.

C# `ApiClient`는 요청 시작 시 서버 주소·접속 세대·Bearer를 캡처합니다. 로그아웃·로그인 시작/완료·명시적 초기화·서버 변경 시 세대를 바꾸고 이전 요청에 취소를 전달합니다. 전송 계층이 취소를 무시해도 이전 응답은 `OperationCanceledException`으로 종료되며 `CurrentUser`·새 Bearer·만료 이벤트를 변경하지 않습니다. 화면은 호출자 취소와 같은 방법으로 이전 접속의 취소를 처리하고 오래된 결과를 다시 표시하지 않습니다.

현재 접속의 HTTP 401은 정상 오류 JSON·HTML·잘못된 JSON·빈 본문 모두에서 인증을 비우고 `AuthenticationExpired`를 한 번 발생시킵니다. JSON 파싱에 성공해야 만료되는 구조가 아닙니다. 같은 이전 접속의 후속 응답은 취소 처리합니다. 500·502·잘못된 성공 본문 등 401이 아닌 오류는 현재 접속을 임의로 해제하지 않습니다. timeout 안내와 호출자/접속 취소도 구분합니다.

로그아웃은 로컬 접속을 즉시 비운 뒤 캡처한 이전 Bearer로 서버 폐기를 요청합니다. 그 응답이 늦게 와도 이후 새 로그인을 지우지 않습니다. 서버 폐기 요청 실패 시 로컬 접속은 비워져 있으며 기존 서버 만료 시각은 그대로 적용됩니다.

회귀 검증은 [C# 접속 테스트](../Tests/ApiClient.Tests/README.md), 서버 `tests/test_admin_concurrency.py` 및 [PR 수정 검증 기록](PR2_수정검증_2026-10-16.md)에 정리합니다.
