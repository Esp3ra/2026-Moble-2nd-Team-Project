# 원격 근무용 업무 처리 확인 시스템

원격 근무 중 업무 배정·상태·마감·결과 검토와 공지·채팅·근태를 관리하는 팀 프로젝트입니다. `alpha`는 화면 초안과 담당별 구현을 통합하는 개발 브랜치입니다. C# WinForms 클라이언트와 Python 공통 서버를 포함합니다.

현재 공통 로그인·사용자 관리·HTTP·DB 기반과 공지 DB 저장 연결 개발 초안, 채팅 DB 저장·개인 목록 숨김의 개발 검토안이 구현돼 있습니다. 업무 목록·상세는 공통 인증을 사용하는 GET API·HTTP 서비스와 기존 화면을 연결한 2번 개발 초안입니다. 업무 API는 명시적으로 활성화한 개발 서버에서 사용하며, 기본 실행에는 미확정 업무 스키마를 적용하지 않습니다. 설계·합의 사항은 [DOCX](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/DOCX/DOCX), 구현·검토 이력은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG), 담당별 수정 요청과 재검토 결과는 [PR](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/PR)에서 관리합니다.

## 현재 구현 범위

| 담당 | 영역 | 현재 동작 |
|---|---|---|
| 1번 | 공통 서버·DB·인증·사용자·통신 | 실제 로그인·역할 진입, 사용자 관리·비밀번호 변경, 접속 만료·폐기, 공통 응답·오류·트랜잭션·감사 이력, 웹 로그인·사용자 목록, 개발용 W1 직원 쓰기 guard·후보/문맥·버전·중복 요청 공통 지원 |
| 2번 | 업무·상태·마감 | 기존 화면·조회 경계를 유지한 GET 목록/상세·전체/5상태 필터·서버 본인/지정 관리자 권한·공통 HTTP 연결. 개발용 GET·별도 opt-in 신규 등록 서버 API. 실제 WinForms 저장·수락/재작업/마감 변경은 미연결 |
| 3번 | 결과물·제출·검토·이력 | 직원·관리자 내 결과물과 관리자 검토 화면 예시. 실제 제출·파일·승인/반려·이력 저장은 미연결 |
| 4번 | 공지·채팅 | 명시적 개발 활성화 시 공지 DB 등록·조회·관리자 삭제, 대상 필터·고정·정렬. 채팅은 활성 사용자 초대·서버 DB 방/메시지 저장·본인 목록 숨김 개발 검토안. 공동 계약·공유 적용은 검토 대기 |
| 5번 | 출퇴근·연차·장치 | 독립 라파 지문·조도 API, 메가 A0 송신, Windows 장치 점검 도구와 자동 실행 예제. 기존 근태 폼·공통 회원/인증·DB 저장 연결은 별도 |

업무 조회는 현재 본인 배정과 업무별 지정 관리자 조건을 서버에서 각각 검사합니다. 2026-10-17 [1번 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_업무조회_API_DB_규약확정_회신.md)으로 조회 API·DTO·권한·오류와 상태/날짜 표현·공통 FK 기준을 수용했습니다. 상태 흐름의 사용자 확정 사항과 별도로 세부 쓰기 계약·마감 의미·제출 연결은 1·3번과 합의 필요이며, 실제 DDL 승인·공통 버전 지원·공유 DB 적용·두 PC 통합은 별도입니다.

W1 공통 쓰기 지원은 [WRITE_SUPPORT](docs/WRITE_SUPPORT.md)를 따릅니다. 명시적 개발 활성화에서 직원 쓰기 세션 재검사·사용자 후보/업무 문맥·서명 요청 키와 성공 결과 보관·C# 명시적 키 전송을 제공합니다. 2번의 [신규 등록 개발 API](docs/TASK_CREATION.md)는 이 공통 지원을 재사용합니다. 실제 UI 저장·수락·연장은 다음 연결 및 상태/마감 계약 확인 후 구현합니다. 공통 schema version1·기본 비활성화를 유지하며 공유 DB 적용과 구분합니다.

