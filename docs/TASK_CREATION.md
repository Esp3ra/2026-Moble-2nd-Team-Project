# 2번 개발용 신규 업무 등록

2026-10-19. 기존 공통 W1과 업무 3테이블을 사용하는 개발용 API와 WinForms 신규 등록 연결입니다. 사용자 확정인 등록자 필수 관리자 기준을 적용합니다. 최초 배정/버전 1/1과 POST 입력·결과는 **2번 개발 계약**이며, 2026-10-19 [1번 기술 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-19_협업요청_기술검토_회신.md)으로 초기값·원자 저장·재응답·새 개발 DB DDL을 수용받았습니다. PR #25의 실제 MySQL 등록/DDL/복원 검증과 코드 병합을 정식 migration·공유 서버 적용 승인으로 확대하지 않습니다.

[팀장 공통규약·담당별 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md) · [공통 쓰기 지원](WRITE_SUPPORT.md) · [입력/후보 준비](TASK_WRITE_PREPARATION.md)

## 활성화와 저장 경계

기본 서버에는 등록 POST 경로가 없습니다. **삭제 가능한 별도 개발 MySQL DB**의 공통 테이블·검토용 002/004 구조를 준비하고 별도 프로세스에 다음 세 옵션을 명시합니다. 기존 사용자 `.env`나 공유 서버에 일괄 적용하지 않습니다.

```text
REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1
REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT=1
REMOTEWORK_ENABLE_TASK_CREATE_DRAFT=1
```

테스트 config의 `ENABLE_TASK_CREATE_DRAFT=True`도 지원합니다. query 활성화가 없으면 업무 Blueprint가 등록되지 않습니다. create만 켰다고 공통 receipt 모델을 자동 등록하지 않습니다. HTTP 요청 중 테이블 생성·열 변경·전역 스키마 버전 변경을 하지 않습니다.

실제 로그인 업무 화면은 공통 세션에 묶인 등록 서비스를 사용하며 기본/미리보기 생성자에는 쓰기를 자동 주입하지 않습니다. 이 문서는 신규 등록 계약을 설명합니다. 이후 [수락·재작업](TASK_START_PREPARATION.md)과 [일반 수정·재배정·관리자 변경](TASK_MANAGEMENT.md)을 별도 개발 계약으로 연결했습니다. [취소·연장](TASK_LIFECYCLE.md)도2번 저장/조회·폼을 구현했으나1·3번 실제 소비 연결 전 변경503입니다.3번 결과 제출·검토·파일 구현을2번이 대신하지 않습니다.

## 웹 등록 연결 — 2026-10-21

위 query/common/create 옵션을 명시한 서버의 `/tasks/new`에서 기존 cookie 인증·CSRF로 등록합니다. 활성 관리자만 공통 후보 서비스의 이름/부서·20명 단위 페이지를 검색하고,업무 입력과 선택을 유지할 수 있습니다. 등록자 필수 관리자·최소 공개 후보 정보·최종 활성 후보 검사는 기존 계약을 따릅니다. 현재 웹 폼은 첨부 입력 없이 기존 여섯 필드 본문을 만들며, 브라우저에 Bearer를 주지 않습니다.

`등록 내용 확인`은 현재 로그인에 묶인 서명 확인폼과 W1 키만 준비하며 업무를 저장하지 않습니다. 별도 실행에서 API와 같은 `execute_creation`과 단일 transaction/감사/receipt를 사용합니다. 결과 불확실 시 같은 열린 폼에서 명시적으로 결과를 확인하고,`not_recorded` 뒤에만 같은 키/본문을 다시 전송합니다. 자동 재전송·새 키 자동 발급은 없습니다. 확인폼을 닫은 뒤 복원하는 저장소는 제공하지 않으며 새 등록 확인은 별개 요청입니다. 웹 기능을 끄면 이 웹 결과 경로도 닫힙니다.

