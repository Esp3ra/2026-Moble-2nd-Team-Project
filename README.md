# 원격 근무용 업무 처리 확인 시스템

## PR #7 R1·R2 수정 보고 — 2026-10-17

기준 원본 alpha8a652ad와 공지 head7478101을 결합하고 공지 전용 routes.py·회귀만 수정했다. 기존 업무 격리 HTTP 실행기와 공통 인증/DB/관리자 가드는 보존한다.

| 구분 | 현재 상태 |
|---|---|
| 구현·작성자 검증, 재검토 대기 | R1: admin_transaction 뒤 공지를 FOR UPDATE/current read·populate_existing으로 다시 확인해 삭제200/후속404·삭제 감사1건 보장. R2: 현재 INTEGER/C#int에 맞춘 개발 방어 경계1..2147483647, DB 바인딩 전 기존 미존재404로 처리. 일반 DB 예외를404로 바꾸지 않음 |
| 추가 발견·보완 | MySQL DATETIME 저장 정밀도로 등록 응답/재조회 시각이 달라지는 경우를 재현해 flush 후 저장된 값을 refresh하여 응답. 시각 컬럼/공통 규약은 바꾸지 않음 |
| 미합의 | 공지 전용 ID 범위·범위 밖404 유지/400 등 최종 오류, 삭제 응답·API/DTO·보존·DDL 번호/버전·공유 적용/활성화. 1번 회신 전 개발 초안이며 BIGINT 전환이나 공통 규약 확정 아님 |
| 미실행/범위 밖 | 공유/운영DB·두PC·웹·장비·수동 전체GUI, 이번 전체MySQL/실제C#→MySQL. 팝업/확인 기록·파일 전송·중지된 정렬 추가 조정은 PR 밖 |

이번 실제 결과: 서버 SQLite전체74통과/16생략(기존 MySQL2+DDL12+새 R1 MySQL2), 격리MySQL8.2 REPEATABLE-READ 표적32통과(공지 기존+R1/R2/시각/삭제rollback+공통관리자회귀), 실제 공지 C#→HTTP→임시SQLite37검증 통과. 최신 업무HTTP 프로젝트/참조앱 Release 경고0·오류0, 업무 격리 HTTP 실행기300검증·권한회수11개(실행기 내 포함; 중복 합산하지 않음), C#직원36/관리자52검증 통과. 서로 포함/반복되는 수치를 합산하지 않는다.

명령: 서버 `python -m pytest -q`, MySQL 전용 DB에서 `python -m pytest -q tests/test_notice_review.py tests/test_notices.py tests/test_admin_concurrency.py`, 프로젝트 루트 `python Tests/Notices.Tests/run_http_check.py --client-dll <Notices.Tests.dll>`, `python Tests/TaskQueries.Http.Tests/run_http_check.py --client-dll <TaskQueries.Http.Tests.dll>`. MySQL은 새 소유 datadir/프로세스/임시포트·합성계정만 사용하고 종료/정리했다. 이전 전체MySQL/빌드 보고는 아래 당시 결과로 보존하며 이번 실행으로 재사용하지 않는다.

새 회귀를 수정 전 코드에 적용했을 때 SQLite의 큰ID500과 MySQL 동시삭제200/200·중복감사 경고를 탐지했다. 수정 후에는 해당 회귀가 통과했으며 검토자 독립 재확인·1번 계약 회신·원본 병합/정식 적용은 별도다. REMOTEWORK_ENABLE_NOTICE_DRAFT/ENABLE_NOTICE_DRAFT의 기본off와 공통version1·업무002 예약을 유지한다.


원격 근무 중 업무 배정·상태·마감·결과 검토와 공지·채팅·근태를 관리하는 팀 프로젝트입니다. `alpha`는 화면 초안과 담당별 구현을 통합하는 개발 브랜치입니다. C# WinForms 클라이언트와 Python 공통 서버를 포함합니다.

