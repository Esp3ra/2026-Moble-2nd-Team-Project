# 팀 공통 서버 연결 규칙

## 2026-10-21 추가: 마감 알림의 로그인 수명과 내 업무 탐색

`Shell/MainForm.DeadlineAlerts.cs::ConnectDeadlineAlertsAsync`는 **실제 서버 source가 제공된 뒤**
표시된 로그인 화면의 UI 스레드에서 한 번 호출하는 명시적 조립 접점입니다.
factory에는 원래 `ApiClient.Session`을 전달합니다. 아직 실제 source/API가 없으므로 기본
MainForm 시작에서는 호출하지 않으며 가짜 사건·새 endpoint·폴링 설정을 설치하지 않습니다.

`Shell/LoginDeadlineNotifications.cs`는 기존 `DeadlineAlertScope`를 로그인당 하나 소유합니다.
source와 공통 캡처 접속의 취소를 결합하고, 비동기 응답 전후 현재 접속을 검사합니다.
원본 source/공유 ApiClient는 빌리며 Dispose하지 않습니다. 로그아웃·401·같은 계정 재로그인·
서버 전환·owner 종료는 기존 수신/탐색을 끝냅니다. 메뉴는 기존 owner chain의 scope를 빌립니다.

`TaskRequested(string taskId)`가 발생하면 MainForm이 실제 업무 화면을 활성화하고 살아 있는
`TasksControl.CaptureOwnTaskNavigator()`를 UI 스레드에서 캡처합니다. 2번 제공부가 새
own/전체/취소제외 목록과 해당 ID 상세를 조회합니다. 동기 이벤트 발행을 표시 성공으로
간주하지 않으며 `LastNavigationSucceeded`는 navigator의 비동기 결과 후에만 정합니다.
false는 미존재·조회 실패·취소·화면 변경 등을 포함하므로 권한 회수로 단정하지 않습니다.
탐색은 알림 확인 저장·업무 상태 변경·새 권한 부여를 실행하지 않습니다.

### 실제 source 연결 전에 남은 기술 계약

- 기존 `IDeadlineAlertSource.RecheckAsync`의 반환값은 UI가 **전체 교체하는 snapshot**입니다.
  단건 GET 결과 하나를 전체 snapshot으로 감싸지 않습니다. 단건 API를 채택하면 source가
  무효화/삭제를 처리한 뒤 권위 있는 전체 현재 목록을 다시 받아야 합니다. 실패503은 빈 목록이 아닙니다.
- W1 확인 receipt는 저장 성공 증거이며 현재 목록이 아닙니다. 성공 뒤 목록 조회가 실패해도
  미기록/실패·새 키 POST로 바꾸지 않습니다. 4번은 source/port에 **확인 성공과 선택적 목록 갱신**을
  분리하는 보완을 자기 범위에서 대조해야 합니다. 현재 `DeadlineConfirmation(State, Snapshot)`의
  필수 snapshot 계약을 바꾸거나 가짜 최신 목록을 반환하는 구현은 이번 공통 변경에 없습니다.
- 실제 기한 사건/최초 자료·현재 사건 판별은 2번과 대조합니다. 제한된 협업 연속성 reader와
  새 업무 navigator는 이미 제공됐지만 알림 전체 사건 원본을 대신하지 않습니다. 사건/확인 DB·
  실제 API/DTO·DDL 번호/운영 설정은 별도 합의·검증 전입니다.

검증: `dotnet run --project Tests/DeadlineNavigation.Tests -c Release`는 실제 MainForm·TasksControl과
합성 loopback HTTP/source로 큰 ID·없는 업무·503·재로그인·늦은 응답·종료를 검사합니다.
제품 서버·MySQL·실제 알림 source/확인 저장·두 PC 시험을 뜻하지 않습니다.