소유 합성 브라우저의 등록→결과 미기록 확인→명시 실행→직원 로그인·수락 흐름을 확인했습니다. MySQL/API 회귀의 최종 실행 결과·미실행은 [업무 웹 검증](../Tests/TaskWeb.Tests/README.md),서명·세션·크기와 담당 경계는 [업무 웹 계약](TASK_WEB.md#task-web-create-start)을 따릅니다. 아래 WinForms의 접속별 메모리 복원과 웹 확인폼의 수명은 다릅니다.

## WinForms 등록과 결과 확인

1. 신규 업무 창에서 제목·내용·날짜를 작성하고 별도 후보 창에서 담당자·업무 관리자를 선택합니다. 등록자는 필수 관리자로 유지합니다.
2. **입력 확인**은 현재 입력과 선택을 검사합니다. 기존 `CheckedInput`·`PreparedSelection`을 유지하고, 신규 등록용 `TaskCreationRequest`에 검증된 값과 관리자 ID의 불변 사본을 따로 보관합니다. 입력 확인이나 후보 선택 자체는 POST하지 않습니다.
3. **등록**을 눌러야 공통 요청 키를 발급받고 POST합니다. 처리 중 중복 실행·입력 변경을 막습니다. 서버는 현재 수행자·후보·등록자 관계를 다시 검사합니다.
4. 성공 응답 또는 저장된 성공 결과를 확인해야 창을 닫고 현재 업무 범위·상태 필터로 목록을 재조회합니다. 새 ID가 결과에 있으면 해당 행과 상세를 선택하고, 필터 밖이면 기존 조건과 가능한 기존 선택을 유지합니다. 목록 재조회 실패를 등록 실패나 새 POST로 바꾸지 않습니다.

| 요청 상태 | 화면 동작 |
|---|---|
| 입력 오류·등록 전 키 발급 실패 | 입력을 유지하고 확인·등록을 다시 진행. 등록 POST 전 실패와 결과 불확실을 구분 |
| POST 응답 유실·취소·판독 불가 등 결과 불확실 | 새 등록을 막고 **처리 결과 확인** 제공. 본문과 요청 키 유지 |
| 결과 조회 `completed` | 저장된 최소 결과를 검증한 뒤 성공 처리·목록/상세 재조회. 생성 POST 재전송 없음 |
| 결과 조회 `not_recorded` | **같은 요청 다시 전송**을 활성화. 사용자가 눌렀을 때 기존 키·원문 본문으로만 재전송 |
| 조회 실패 | 결과 확인을 다시 안내. `not_recorded`로 추측하거나 새 키를 자동 발급하지 않음 |
| 등록·재전송 POST의 키 만료 410 | 보관된 성공 결과가 있을 수 있으므로 **처리 결과 확인** 제공. 새 키 발급·자동 재등록 없음 |
| 결과 조회의 키 만료 410·키 재사용/충돌 | 자동 재등록을 차단하고 현재 업무 상태의 수동 확인 안내 |
| 401·403·로그아웃·재로그인·서버 변경 | 이전 접속의 쓰기와 늦은 응답을 차단하고 창의 선택·확인값·보류 요청을 정리 |

`TaskCreationSessionStore`가 같은 공통 `ApiClient`와 캡처한 로그인 세션의 `TaskCreationWorkflow`를 메모리에 보관합니다. **등록 창을 닫았다 열거나 업무 메뉴를 나갔다 다시 들어와도** 같은 로그인 세션이면 불확실 요청과 확인 상태를 이어갑니다. 403으로 차단된 접속도 메뉴 재진입으로 새 요청을 만들 수 없습니다. 앱 종료·로그아웃·재로그인·서버 변경 시 이전 요청은 이어 쓰지 않으며, 디스크 복구 저장소는 제공하지 않습니다. 전송 중에는 사용자 닫기를 막고, 세션 취소 뒤 이전 응답을 새 접속의 성공으로 표시하지 않습니다. 자동 결과 조회·자동 재전송은 없습니다.

## 요청과 성공 결과

1. 활성 admin이 기존 `POST /api/v1/write-requests/keys`에 `{"action":"task.created","target":"new"}`를 보내 요청 키를 받습니다.
2. `POST /api/v1/tasks`에 Bearer 토큰과 `Idempotency-Key` 헤더, 아래 여섯 필수 필드를 보냅니다. 2026-10-21 수용한 선택 필드 `attachment_ids`도 지원합니다. 예시 사용자 ID는 실제 후보 조회 결과로 교체합니다.

```json
{
  "title": "개발용 업무",
  "description": "",
  "start_date": "2026-10-19",
  "due_date": "2026-10-23",
  "assignee_id": 2,
  "manager_user_ids": []
}
```

- 제목은 Unicode 코드 포인트 200개까지이며 공백만인 제목은 거절합니다. 설명은 문자열이며 빈 값도 허용합니다. 원문의 앞뒤 공백을 임의로 자르지 않습니다. 공통 요청 크기 32KiB가 적용됩니다.
- 날짜는 유효한 `YYYY-MM-DD`·1000~9999년·시작일≤마감일입니다. 현재 날짜와 비교한 별도 수락·마감 정책은 넣지 않았습니다.
- 담당자는 현재 활성 employee/admin 1명, 관리자는 현재 활성 admin이어야 합니다. ID는 양의 JSON 정수이며 관리자 중복은 거절합니다.
- 인증된 등록자는 서버에서 관리자에 자동 포함합니다. 요청 관리자 목록이 비어도 등록자 1명은 저장됩니다. 요청의 상태·수행자·버전·시각 위조 필드는 거절합니다.

<a id="task-creation-attachment-selection"></a>
### 선택 첨부 목록 — 2026-10-21

[팀장 수용 규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member3-proposals-accepted-20261021)에 따라 `attachment_ids`는 업무에 연결할 최종 파일 ID 배열입니다. 생략과 `[]`는 모두 첨부 없는 신규 등록이며 첨부 제공부를 요구하지 않습니다. 생략·빈 배열·값 및 배열 순서는 서로 다른 W1 원문으로 보존합니다. 최대 10개, 소문자 하이픈 포함 정규 UUID 문자열만 허용하고 중복·null·비문자열을 거절합니다. 전체 JSON 본문에는 기존 32KiB 제한을 적용합니다.

값이 있으면 새 업무·최초 배정·관리자를 flush한 뒤 현재 등록된 3번 `replace_task_files`를 **같은 Session·트랜잭션**에서 호출합니다. 초기 기대 버전은 row_version 1·assignment_revision 1입니다. 제공부의 현재 접근 권한·업로드 완료·참조 검사를 통과해야 하며, 실패하면 업무·첨부 연결·감사·receipt가 함께 rollback됩니다. 등록에 별도 버전 증가나 첨부 전용 성공 이벤트를 추가하지 않고 기존 `task.created`와 최소 성공 결과를 유지합니다. 결과 응답 유실 뒤 저장된 성공을 찾으면 첨부를 다시 연결하지 않습니다.

기존 업무 등록 옵션만으로 첨부 제공부·013 구조를 자동 준비하지 않습니다. 값이 있는 요청은 3번 results/shared 제공부의 기존 활성화 조건과 준비된 `013_shared_attachments.mysql.sql` 구조도 필요하며, 제공부가 없으면 `503 TASK_ATTACHMENT_PROVIDER_REQUIRED`입니다. 제공부의 파일 오류를 다른 성공이나 빈 첨부로 바꾸지 않습니다. 공유 DB·DDL 적용·운영 ACK를 이 연결에서 활성화하지 않습니다.

C# `TaskCreationRequest`는 선택 `IReadOnlyList<string>? attachmentIds = null`을 불변 사본으로 보관합니다. null은 JSON 전송에서 생략하고, 빈 배열·원래 ID 순서는 그대로 전송·복구합니다. **WinForms/업무 웹의 업로드·파일 선택 UI는 미연결**이므로 현재 화면 등록은 기존 여섯 필드로 진행합니다. 수정의 최종 목록 교체는 [별도 기술 조건과 503 차단](TASK_MANAGEMENT.md#task-management-attachment-selection)을 따릅니다.

성공은 공통 envelope의 HTTP 201입니다. `data`의 필수 필드는 다음과 같습니다.

```json
{
  "task_id": "123",
  "row_version": "1",
  "assignment_revision": 1,
  "task_status": "not_started",
  "updated_at": "2026-10-19T02:00:00Z"
}
```

업무 ID와 row_version은 양의 정규 BIGINT 문자열입니다. 최초 업무는 진행전, 배정 차수·row_version은 명시적 1/1, 수락 시각은 NULL입니다. 전역 schema version 1과 업무 row_version은 별개입니다.

## 권한·원자성·재요청

기존 `authenticated_transaction(roles=("admin",))`과 현재 `task.created/new` authorizer를 재사용합니다. 같은 요청 Session에서 사용자 정렬 잠금·후보의 활성/역할 검사·업무·최초 배정·관리자·선택 첨부 연결·`task.created` 감사·공통 receipt를 함께 저장합니다. 개별 commit이나 업로드/다운로드·외부 네트워크 동작을 끼우지 않습니다.

최초 후보와 선택 첨부의 현재 상태 검사는 mutate 안에서 합니다. 성공 뒤 후보나 파일 상태가 바뀌어도 같은 원문 본문·키는 기존 성공을 재응답하며, 현재 요청 수행자의 권한은 재검사합니다. 같은 키로 다른 본문을 보내면 409 `IDEMPOTENCY_KEY_REUSED`입니다. 관리자·첨부 ID의 요청 순서와 첨부 필드 생략 여부까지 원문 JSON 값으로 비교하므로 실패·응답 유실 뒤 본문을 임의로 바꾸지 않습니다.

응답 유실은 같은 action/target/key로 기존 결과 조회를 사용하고 업무를 재조회합니다. 새 키로 자동 생성 요청을 반복하지 않습니다. 만료·공통 결과 보관·현재 권한 재검사는 W1 계약을 따릅니다.

| 상황 | 응답 |
|---|---|
| 로그인 필요·만료 | 401 |
| 현재 직원의 생성 | 403 |
| 입력·날짜·중복 관리자·위조 필드 | 400 `INVALID_INPUT` |
| 현재 담당자 후보 불가 | 409 `ASSIGNEE_UNAVAILABLE` |
| 현재 관리자 후보 불가 | 409 `TASK_MANAGER_UNAVAILABLE` |
| 선택 첨부 형식·개수·중복 오류 | 400 `INVALID_INPUT` |
| 선택 첨부 없음·삭제 처리 중 | 404 `ATTACHMENT_NOT_FOUND` |
| 선택 첨부 미완료 등 제공부 사용 불가 | 409 `ATTACHMENT_UNAVAILABLE` |
| 선택 첨부 현재 접근 권한 없음 | 403 `FORBIDDEN` — 등록 권한 오류와 같은 공통 코드 |
| 첨부 제공부 미등록 | 503 `TASK_ATTACHMENT_PROVIDER_REQUIRED` |
| 첨부 구조·저장소 준비 문제 | 제공부의 기존 503 오류 |
| 요청 크기 초과 | 공통 413 |
| 공통 쓰기 비활성화 | 503 `WRITE_SUPPORT_DISABLED` |
| 생성 권한 함수 미연결 | 503 `WRITE_ACTION_NOT_READY` |
| 업무/receipt 구조 누락·DB 실패 | 기존 공통 DB/스키마 503 |

WinForms는 첨부 전용 404/409를 확정 실패로 구분해 입력 단계로 돌아가되 원래 파일 ID와 본문을 유지합니다. 자동 파일 삭제·선택 제거는 없습니다. 공통 403은 원인이나 미기록 여부를 추측하지 않고 기존 접속 차단/메모리 정리 규칙을 유지하며, 401도 기존 만료 처리입니다. 원래 접속이 유효한 서버 503·결과 불확실은 기존 키/본문으로 처리 결과를 먼저 확인합니다. 권한 상실·접속 교체 뒤에는 이전 입력을 다시 사용하지 않으며 파일 자동 DELETE·새 키 자동 재등록도 없습니다. 저장 성공 확인 뒤의 후속 오류를 새 등록이나 재전송 사유로 쓰지 않습니다.

<a id="task-creation-upload-transport"></a>
## 등록용 C# 첨부 전송 — 2026-10-22

`HttpTaskCreationUploadService`와 `TaskCreationUploadAttempt`는 현재 관리자 로그인에 고정된 공통 `ApiClient.Session`으로 3번의 기존 Bearer `results/shared/uploads/start`, `/{id}/chunks`, `/{id}/complete`를 소비합니다. 서버나 공통 HTTP를 변경하지 않았습니다. `PrepareFileAsync`는 최대25MiB의 파일을 읽어 이름·크기·SHA256·바이트 사본을 확정하며 HTTP를 보내지 않습니다. 이후 원본 파일이 바뀌어도 전송 내용은 바뀌지 않습니다.

| 상황 | 처리 |
|---|---|
| 준비 완료 | `Ready`; 명시 `StartAsync`에서만 시작 |
| 시작 응답 유실·잘못된 metadata | `StartUncertain`; ID를 모르므로 재시작·새 업로드 자동 생성 금지 |
| ID 확인 후 청크/완료 응답 불명 | `NeedsRetry`; 명시 `RetryAsync`에서 같은 ID·마지막 확인 offset·같은 바이트 또는 완료만 재요청 |
| 완료 metadata 검증 성공 | `CompletedFile` 제공. 아직 업무 저장·첨부 연결 성공을 뜻하지 않음 |
| 400/404/409/413 | `Rejected`; 오류 코드 유지, 자동 재시도 없음 |
| 401/403·접속 변경 | `Disconnected`; 이전 접속의 파일 사본·결과 무효화 |
| 폐기 | 진행 중 전송 취소·사본 정리. 서버 DELETE를 호출하지 않음 |

16KiB 청크의 base64 `+`는 기본 JSON 인코더에서 `\u002B`로 늘어나 32KiB 본문 제한을 넘을 수 있습니다. 2번 청크 DTO만 전용 converter로 내부 생성 base64를 기록하며 공통 JSON 인코더·파일명 규칙을 바꾸지 않습니다. 청크 크기를 줄이거나 서버 제한을 늘리지 않습니다.

현재 서비스는 **파일 선택/삭제 UI에 주입하지 않았습니다**. 작업 도중 PR64가 병합되어 캡처된 공통 DELETE는 제공됐습니다. 이를 소비하는 임시 파일의 명시 폐기·선택 상태를 다음 단계에서 연결합니다. `Dispose`는 서버 파일 삭제나24시간 후 자동 삭제를 보장하지 않습니다. 일반수정 첨부의 같은 TX diff/감사, 웹 cookie/CSRF, `view=read`/`back_context`는 기존 T3-1~5 기술 조건을 기다립니다.

[전용 검증](../Tests/TaskCreationUploads.Tests/README.md): 제어 HTTP344개, 실제 C#→TCP→임시 MySQL/파일26개와 독립 DB9개를 통과했습니다. 실제 시작·청크·완료, 등록 연결·같은 키 receipt·직원 다운로드를 확인했고 응답 유실은 실제 성공 응답 뒤 클라이언트에서 명시 주입했습니다. 공유 운영 서버·두PC·GUI·DELETE·25MiB 전체 네트워크 전송은 미실행입니다.

## 검증

2026-10-21 선택 첨부 확장의 C# 제어 검사는 `dotnet run --project Tests/TaskCreation.Tests/TaskCreation.Tests.csproj -c Release`로 **133개 통과**를 직접 확인했습니다. 불변 목록·생략/빈 배열·순서·32KiB·정확한 오류 분류·결과 유실/동일 키 복구를 포함하며 실제 WinForms와 명시 HTTP 대역의 범위입니다. 실제 서버/DB·파일 바이트 검증 수치로 해석하지 않습니다.

서버의 실제 등록된 제공부와 파일 바이트·같은 트랜잭션·rollback·명시 첨부 수정 차단은 `test_task_creation_attachments.py` 및 [소유 MySQL `--suite attachments` 실행](../Tests/TaskWeb.Tests/README.md#task-creation-attachment-checks)으로 따로 검사합니다. 이번 소유 MySQL/Flask 직접 실행은 기존 등록·관리 API/웹 회귀를 포함해 **274개 통과**입니다. 실제 파일 청크/바이트를 사용했지만 C#→실서버 소켓·공유 서버/두PC 검증은 아닙니다. 해당 회차의 보고서/LOG를 확인하며 아래 이전 회차 수치와 합산하지 않습니다.

2026-10-19 WinForms 연결 회차의 실제 실행은 다음과 같습니다. 서로 다른 실행의 검사 수를 하나의 통과 수치로 합산하지 않습니다.

| 실행 | 결과·범위 |
|---|---|
| [TaskCreation.Tests 제어 검사](../Tests/TaskCreation.Tests/README.md) | **79개 통과**. HTTP 대역과 실제 WinForms 메시지 루프: 요청 형식·Unicode·불변 값, 결과 불확실/조회/명시 재전송, POST 410 후 보관된 성공 복구, 취소·세션·권한·늦은 응답, 창/메뉴 재진입·접속별 분리·성공 후 필터/선택 유지 |
| 같은 프로젝트의 실제 C# HTTP 실행 | **10개 통과**. 자동화된 실제 등록 모달→공통 ApiClient→TCP→새 MySQL, 성공 후 관리 업무 목록/상세. 추가 2건은 서버 응답 후 클라이언트에서 응답 유실과 서버 전달 전 전송 실패를 주입해 복구 확인 |
| Python 독립 DB 검사 | **24개 통과**. 업무3·배정3·관리자6·감사3·receipt3, 각 등록자 관계·초기1/1·미수락·최소 결과 정합성·global schema version1 |
| 기존 후보·조회 회귀 | TaskPreparation **87개**, TaskQueries **171개**를 각각 통과. 신규 실제 쓰기 검증 수치와 합산하지 않음 |

실제 HTTP 회차에서는 후보 선택값을 테스트가 주입합니다. 후보 검색/페이지 선택 창은 별도 후보 검사 범위입니다. DB 초기화는 새 owned MySQL 프로세스·datadir·DB에만 수행하며 raw002/raw004와 공통 테이블을 분리해서 준비합니다. 정상 및 의도적 실패 뒤 프로세스·DB·임시 폴더 정리를 확인했습니다. 자세한 명령과 미실행 범위는 [테스트 안내](../Tests/TaskCreation.Tests/README.md)를 따릅니다.

서버 등록 자체의 앞선 MySQL **18개** 검사는 이전 서버 구현 회차의 근거입니다. 이번 WinForms 회차에서 다시 실행한 수치로 옮기지 않습니다. 공유 서버·두 PC·사용자의 수동 전체 GUI·프로그램 재시작 후 보류 요청 복구는 이번 검증에 포함하지 않습니다.

### 서버 단독 검사 실행 기준

`RemoteWork.Server/tests/test_task_creation.py`는 별도 `remote_work_test_` MySQL DB를 요구합니다. MySQL URL 없이 실행하면 명시적으로 건너뛰며 SQLite로 대신 검증하지 않습니다. 기존 fixture는 지정 테스트 DB를 정리하므로 공유·운영 DB를 지정하지 않습니다.

```powershell
& .\.venv\Scripts\python.exe -m pytest -q tests/test_task_creation.py
```

실제 실행 환경·건수·회귀와 미실행 범위는 [LOG 구현·검증](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)에 기록합니다. 공통 감사가 저장됐다는 사실을 3번의 화면용 업무 이력 기능 완료로 표시하지 않습니다.