현재 공통 로그인·사용자 관리·HTTP·DB 기반과 공지 DB 저장 연결 개발 초안, 채팅의 로컬 동작이 구현돼 있습니다. 업무 목록·상세는 공통 인증을 사용하는 GET API·HTTP 서비스와 기존 화면을 연결한 2번 개발 초안입니다. 업무 API는 명시적으로 활성화한 개발 서버에서 사용하며, 기본 실행에는 미확정 업무 스키마를 적용하지 않습니다. 설계·합의 사항은 [DOCX](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/DOCX/DOCX), 구현·검토 이력은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG), 담당별 수정 요청과 재검토 결과는 [PR](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/PR)에서 관리합니다.

## 현재 구현 범위

| 담당 | 영역 | 현재 동작 |
|---|---|---|
| 1번 | 공통 서버·DB·인증·사용자·통신 | 실제 로그인·역할 진입, 사용자 관리·비밀번호 변경, 접속 만료·폐기, 공통 응답·오류·트랜잭션·감사 이력, 웹 로그인·사용자 목록 |
| 2번 | 업무·상태·마감 | 기존 화면·조회 경계를 유지한 GET 목록/상세·전체/5상태 필터·서버 본인/지정 관리자 권한·공통 HTTP 연결. 개발용 활성화 필요, 업무 저장·상태 변경은 제외 |
| 3번 | 결과물·제출·검토·이력 | 직원·관리자 내 결과물과 관리자 검토 화면 예시. 실제 제출·파일·승인/반려·이력 저장은 미연결 |
| 4번 | 공지·채팅 | 명시적 개발 활성화 시 공지 DB 등록·조회·관리자 삭제, 대상 필터·고정·정렬. 채팅은 기존 로컬 메모리·메뉴 이동 중 저장소 유지 |
| 5번 | 출퇴근·연차·장치 | 기존 폼과 역할별 화면 예시. 실제 근태 저장·승인·장치 인증은 미연결 |

업무 조회는 현재 본인 배정과 업무별 지정 관리자 조건을 서버에서 각각 검사합니다. 2026-10-17 [1번 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_업무조회_API_DB_규약확정_회신.md)으로 조회 API·DTO·권한·오류와 상태/날짜 표현·공통 FK 기준을 수용했습니다. 상태 전환·마감 의미·제출 연결은 3번과 합의 필요이며, 실제 DDL 승인·공통 버전 지원·공유 DB 적용·두 PC 통합은 별도입니다.

## 기술과 실행 환경

- 데스크톱: Windows, .NET 8 SDK, C# WinForms.
- 서버: Python 3.11 이상, Flask/Waitress, SQLAlchemy/PyMySQL.
- DB: 공동 사용은 MySQL. SQLite는 명시적 데모·테스트용입니다.
- UI 구조: `LoginForm → MainForm → 기능별 UserControl`. 기존 화면의 Designer·도구상자 편집 구조를 유지합니다.

## 실행

1. 처음 화면과 공통 로그인을 확인하려면 프로젝트 루트의 `Start-Demo.cmd`를 실행합니다. 별도 SQLite 데모 DB가 준비되며 최초 생성 계정의 무작위 비밀번호는 서버 창에 표시됩니다.
2. 서버를 켠 상태에서 `Start-Desktop.cmd`를 실행하거나 `RemoteWork.sln`을 Visual Studio로 열어 `RemoteWork.WinForms`를 시작합니다.
3. 웹 공통 화면은 `http://127.0.0.1:5000/login`에서 확인합니다.

```powershell
dotnet run --project .\RemoteWork.WinForms\RemoteWork.WinForms.csproj
```

공동 MySQL 설정·초기화·테스트 계정 준비는 [SETUP](docs/SETUP.md)을 따른 뒤 `Start-MySQL.cmd`로 실행합니다. 서버 패키지 설치에는 인터넷 연결이 필요합니다. 실제 `.env`는 `.env.example`을 참고해 로컬에서 준비합니다.

클라이언트에는 `http://서버IP:5000/`처럼 **서버 루트**를 설정합니다. 공통 `AppServices.Api`가 요청 경로에 `/api/v1/`을 붙입니다. 다른 PC의 `127.0.0.1`은 서버 PC를 가리키지 않으므로 서버 주소·수신 설정·방화벽과 두 PC 연결을 별도로 확인해야 합니다.