팀원 1은 인증, 사용자, DB 연결, 공통 응답, 공통 이력, 자원 접근 검사 도구를 제공합니다. 팀원 2~5는 자신의 테이블·API·화면을 연결하고, 실제 업무·채팅방·파일의 접근 조건을 서비스에서 적용합니다. 담당별 개발 API가 추가되어도 제출·검토·장치와 원격 환경을 포함한 전체 업무 통합 완료로 취급하지 않습니다.

## 공통 주소와 응답

첨부 저장소는 `REMOTEWORK_RESULTS_STORAGE_ROOT`로 지정하는 **프로젝트 밖의 전용 폴더**여야 합니다.
`.local/`·테스트 checkout 아래도 프로젝트 내부이므로 거절됩니다. 첨부 검사 시 pytest의
임시 저장소 역시 프로젝트 밖에 두며, 이 조건의503을 우회하려고 제품 검사를 완화하지 않습니다.
실제 배포는 실행 계정의 저장/정리 권한과 DB+파일 백업/복원을 함께 확인한 뒤 활성화합니다.

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

### 업무·결과물 웹의 최소 연결 접점 (2026-10-20)

2번 F1-04에 대한 현재 공통 계약이다. `/features/tasks`와 `/features/results`는 아직 준비 화면이며 업무/제출 웹의 제품 구현 완료를 뜻하지 않는다. 기존 `remotework/web.py`·`templates/base.html`·`features/registry.py`를 재사용한다.

- 웹 인증은 기존 `web.login`이 발급한 HttpOnly `rw_session`과 `auth.load_identity`의 `channel="web"`을 사용한다. 실제 사용자/접속은 요청마다 `g.user/g.auth_session`으로 확인한다. 브라우저 쿠키를 `/api/`의 Bearer 인증으로 바꾸거나 토큰을 HTML·브라우저 저장소에 노출하지 않는다.
- 웹의 POST/PATCH/PUT/DELETE는 기존 앱 전역 `web.protect_forms`를 통과한다. 템플릿 폼에는 `csrf_token()`의 값을 `csrf_token` 필드로 보낸다. 현재 검사는 form 필드 계약이며 JSON AJAX의 별도 CSRF 헤더를 지원한다고 가정하지 않는다. 신규 방식이 필요하면1번과 먼저 대조한다.
- 2번은 자기 웹 blueprint/템플릿에서 기존 업무 서비스의 현재 배정·지정 관리자 권한과 상태/버전/멱등 처리를 재사용한다. 웹 요청이 자기 서버의 Bearer API를 다시 로그인해 호출하는 구조는 만들지 않는다. 공통 `authenticated_transaction`을 포함한 기존 쓰기 보호를 우회하지 않는다.
- 2번 첫 화면은 기존 Own/Managed 업무 목록·상세/현재 필터 재조회부터 연결하는 안이다. 새 `/tasks`·`task_web.index`는 **제안**이며 아직 등록된 경로가 아니다.2번이 실제 blueprint/endpoint·필요 flag를 제공하면1번이 공통 메뉴의 `web.feature` 분기와 registry를 최소 연결한다. 준비 전에는 현재 준비 화면을 유지한다.
- 업무→결과물 연결은 서버에서 재검사할 업무 ID만 전달하고 브라우저의 담당자/검토자 값을 권한 근거로 쓰지 않는다.3번 첫 화면의 실제 URL/조회권한·파일/제출 경계는3번 제공 후 맞춘다. 기존 WinForms 연결을 웹 구현 근거로 대신하지 않는다.
- 성공 후에는 서버 확정 결과를 확인하고 목록·상세를 다시 읽는다. POST 폼 방식에서는303 GET 전환을 제안한다.401은 기존 웹 로그인 이동,403은 현재 접근 불가,409는 입력/예상 버전을 보존한 재조회,503은 준비/통신 실패를 구분한다. 응답 유실 뒤 자동 새 쓰기/새 요청 키를 만들지 않는다.