2번은 W1에 [후보·쓰기 준비 연결](docs/TASK_WRITE_PREPARATION.md)을 추가했습니다. 현재 업무의 ID·버전·배정 정보와 담당자/업무 관리자 검색·페이지·선택을 실제 HTTP로 조회합니다. 선택값은 로컬에 보관합니다. 신규 등록 서버는 별도 개발 opt-in으로 추가했으며 WinForms POST·수락·연장 연결은 다음 단계입니다.

신규 업무의 관리자 기준은 **최소 1명·등록자 자동 포함·최초 등록 시 등록자 제외 불가**입니다(2026-10-19 사용자 확정). 선택 준비 화면에 등록자를 필수 관리자로 표시하고 해제를 막습니다. 추가 관리자는 기존 후보에서 선택하며 제출 검토자는 별도입니다. 별도 개발 등록 API도 현재 후보·등록자를 다시 검사하고 DB에 저장합니다. WinForms 입력 확인·선택은 아직 실제 등록 POST를 호출하지 않습니다. 기존 업무의 관리자 변경은 별도 합의 대상입니다. 반려 후 흐름은 **반려완료 → 재작업 시작 → 진행중 → 재제출 성공 → 제출완료**로 확정했습니다. 재작업 시작에서는 제출 회차를 올리지 않습니다. [3번 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-19_3번_재작업상태_확정_및_연결회신.md)의 담당 경계를 따르며 실제 재작업/제출 API와 마감·검토자 정책은 별도 구현·합의가 필요합니다.

팀장 공통 기준과 담당별 회신은 [LOG/2번 공통규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md), 현재 구현·실행/미실행은 [통합 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)에서 확인합니다. 1번 부재 중 공통 코드를 대신 변경하지 않았으며, 초기 배정/업무 버전 1/1은 2번 개발 계약·공동 기술 검토 전입니다.

## 5번 장치 독립 시연

[장치 설치·실행·API·검증 범위](Devices/RaspberryPi/README.md)에 지문 센서·SH1106 OLED·메가 조도 송신과 systemd 자동 실행 예제를 정리했습니다. [Windows 점검 도구](Tools/DeviceCheck/README.md)는 공통 WinForms와 별도로 실행하며 조도 조회와 수동 지문 요청을 제공합니다. 실제 표시 이름 설정·센서 이벤트 CSV·장치 원본 데이터는 게시하지 않습니다.

가림 감지와 최근 변화 이력을 구분하며 손가락 그림자로 움직임을 모의실험합니다. 재석/이석·출퇴근을 자동 판정하지 않습니다. 센서 슬롯과 로그인 회원의 연결·장치 신뢰·인증 시점·공통 API/DB는 1번·5번 및 팀장과 합의 후 통합합니다. 이 장치 API의 시각/응답 형식은 독립 시연용이고 기존 공통 인증·UTC·업무 계약을 대체하지 않습니다.

## 기술과 실행 환경

- 데스크톱: Windows, .NET 8 SDK, C# WinForms.
- 서버: Python 3.11 이상, Flask/Waitress, SQLAlchemy/PyMySQL.
- DB: 실제 구현·DB 규약·지속 관리와 주요 검증은 MySQL 기준입니다. SQLite는 DEMO용 임시 구성이며 DEMO 종료 후 삭제할 예정입니다.
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

2번 쓰기 연결용 [W1 공통 지원](docs/WRITE_SUPPORT.md)은 기본 비활성 개발 초안이며, 실제 업무 저장 API와 구분합니다.

## 공지 DB 개발 초안과 채팅 저장 범위

공지 작성·목록·상세·관리자 삭제를 공통 인증 서버와 notices 테이블에 연결합니다. 전 직원·기획팀·개발팀·디자인팀을 target으로 구분하고 사용자 결정에 따라 직원도 모든 부서를 조회합니다. 작성자·UTC 시각은 서버에서 결정하며 저장 성공 때만 작성 창을 닫고 오류 시 입력을 유지합니다. 목록/대상 필터·고정 우선·열 정렬·조회 버튼 상세를 유지합니다. 정적 샘플 공지는 제거했고 종료 후 유지 여부는 연결한 DB가 담당합니다.