## 로그인과 공통 연결

아이디·비밀번호로 로그인한 서버 사용자의 역할에 따라 직원·관리자 화면으로 진입합니다. 로그인 화면의 기존 미리보기 버튼 위치는 서버 연결 설정과 웹 로그인 열기에 사용합니다. 회사명은 확정 전까지 `회사명`으로 표시합니다.

2026-10-17 사용자 결정에 따라 지문센서는 **로그인 이후 원격 근무 중 본인 인증 수단**으로 사용합니다. 사용자가 설명한 취지는 인증 시점에 본인이 근무 자리에 있는지 확인하는 **재석 확인**입니다. 로그인 화면의 지문 버튼과 로그인 대기 안내는 제거했습니다. 실제 센서 통신·인증 화면·API·DB는 5번 담당의 연결 전이며, 인증 요청 시점·주기·응답 시간·미응답/실패 처리·출퇴근 연계는 1번·5번 등 관련 담당과 합의가 필요합니다. 기존 업무 조회나 업무 상태에 장치 인증 조건을 임의로 추가하지 않습니다.

공통 API는 Bearer 인증, snake_case 필드와 UTC ISO 8601 기록 시각을 사용합니다. `ApiClient`는 접속 세대·서버·토큰을 요청에 연결하여 이전 접속의 늦은 응답이 새 로그인 상태를 바꾸지 않게 합니다. 현재 접속의 401은 본문 JSON 형식과 관계없이 인증 만료로 처리합니다. 공통 관리자 변경은 잠금 뒤 수행자·접속·관리자 상태를 다시 확인합니다.

실제 API·오류·DB·기능 등록 방법은 [INTEGRATION](docs/INTEGRATION.md)을 따릅니다. 문서의 이전 업무 코드 블록과 새 2번 GET 개발 API를 구분합니다. 현재 조회 API와 활성화 조건은 [업무 조회 개발 계약](docs/TASK_QUERIES.md)을 따릅니다.

## 공지 DB 개발 초안과 채팅 저장 범위

공지 작성·목록·상세·관리자 삭제를 공통 인증 서버와 notices 테이블에 연결합니다. 전 직원·기획팀·개발팀·디자인팀을 target으로 구분하고 사용자 결정에 따라 직원도 모든 부서를 조회합니다. 작성자·UTC 시각은 서버에서 결정하며 저장 성공 때만 작성 창을 닫고 오류 시 입력을 유지합니다. 목록/대상 필터·고정 우선·열 정렬·조회 버튼 상세를 유지합니다. 정적 샘플 공지는 제거했고 종료 후 유지 여부는 연결한 DB가 담당합니다.

공지 API/DB는 1번 검토 전 개발 초안으로 **기본 off**입니다. 프로젝트 루트의 PowerShell에서 `$env:REMOTEWORK_ENABLE_NOTICE_DRAFT = "1"`을 지정하고 `& .\Start-Demo.cmd`를 실행하면 별도 SQLite 데모에 공지 테이블을 준비합니다. 기존 업무 플래그와 독립적으로 등록하며 공통 인증·스키마 버전1·업무 코드는 보존합니다. 실제 MySQL·공유 DB 적용·migration 번호·정식 활성화는 협의/검증 대기입니다. [공지 준비·API·제한](RemoteWork.Server/remotework/features/notices/README.md)을 따릅니다.

첨부는 파일명 메타데이터만 저장하고 원본 절대경로·파일 본문을 서버에 보내지 않습니다. 실제 업로드/다운로드·중요 공지 자동 팝업·직원별 확인 기록과 정렬 추가 조정은 이번 DB 변경에 포함하지 않습니다.

채팅은 기존 MainForm 단위 로컬 저장소로 메뉴 이동 중에는 유지되지만 창 종료 후 사라집니다. 개인/그룹 방 생성·삭제, 참여자·메시지 조회, Enter 전송·Shift+Enter 줄바꿈을 제공합니다. 실제 채팅 사용자 매핑·서버/DB·두 PC 전송은 미연결입니다.

## 업무 조회와 상태 기준