이 절은 기존 공통 접점을 설명하고 URL/첫 화면안을 제시한다. 업무·결과물 웹 구현, 공동 URL 합의, 공통 메뉴 변경·공유 적용/두PC 검증은 별도이며 이번에 수행하지 않았다.

기능 담당자는 `remotework/features/` 아래에 모델과 라우트를 만들고 `features/registry.py`의 `get_blueprints(config)`가 반환하는 목록에 등록합니다. 2번의 업무 조회 개발 초안은 명시적 활성화일 때만 모듈을 import합니다. 모델 파일을 해당 모듈에서 import해야 최초 `init-db`가 테이블을 발견합니다. 아직 구현되지 않은 기능을 자동으로 성공시키는 예시 API는 등록하지 않습니다.

다음은 공통 grant 도우미를 사용하는 연결 예시입니다. 현재 2번 업무 GET 구현은 [TASK_QUERIES](TASK_QUERIES.md)의 현재 배정·지정 관리자 관계를 직접 검사하며, 1번 회신에 따라 중복 grant 동기화를 요구하지 않습니다. 아래 코드를 현재 업무 라우트와 혼동하지 않습니다.

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
    # 이 grant 예시는 관리자도 grant가 없으면403입니다. 현재 2번의 실제 업무 조회는 위 설명처럼 배정·지정 관리자를 직접 검사합니다.
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

- grant를 권한 원천으로 채택한 기능은 담당자 변경·협업 탈퇴 시 기존 grant를 같은 트랜잭션에서 지우고 새 연결을 만듭니다. 2번 업무 조회의 현재 배정·지정 관리자 관계에는 중복 grant를 요구하지 않습니다.
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
3. 권한 없는 업무, 비참여 채팅방, 다른 직원 파일의 직접 요청을 거절하는지 확인합니다. 공통 grant 거절은 403이며, 현재 2번 업무 상세도 존재하지만 권한 없으면403·업무 없으면 전용404를 사용하는 기준을 1번이 수용했습니다.
4. 서버 재시작·로그아웃·접속 만료·DB 연결 실패를 확인합니다.
5. 두 PC와 웹에서 업무 배정 → 수락 → 제출 → 반려 → 재제출 → 승인을 시연합니다. 실제 장치는 팀원 5와 함께 확인합니다.

## 참고한 공식 문서