공지 API/ID/권한은 PR #12의 1번 수용 기준이며 **기본 off**를 유지합니다. 프로젝트 루트의 PowerShell에서 `$env:REMOTEWORK_ENABLE_NOTICE_DRAFT = "1"`을 지정하고 `& .\Start-Demo.cmd`를 실행하면 별도 SQLite 데모에 공지 테이블을 준비합니다. 기존 업무 플래그와 독립적으로 등록하며 공통 인증·스키마 버전1·업무 코드는 보존합니다. 실제 격리 MySQL 검증을 수행했고 migration003은 번호 예약에 따른 SQL 검토안입니다. 공유 적용·공통 버전 지원·정식 활성화는 대기입니다. [공지 준비·API·제한](RemoteWork.Server/remotework/features/notices/README.md)을 따릅니다.

첨부는 파일명 메타데이터만 저장하고 원본 절대경로·파일 본문을 서버에 보내지 않습니다. 실제 업로드/다운로드·중요 공지 자동 팝업·직원별 확인 기록과 정렬 추가 조정은 이번 DB 변경에 포함하지 않습니다.

채팅은 실제 로그인 경로에서 공통 HTTP 서비스를 통해 활성 사용자·방·메시지를 조회하고 DB에 저장하는 개발 검토안입니다. 본인 목록 삭제는 상대 기록을 유지합니다. 기본 비활성화이며 API·DDL 공동 수용·공유 적용은 대기입니다. 미리보기의 메모리 예시와 구분하고, 새 메시지 자동 수신은 포함하지 않습니다.

## 업무 조회와 상태 기준

`ITaskQueryService`·`TaskSummary`·`TaskDetail`을 유지하고 실제 로그인 MainForm이 공통 `AppServices.Api`를 사용하는 HTTP 조회 서비스를 주입합니다. 기본 TasksControl·미리보기 생성자는 미구성 서비스를 유지하고 테스트 대역은 명시적으로 주입합니다. 서버 오류를 샘플 목록으로 대체하지 않습니다.

`GET /api/v1/tasks?scope=own|managed&status=...`와 `GET /api/v1/tasks/{id}`를 제공합니다. 내 업무는 직원·관리자의 현재 본인 배정만, 관리 업무는 해당 업무에 지정된 관리자만 조회합니다. 직접 상세 접근도 서버에서 검사하며 관리자 역할만으로 전체 업무를 열람하지 않습니다. 첫 연결은 전체 목록 반환이며 페이지·검색 조건은 아직 지원하지 않습니다.

같은 조회 조건으로 다시 조회하면 선택 업무를 유지합니다. 상세 실패 후에는 해당 상세만 재시도하고 F5로는 전체 목록을 다시 받습니다. 범위·상태 변경 시 선택을 초기화하며, 접속 변경 취소는 이전 데이터를 지우고 취소 안내로 구분합니다. 요청한 상태와 다른 목록 응답은 정상 결과로 표시하지 않습니다. 이 개선은 API·DB·업무 상태 전환 규칙을 변경하지 않습니다.

목록 조회 이후 같은 업무의 제목·담당자 표시명·상태·시작일·마감일이 달라진 상세를 받으면 변경 감지 안내와 `목록 새로고침`을 표시합니다. 최신 상세를 보여주고 목록은 조회 당시 결과로 유지합니다. 버튼이나 F5로 현재 조회 조건의 목록을 다시 받으며, 같은 조건의 선택 복원과 새 결과의 상태 필터를 따릅니다.

등록·수정은 기본 입력을 확인하고, 수락은 대상 업무를 읽기 전용으로 확인하며, 마감 연장은 실제 선택 업무의 현재 마감일과 희망일·사유 입력을 준비합니다. 수정·수락·연장 버튼은 현재 상세 조회 성공 후에만 열립니다. 입력 확인은 로컬 형식 검사이며 창에 값이 유지됩니다. 확인된 제목·내용·날짜와 원본 업무 ID는 불변 로컬 입력값으로 보관하고, 입력이 바뀌면 확인값을 무효화합니다. 날짜 체크의 변경 이벤트가 없더라도 읽는 시점에 현재 입력과 다시 대조합니다. 이 값은 서버 요청 DTO나 쓰기 계약이 아닙니다. [1번의 W1 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_업무쓰기_공통규약_회신.md)은 수신했으며 직원 쓰기 재인증·멱등성·버전 지원은 후속 W1 코드로 제공됩니다. 신규 등록 개발 API는 별도 계약과 opt-in으로 추가했으며 UI 저장·수락·접수·마감 변경은 2·3번 행동 계약 확인 후 연결합니다. 상세 범위와 담당별 요청 문서는 [입력 준비 안내](docs/TASK_INPUT_PREPARATION.md)를 따릅니다.