`ITaskQueryService`·`TaskSummary`·`TaskDetail`을 유지하고 실제 로그인 MainForm이 공통 `AppServices.Api`를 사용하는 HTTP 조회 서비스를 주입합니다. 기본 TasksControl·미리보기 생성자는 미구성 서비스를 유지하고 테스트 대역은 명시적으로 주입합니다. 서버 오류를 샘플 목록으로 대체하지 않습니다.

`GET /api/v1/tasks?scope=own|managed&status=...`와 `GET /api/v1/tasks/{id}`를 제공합니다. 내 업무는 직원·관리자의 현재 본인 배정만, 관리 업무는 해당 업무에 지정된 관리자만 조회합니다. 직접 상세 접근도 서버에서 검사하며 관리자 역할만으로 전체 업무를 열람하지 않습니다. 첫 연결은 전체 목록 반환이며 페이지·검색 조건은 아직 지원하지 않습니다.

같은 조회 조건으로 다시 조회하면 선택 업무를 유지합니다. 상세 실패 후에는 해당 상세만 재시도하고 F5로는 전체 목록을 다시 받습니다. 범위·상태 변경 시 선택을 초기화하며, 접속 변경 취소는 이전 데이터를 지우고 취소 안내로 구분합니다. 요청한 상태와 다른 목록 응답은 정상 결과로 표시하지 않습니다. 이 개선은 API·DB·업무 상태 전환 규칙을 변경하지 않습니다.