- [Flask 애플리케이션 팩토리](https://flask.palletsprojects.com/en/stable/patterns/appfactories/)
- [Flask 웹 보안과 쿠키·CSRF](https://flask.palletsprojects.com/en/stable/web-security/)
- [PyMySQL 연결 설정](https://pymysql.readthedocs.io/en/latest/modules/connections.html)

## 2026-10-17: 2번 조회 개발 초안 연결

업무 GET 목록·상세와 공통 HTTP 어댑터는 [업무 조회 계약](TASK_QUERIES.md)에 추가했습니다. 업무별 지정 관리자 정책은 사용자 확인 기준입니다. [1번 M1-01~10 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/7b0c81588db667deeccc8ad67f3249090f0276de/1번/2026-10-17_업무조회_API_DB_규약확정_회신.md)으로 현재 경로·DTO·정렬/전체 목록·권한·오류·상태 문자열·DATE/DateOnly 형식·공통 FK/보존 기준을 수용했습니다. 날짜의 형식과 시간대·마감 시각의 의미를 구분합니다. 상태 전환·제출/판단·배정 포인터·마감 의미는 2·3번 공동 합의가 필요합니다.

1번이 예약한 `002_task_queries.mysql.sql`의 [검토용 DDL·전용 MySQL 검증/복구안](../RemoteWork.Server/migrations/002_task_queries.md)을 준비합니다. DDL 승인·공통 기대 버전 검사 지원·공유 적용은 별도 게이트이며 현재 schema version1을 유지합니다. `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`일 때만 업무 Blueprint·모델을 등록하고 기본 off를 유지합니다. 일반 실행에 업무 데이터를 자동 생성하지 않습니다. 공식 공유 서버·계정·두 PC 검증은 미완료입니다. 1번 회신의 이전 health 성공과 문서 업로드 후 연결 거부를 구분하며 현재 서버 접속 성공으로 안내하지 않습니다.

아래 절은 공통 PR 당시의 이전 기준(8f09183)을 설명한 기록입니다. 당시의 ‘업무 API 미등록’과 현재 개발 초안의 명시적 활성화를 구분하세요. 인증·공통 ApiClient·DB 연결 규칙은 그대로 재사용합니다.

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

## 2026-10-22 후속: 옛 미확정 작업의 W1 원 배정 결합 — 구현 제안

4번564b352의 `journal_bridge`가 찾지 못하던 **확인 행이 없는 옛 요청**을 위해
`remotework/deadline_bindings.py`를 추가했다. 공통 W1/DB를 바꾸지 않으며
자동 action/reader/HTTP 등록과 배정별 확인 DDL 설치도 하지 않는다.

- `DeadlineBinding(task_id,event_id,assignment_revision)` / `target(binding)` /
  `parse_target(value)`는 `d1:<task_id>:<event_uuid>:<assignment_revision>`을
  정규화한다. 최대 signed BIGINT/INTEGER에서도 기존80자 target에 들어간다.
  **UUID 사건은 바꾸지 않는다.** 이 문자열은 W1 확인 대상이며 새 사건ID가 아니다.
  raw event만 사용한 이전 대상/키는 새 대상으로 변환하지 않는다.
- `current_binding(db,task_id=...)`는 실제2번 현재 own facts와1번 현재 사건에서
  화면의 현재 결합을 만든다. 날짜·버전 hash/임시 suffix로 사건을 제조하지 않는다.
- `read_bound_event(db,target)`는 같은 인증TX 안에서 **정확한 업무 current own**을
  먼저 검사하고, 원 `task_assignments(task_id,revision)`의 assignee가 현재 actor인지
  잠금 대조한 뒤 보관 사건의 같은 task를 확인한다. 확인행/전체 own목록 탐색이
  없어도 다른 초기불명 업무와 독립적으로 원래 사건을 읽는다. 현재 own 회수나
  원 배정 타인이면403, 검증된 사건 부재404·저장소/자료 불명503을 구분한다.
- `authorize_result(db,target)`는 기존 W1에 **명시 등록할 수 있는** 현재 접근
  callback이다. `/write-requests/keys`, perform_once, read-only result reader에
  같은 함수를 연결하고 actor/action/target·TTL/원본문 hash는 기존W1 검사를
  그대로 사용한다. 결과조회 본문은 기존 `{action,target}`로 충분하며 공통 코어에
  원 assignment 필드를 추가하거나 signed key를 다시 발급하지 않는다.
- 제안 wire: 새 확인 body에 `task_id`(정규 BIGINT 문자열)와 기존
  `expected_row_version`/`expected_assignment_revision`을 고정하고 서버가 위 target을
  재구성·대조한다. 최소 성공은 `task_id,event_id,assignment_revision,confirmed_at`.
  클라이언트의 원 operation/key/body·성공/불명 cache와 MainForm wrapper도
  이 결합을 함께 전달해야 한다. **이 wire와 배정별 확인 SQL은 대조 중인 제안**이며
  현재 HTTP/DDL 최종 규격 또는 구현 완료로 표시하지 않는다.
- 새 확인 mutate는 현재 event/assignment·상태/취소·expected 버전을 별도로 검사한다.
  보관 결과 접근이 허용됐다고 옛 요청을 새 배정 확인으로 실행하지 않는다.
  같은 계정이 배정1→타인2→본인3으로 돌아오면 UUID는 유지하고 target1/3은 다르다.
  같은 계정의 옛 배정 결과 조회 허용은 현재 own+원 actor+기존 W1 TTL 아래의 제안이며,
  중간상태·baseline/최종 정책 합의를 대신하지 않는다.

`test_deadline_events.py`에서 실제 journal/facts/배정·W1 key/result와 결합해 검사한다.
등록은 TESTING fixture만 수행하며 제품 startup이나 기존4번 초안을 수정하지 않는다.
4번은 수정된109 HTTP/166 UI와50 MySQL 검사를 제공했고1번도 독립 재실행했다.
추가 실제journal 소비13검사는 고정6302dbe 공급과564b352 bridge의 결합이며,
최종 배정별 source/확인 저장·2번 제품 hook·공유 적용/물리두PC와 구분한다.

## 2026-10-22: 2·4번 연결용 마감 변경 기록 함수 — 검토 제안

**제공 완료:** `record_change`는 2번의 원래 W1 mutation 안에서 업무 flush/refresh
후 감사·receipt 전에 호출합니다. `read_current`는 현재 사건, `read_event`는 보관된
사건을 같은 인증 세션에서 읽습니다. 실제 업무 writer 연결·HTTP/확인 저장은 별도입니다.
**아직 미확정:** 아래 번호 없는 SQL/사건 투영·확인 키·옛 결과 권한/최초 기준 전환은
검토 제안입니다. 기본 기동/2번 코드/공유 DB에 자동 설치하지 않습니다.
**2번 연결:** 실제 생성은 `task.created`/version1/이전 기한 None, 실제 수정·연장 승인은
`task.updated` 또는 `task.deadline_extension_approved`와 변경 전후 DATE를 전달합니다.
같은 날짜는 사건을 만들지 않고 A→B→A는 서로 다른 UUID·순서를 기록합니다.
오류는 잡아서 성공으로 바꾸지 말고 원 업무·사건·감사·receipt 전체를 롤백해야 합니다.
기존 자료에 현재 사건이 없으면 503이며 현재 날짜로 과거 최초 사건을 만들지 않습니다.
**4번 연결:** 사건은 업무의 실제 기한 변경이고 수신자/배정은 현재 facts로 별도 판정합니다.
기존 초안의 `(event,user)` 확인 키와 제안한 `(event,user,assignment_revision)` 경계를
대조하고 조회/저장/FK/영수증·성공 캐시를 함께 맞춰야 합니다. 보관 사건 reader는
권한 검사가 아니므로 현재 own 및 원래 확인/receipt의 사용자·배정을 확인한 뒤 노출합니다.
아래는 정확한 Python 입력·출력/잠금·실패·검증 범위입니다.

This is an **executable review proposal**, not an agreed HTTP/DDL contract or a
shared deployment. `remotework/deadline_events.py` supplies internal ports;
neither `create_app` nor any task writer imports/calls them automatically.
The separate `migrations/drafts/deadline_events.mysql.sql` is unnumbered. Do not
apply it to the shared database before owner review, fresh backup/restore and
an agreed rollout. Existing schema version1 is unchanged.

### Owner2: original mutation session, no hook or second commit

Call `record_change(db, task_id=..., source_row_version=...,
source_action=..., before_due_date=..., due_date=...)` **inside the existing W1
mutate**, after the task version/flush/refresh and before its audit/WriteResult.
Reuse the exact authenticated session and existing AdminGuard/parent/assignment
locks. Then the provider locks the current-event head and journal rows; it adds
no user/assignment lock after those journal locks. Propagate every failure so
the original task, journal/head, audit and receipt roll back together. Do not
call it after `perform_once` returns: a retained W1 result must not create an
event. This internal provider does not replace the caller's manager/creation
authorization and does not authorize HTTP access to any task.

- `task.created`: version1, `before_due_date=None`, actual stored new DATE.
  This captures a **new creation** at its original mutation; it is never a way
  to bootstrap existing tasks from their current date or old incomplete audit.
- `task.updated` / `task.deadline_extension_approved`: explicit original and
  stored new Python DATE. The returned frozen `DeadlineChange` contains UUID,
  task ID, per-task event order, source version/action, before/after dates,
  server UTC observation and actor. Same dates return `None`; title, text,
  start date, attachments, requests/rejections/withdrawals are not new events.
- A→B→A records separate UUIDs/order. Same task/source version and exact original
  values return the same record only while it is still current; changed input
  fails503. UUIDs are not date/version hashes. `recorded_at` is MySQL
  UTC_TIMESTAMP(6) observed in the transaction, **not a commit timestamp**.
- Missing current head on an existing task fails503 `DEADLINE_EVENT_UNKNOWN`;
  mismatched source/current dates or versions fail503
  `DEADLINE_EVENT_SOURCE_MISMATCH`; missing/incompatible tables fail503
  `DEADLINE_EVENT_STORAGE_REQUIRED`. No baseline, repair or history inference.
  Deployment/readiness must check the schema before connecting writers.

### Owner4: immutable task event versus current recipient/assignment

`read_current(db, task_id=...) -> DeadlineChange` is an **internal trusted**
same-TX reader, not an own-task authorizer. First use owner2's real current
facts/authorization in that same session; do not pass arbitrary client task IDs
directly to the internal reader. Missing/unknown/mismatched evidence fails503,
never an empty successful snapshot. An event records a real task deadline
mutation; it does not encode a recipient or manufacture another event on
reassignment. Compose current recipient/assignment from the current facts.
`read_event(db, event_id=...)` returns the retained task event or `None` for a
verified absent UUID. It is also an internal trusted reader, without recipient
authorization: use the retained confirmation/receipt's original actor and
assignment plus current own access before exposing an old success. Owner4's
old callback must not treat an immutable task event as a permanently assigned
recipient projection. Current own access alone never imports an earlier
assignment's confirmation into a new assignment.

The new functions do not finalize owner4's `DeadlineEvent` projection, old-event
history/result authorizer or HTTP DTO. In particular, the current review
confirmation PK `(event,user)` must be reconciled with the proposed
`(event,user,assignment_revision)` boundary, including all lookup/INSERT/FK/
receipt and C# success-cache keys. No automatic shared installation is allowed
for either draft. State transitions without a due change leave the task event
unchanged; whether confirmation returns on the same assignment after a
submitted→returned transition remains the existing joint proposal, not a new
policy decision here. Legacy baseline scope/time/operator/reason/backup also
remains a joint decision; no baseline function is provided.

### Verification boundaries

`tests/test_deadline_events.py` uses a fresh owned MySQL plus original
002/004/006/009 and the unnumbered journal draft. It checks sequence/replay,
non-change, unknown and rollback/storage/session boundaries.
`tests/test_deadline_event_writers.py` connects the actual owner2 writers/W1 to
this provider at the reviewed audit boundary **with a test-only adapter**.
It checks actual create/update/approved-extension, retained result replay,
non-change and downstream audit/receipt rollback. This is not a registered
production hook, operational ACK, real baseline, deadline HTTP/source or
physical two-PC test. Owner2 must explicitly connect the real mutation calls
after review. Use only an independently owned disposable MySQL, never SQLite or
the user's server/DB. The shared5000 identity flag and lifecycle ACK remain
unchanged by this module.
# 2026-10-22: 1번 마감 HTTP 공급 / PR70 재검토 전·공유 적용 전

아래 과거의 내부 함수만 제공/HTTP 미구현 표시는 이 구현 이전의 이력이다.
`remotework/deadline_http.py`는 실제 Flask·인증 TX·MySQL·W1을 연결한다.
4번의 검토 초안 제품을 복사하거나 2번 writer/4번 화면을 대신 수정하지 않는다.
기존 PR70에서 공급하며 최종 공동 검토·번호 예약·공유 설치 완료와 구분한다.

## 실제 HTTP 계약

모든 요청은 기존 정상 Bearer 인증을 쓴다. 입력 ID는 기존 정규 BIGINT 문자열,
배정 차수는 1..2147483647의 JSON 정수다. UUID 사건과 배정별 target을 구분한다.

| 경로 | 요청 / 성공 data |
|---|---|
| `GET /api/v1/deadline-alerts` | query 없음. `{server_time,items}` 전체 본인 snapshot |
| `GET /api/v1/deadline-alerts/{target}` | 원 binding 현재 열람 재인가 후 **전체** snapshot. 단건 목록 아님 |
| `POST /api/v1/deadline-alerts/{target}/confirm` | `Idempotency-Key` 및 정확히 `{task_id,expected_row_version,expected_assignment_revision}`. 최소 결과 `{task_id,event_id,assignment_revision,confirmed_at}` |
| `POST /api/v1/write-requests/keys` | 기존 `{action:"deadline_alert.confirmed",target}` → 기존201 키/만료 |
| `POST /api/v1/write-requests/result` | 기존 헤더의 원 키 + `{action:"deadline_alert.confirmed",target}`. 기존 completed/not_recorded·보관 만료410 계약 |

`target = d1:<task_id>:<event_uuid>:<assignment_revision>`이며 snapshot의 각 item이 제공한다.
item의 정확한 필드는 `event_id,target,task_id,assignment_revision,assignee_id,title,status,
cancelled,due_date,row_version,confirmed_at`이다. server_time/confirmed_at는 UTC,
미확인은 confirmed_at=null, due_date는 DATE다. 서버UTC는 같은 인증 TX의 MySQL에서 읽는다.
기존5상태/취소의 **전체 현재 사실**을 반환하고 UI는 기존 A1~A6 후보만 표시한다.
업무마다 현재 사건이 검증되지 않으면 부분/빈 성공 대신 전체503이다.

키 발급/보관 결과 조회는 현재 own + 원 배정 actor + 해당 task/event를 검사한다.
새 확인만 현재 사건·배정·두 버전·3허용상태/취소·발생시각을 재검사한다.
전날09 KST(전날00 UTC)부터 경과 업무까지 확인 가능하다.
확인·감사·W1 receipt는 같은 TX이며 확인만으로 업무 버전을 올리지 않는다.
동일 사건/계정/배정의 기존 확인은 같은 시각을 반환한다. 새 배정은 별도 확인이다.
동일 원 key/body 재생은 최소 결과만 돌려준다. 성공 후 목록 실패는 성공 취소가 아니다.
4번 source는 원 target/body/key와 최소 receipt의 task/event/배정을 모두 대조하고,
성공 cache/불명 Operation/로그인 wrapper를 원 binding에 연결해야 한다.
이 제공이 현재 string-event 전용 4번 어댑터에 이미 소비됐다는 뜻은 아니다.

오류: 입력400, 인증401, 현재 own/원 actor 불일치403, 검증된 task/event 부재404,
새 확인의 사건/배정/상태/버전 변화409 `DEADLINE_EVENT_CHANGED`,
발생 전409 `DEADLINE_NOT_OCCURRED`, 원 키/receipt 만료410,
사건/확인 스키마·불명/불완전503. 503을 미기록으로 바꾸지 않는다.
기존 raw-event target의 키는 d1 target으로 변환하거나 재발급하지 않는다.

## 설정·물리 저장소와 적용 순서

- 기본 OFF: `REMOTEWORK_ENABLE_DEADLINE_ALERTS_DRAFT=0`.
  1로 켜려면 기존 common W1/task query 및 MySQL이 필요하다.
- 쓰기 OFF의 독립 기존 결과 읽기는 `REMOTEWORK_ENABLE_DEADLINE_RESULT_READS_DRAFT=1`.
  목록/새 확인 경로는404, 새 키는503 `WRITE_ACTION_READ_ONLY`이며 결과만 현재 재인가한다.
- SQL: `RemoteWork.Server/migrations/drafts/deadline_events.mysql.sql` 뒤
  `deadline_confirmations.mysql.sql`. 자동 CREATE/ALTER/초기화/seed/ORM metadata 등록 없음.
  확인 PK `(event_id,user_id,assignment_revision)`, task+event→실제 journal,
  task+assignment→원 배정, user→계정의 보존형 FK, 계정/확인시각 index,
  signed BIGINT/INTEGER·ascii_bin UUID·UTC DATETIME(6)이다.
  4번의 이전 두열 PK SQL을 공유 DB에 적용하지 않는다. 아직 정식 번호/공유 설치 전이다.
- 실제 적용 전: PR70 결함 수정 재검토 → 2번 원 생성/수정/승인연장 W1 mutate에서
  record_change 명시 연결·전체 rollback/replay 확인 → 공동 baseline/상태·열람 범위 결정 →
  정식 번호/DDL 승인 → 기존 DB+파일 비공개 백업 및 별도 MySQL 전체복원·물리 비교 →
  누락 테이블만 적용/기존자료 비교 → 같은5000의 해당SHA·설정 재기동 → 역할/두PC 검증.
  되돌릴 때는 기능 OFF/독립 결과 읽기 유지와 검증된 이전 배포를 사용하고,
  새 확인·사건 자료를 DROP하거나 실제 DB 위에 덤프를 덮어쓰지 않는다.
- 이번 공유5000에는 새 API/3테이블/설정·baseline을 설치하지 않았다.
  기존 업무에 사건 head가 없으므로 플래그만 켜면 전체503일 수 있다.
  기존37테이블/schema1은 마감3테이블 완료의 증거가 아니다.

## RR 수정·직접 검사와 재현

PR70 920145c의 일반 SELECT 사전 탐색은 오래된 인증 RR snapshot으로 새 사건을 놓쳤다.
`read_event(...,task_id=정확한원업무)`는 부모 잠금 후 현재 사건을 읽도록 수정했다.
`read_bound_event`는 이 정확 경로를 쓴다. 이전 task_id 없는 신뢰된 호출은 현재 부모들을
ID순으로 잠근 후 사건을 current read하는 호환 경로다. 최대1000개이며 초과는503
`DEADLINE_EVENT_TASK_REQUIRED`다. 4번은 아는 원 task_id를 전달해 이 전체 잠금 경로를 피한다.
어느 경로도 journal부터 잠가 부모→사건 잠금 순서를 뒤집지 않는다.

검사는 **빈 독립 MySQL**의 `MYSQL_TEST_URL`만 지정하고 실제 .env 사용을 차단한 환경에서
`python -m pytest RemoteWork.Server/tests/test_deadline_events.py -q`(23),
`.../test_deadline_http.py -q`(21), `.../test_deadline_event_writers.py -q`(기존7)를 각각 실행한다.
기존 테스트의 소유DB 이름/localhost 안전 검사를 유지한다. 기본 raw002/004/006/009 및
새3테이블은 검사 fixture가 그 빈 DB에만 만든다. pytest import 경로에 Server/tests와
Server를 포함한다. 같은 DB에서 서로 다른 모듈의 raw CREATE fixture를 중복 실행하지 않는다.
Windows 소유 MySQL helper는 `Tests/TaskQueries.Http.Tests/run_mysql_http_check.py`의
OwnedMySQL/owned_runtime을 재사용했다. SQLite/실제DB는 사용하지 않았다.
23에는 오래된 인증 snapshot→다른 guarded TX commit→current/retained/binding/키인가 회귀,
21에는 실제 loopback TCP HTTP→목록/확인/W1 결과, 재로그인/배정1→3/권한회수/전체 rollback/
동시확인/기능OFF 결과/스키마손실/입력/중복키 검사가 포함된다.
업무 생성/기록 호출은 fixture이며 2번 제품 hook·4번 C# 새 binding·공유 두PC 검증은 별도다.