업무 API는 서버 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`로 명시적으로 활성화합니다. **삭제 가능한 개발·테스트 DB에서만** 활성화·초기화합니다. 1번이 예약한 `002_task_queries.mysql.sql`은 [검토용 DDL·검증/복구안](RemoteWork.Server/migrations/002_task_queries.md)으로 준비하며, 공유 적용 승인이나 버전2 구현을 뜻하지 않습니다. 현재 공통 버전1과 기본 비활성화를 유지합니다. 기본 서버의 미등록 업무 경로는 연결 필요로 표시합니다. 일반 실행에 업무 데이터를 자동 생성하지 않습니다. [신규 등록 API](docs/TASK_CREATION.md)는 기존 조회 opt-in에 추가로 `REMOTEWORK_ENABLE_TASK_CREATE_DRAFT=1`을 요구하며, 등록자 포함·최초 배정/관리자 저장을 별도 개발 DB에서만 제공합니다. 공유 적용·실제 UI 저장은 별도입니다.

W1-05 요청에 따라 002 SQL과 업무 ORM의 상태 CHECK는 기존 IN과 HEX 비교를 함께 사용해 정확한 다섯 소문자 코드만 허용합니다. DB collation이 대소문자·뒤 공백을 같게 비교해도 대문자·혼합·공백 변형을 거절하고, SQLite의 TEXT와 BLOB 구분도 유지합니다. 기존 공유 DB 제약의 자동 수정이나 상태 전환 API는 추가하지 않습니다.

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
| [TaskPreparation.Tests](Tests/TaskPreparation.Tests/README.md) | W1 후보·쓰기 문맥·키/결과 연결, 접속 변경·응답 순서·실제 후보 선택 화면. 별도 명시적 새 소유 MySQL 실행기 제공 |
| [ApiClient.Tests](Tests/ApiClient.Tests/README.md) | 접속 세대·취소·이전 응답·본문 형식별 401 등 C# 인증 회귀 |
| [Collaboration.Tests](Tests/Collaboration.Tests/README.md) | 실제 WinForms 작성·대상 필터·공지·로컬 채팅 회귀 |
| [Notices.Tests](Tests/Notices.Tests/README.md) | 공지 HTTP 대역·비동기 저장·WinForms, 선택적 격리 HTTP/SQLite 실행기 |
| [TaskQueries.Tests](Tests/TaskQueries.Tests/README.md) | 명시적 조회 대역과 실제 업무 화면의 목록·상세·필터·오류·응답 순서 |
| [TaskQueries.Http.Tests](Tests/TaskQueries.Http.Tests/README.md) | 공통 HTTP 어댑터 응답·오류·취소, opt-in 실제 HTTP와 WinForms 조회 |
| `RemoteWork.Server/tests` | 공통 인증·사용자·권한·웹·감사 이력·MySQL 관리자 동시 변경 및 업무 조회 권한. 예약 DDL의 전용 MySQL 검증은 [별도 opt-in 절차](RemoteWork.Server/migrations/002_task_queries.md)를 사용 |
| [RemoteWork.ClientChecks](Tests/README.md) | 별도 테스트 계정으로 실행하는 실제 C#→공통 서버→DB HTTP 점검 |

실행 결과와 사용한 커밋은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG)에 기록합니다. 테스트 대역·로컬 메모리·공통 HTTP 통과를 실제 업무 통합 완료로 표시하지 않습니다. 서버·실제 업무 권한·두 PC·운영 DB의 검증 범위를 각각 구분합니다. 기존 Preview는 이전 화면 검토 자료이며 현재 인증 화면과 같다고 해석하지 않습니다.

2번 조회의 MySQL 검증은 [전용 HTTP 실행기 안내](Tests/TaskQueries.Http.Tests/README.md)를 따릅니다. `run_mysql_http_check.py`는 명시적 실행 옵션과 MySQL 설치 경로를 받아 새 임시 MySQL 프로세스·datadir·DB·합성 계정·loopback API를 준비합니다. 공통7테이블과 검토용002 SQL로 만든 업무 테이블에서 기존 C# 직원·관리자 WinForms의 정확한 조회 집합/정렬·전체/5상태·행 상세·준비 창·접속 폐기를 검사합니다. 지정 관리자 회수·재배정은 fixture 안에서만 수행하며 GET 전후 업무/공통 행의 보존을 대조합니다.

```powershell
dotnet build .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskQueries.Http.Tests\run_mysql_http_check.py --allow-disposable-mysql --mysql-bin-dir "C:\Program Files\MySQL\MySQL Server 8.2\bin"
```

실제 `.env`·기존 DB 주소·서버 설정을 사용하지 않으며 자신이 생성한 서버·DB·임시 경로만 정리합니다. 제품 업무 등록·쓰기 API·정식 migration/버전 지원·공유 DB·두 PC 완료를 뜻하지 않습니다. 이전 SQLite 실행기는 DEMO 참고용으로 남기며 별도 제품 DB 관리 대상으로 확대하지 않습니다.

다음 쓰기 연결에 필요한 [2번 행동별 API·후보 사용자 선택 제안](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#r026)은 당시 제안 원문으로 보존합니다. 현재 개발용 신규 등록의 실제 경로·입력·결과는 [TASK_CREATION](docs/TASK_CREATION.md)을 따르며, 나머지 행동은 W1/W3 ID로 검토 중입니다.

[3번 W3 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/3번/README.md)의 현재 로컬 구현·제안을 수신했습니다. 상태별 수정·수락 예외·제출 트랜잭션·검토자·마감/연장 조건은 공동 확인이 남아 있습니다. 1번 공통 쓰기 지원과 실제 API/DB를 연결하기 전까지 로컬 시연의 제출 조건을 서버 규약으로 사용하지 않습니다. 기존 조회 서비스·필드와 미구현 접점은 [2번 조회 연결 안내](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#r015)에 정리합니다.

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

## 4번 채팅 저장 검토안 — 2026-10-19

[채팅 실행·정책·미실행 범위](RemoteWork.Server/remotework/features/chat/README.md)를 확인합니다. 로컬 개발 DB의 공통 테이블을 준비한 뒤 Init-Chat.cmd로 채팅 3테이블을 명시적으로 준비합니다. 공유 DB에서 실행하기 전 1번/팀장의 DDL·백업/복구·적용 승인이 필요합니다. 로그인 사용자는 HTTP 서비스를 사용하며 미리보기의 메모리 예시는 별개입니다. 삭제는 본인 목록 숨김이고 상대 기록은 유지합니다. 실시간 수신/주기적 자동 갱신·첨부파일은 제외하며 상대 메시지는 방 재선택/메뉴 재진입 시 조회합니다. 최초 제출 때 저장 검증을 실행하지 않았으며, PR21 수정 후 검증 결과는 아래 안내와 채팅 README를 따릅니다.

PR21 후속 수정: 채팅 기본 off. 개발 서버 .env의 REMOTEWORK_ENABLE_CHAT_DRAFT=1과 명시적 Init-Chat이 필요하다. 정상 사용자 정보 갱신을 세션 교체와 구분하고, 메시지는 서버와 동일한 Unicode code point 2,000자 기준으로 검사한다. 공동 API/DDL 수용·공유 적용은 대기다.

PR21 수정 검증: Release 경고/오류0, C# HTTP대역40·실제WinForms대역47, 서버28(실제MySQL18+등록/CLI 경계10), 별도 실제C#→TCP→MySQL15 검사를 직접 통과했습니다. 두PC·공유DB 적용·1번 공동 API/DDL 수용은 여전히 대기입니다. [재현 명령과 검증 구분](Tests/ChatStorage.Tests/README.md)을 확인합니다.

R21-04 후속: Init-Chat.cmd는 setlocal/endlocal로 채팅 활성화 값을 배치 안에 제한합니다. 기존 CMD에서 call로 실행해도 호출자의 미설정/0/1 값을 성공·실패 종료 후 보존하며, 서버 활성화는 별도로 명시합니다.