업무 API는 서버 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`로 명시적으로 활성화합니다. **삭제 가능한 개발·테스트 DB에서만** 활성화·초기화합니다. 1번이 예약한 `002_task_queries.mysql.sql`은 [검토용 DDL·검증/복구안](RemoteWork.Server/migrations/002_task_queries.md)으로 준비하며, 공유 적용 승인이나 버전2 구현을 뜻하지 않습니다. 현재 공통 버전1과 기본 비활성화를 유지합니다. 기본 서버의 미등록 업무 경로는 연결 필요로 표시합니다. 일반 실행에 업무 데이터를 자동 생성하지 않으며 업무 등록·배정·관리자 지정 저장 기능도 이번 조회 범위에는 없습니다.

업무 상태는 **진행전 / 진행중 / 제출완료 / 승인완료 / 반려완료**입니다. 배정→진행전, 수락→진행중, 정식 제출·재제출→제출완료, 검토 판단→승인완료/반려완료를 기준으로 합니다. 모든 정식 제출은 승인·반려를 거쳐야 하며 승인완료만 최종 완료입니다. 관리자 본인 업무도 같은 흐름을 사용하고 상위 검토자가 판단합니다.

조회 계약은 [TASK_QUERIES](docs/TASK_QUERIES.md)에 정리했습니다. 5개 문자열 코드·DATE/DateOnly 형식·현재 상세403/업무 전용404 구분은 1번 수용 기준입니다. 반려 후 복귀·제출/판단 트랜잭션·마감 시간대와 시각·상위 검토자 매핑·자동 검토 요청은 합의 필요입니다. **마감 경과에 따른 자동 결과 제출은 제외**합니다.

## 검증

Windows에서 소스 루트 기준으로 실행합니다.

```powershell
dotnet build .\RemoteWork.sln -c Release
dotnet run --project .\Tests\ApiClient.Tests\ApiClient.Tests.csproj -c Release
dotnet run --project .\Tests\Collaboration.Tests\Collaboration.Tests.csproj -c Release
dotnet run --project .\Tests\Notices.Tests\Notices.Tests.csproj -c Release
dotnet run --project .\Tests\TaskQueries.Tests\TaskQueries.Tests.csproj -c Release
dotnet run --project .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release
```

서버 테스트는 `RemoteWork.Server`에서 개발 의존성을 설치한 뒤 실행합니다. MySQL 동시성 테스트는 삭제 가능한 별도 `remote_work_test_` DB를 지정해야 합니다.

```powershell
python -m pip install -r requirements-dev.txt
python -m pytest -q
```

| 테스트 | 범위 |
|---|---|
| [ApiClient.Tests](Tests/ApiClient.Tests/README.md) | 접속 세대·취소·이전 응답·본문 형식별 401 등 C# 인증 회귀 |
| [Collaboration.Tests](Tests/Collaboration.Tests/README.md) | 실제 WinForms 작성·대상 필터·공지·로컬 채팅 회귀 |
| [Notices.Tests](Tests/Notices.Tests/README.md) | 공지 HTTP 대역·비동기 저장·WinForms, 선택적 격리 HTTP/SQLite 실행기 |
| [TaskQueries.Tests](Tests/TaskQueries.Tests/README.md) | 명시적 조회 대역과 실제 업무 화면의 목록·상세·필터·오류·응답 순서 |
| [TaskQueries.Http.Tests](Tests/TaskQueries.Http.Tests/README.md) | 공통 HTTP 어댑터 응답·오류·취소, opt-in 실제 HTTP와 WinForms 조회 |
| `RemoteWork.Server/tests` | 공통 인증·사용자·권한·웹·감사 이력·MySQL 관리자 동시 변경 및 업무 조회 권한. 예약 DDL의 전용 MySQL 검증은 [별도 opt-in 절차](RemoteWork.Server/migrations/002_task_queries.md)를 사용 |
| [RemoteWork.ClientChecks](Tests/README.md) | 별도 테스트 계정으로 실행하는 실제 C#→공통 서버→DB HTTP 점검 |

실행 결과와 사용한 커밋은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG)에 기록합니다. 테스트 대역·로컬 메모리·공통 HTTP 통과를 실제 업무 통합 완료로 표시하지 않습니다. 서버·실제 업무 권한·두 PC·운영 DB의 검증 범위를 각각 구분합니다. 기존 Preview는 이전 화면 검토 자료이며 현재 인증 화면과 같다고 해석하지 않습니다.

2번 조회는 [임시 HTTP/SQLite 통합 실행기](Tests/TaskQueries.Http.Tests/README.md)로 검증할 수 있습니다. 테스트 프로젝트를 Release 빌드한 뒤 `Tests/TaskQueries.Http.Tests/run_http_check.py`를 서버용 Python으로 실행하면 임시 DB·계정·실서버를 준비하고 직원·관리자 WinForms의 정확한 조회 집합/정렬·전체/5상태·행 선택·접속 폐기를 검사합니다. 지정 관리자 회수·재배정 후 조회 권한도 임시 fixture에서만 확인합니다. 실행 후 임시 서버·DB를 정리하며, 일반 실행의 업무 자동 생성·쓰기 API·공유 DB·MySQL/두 PC 검증을 뜻하지 않습니다.

## 브랜치와 게시 기준

| 브랜치·태그 | 목적 |
|---|---|
| `alpha` | 현재 통합 개발 코드·화면 |
| `LOG` | 담당별 구현·검토·병합 이력 |
| `PR` | 담당별 수정 요청·회신·재검토 결과 |
| `DOCX` | 기획·설계·합의 문서 |
| `0.0.1V` / `0.0.2V` | 각각 초기 코드 배치·기존 Designer 화면/조회 준비 버전 보존 |

코드 브랜치의 루트 `DOCX/`는 `.gitignore`의 `/DOCX/`로 제외합니다. 로컬 문서는 유지하고 DOCX 브랜치는 별도 요청 시 갱신합니다. 실제 `.env`·비밀번호·토큰·DB·가상환경·bin·obj·임시 검증 결과는 게시하지 않습니다. 푸시할 때마다 해당 브랜치 README를 실제 구현·파일 위치·검증 결과에 맞춰 최신화합니다.

다른 담당이 업무·상태·마감 영역을 수정하거나 기능을 연결할 때는 [2번 영역 수정 협업 규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_2번_영역수정_협업규약.md)을 따릅니다. 담당별 파일·공유 접점, 유지할 조회 계약, 공동 협의가 필요한 변경, 검증 범위와 PR 전달 양식을 정리했습니다. 기존 계약을 유지하는 수정과 API·권한·상태·DB 변경을 구분하며, 미확정 제출/검토·협업·장치 규칙은 관련 담당과 먼저 맞춥니다.
