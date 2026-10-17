# 원격 근무용 업무 처리 확인 시스템

원격 근무 중 업무 배정·상태·마감·결과 검토와 공지·채팅·근태를 관리하는 팀 프로젝트입니다. `alpha`는 화면 초안과 담당별 구현을 통합하는 개발 브랜치입니다. C# WinForms 클라이언트와 Python 공통 서버를 포함합니다.

현재 공통 로그인·사용자 관리·HTTP·DB 기반과 공지·채팅의 로컬 동작이 구현돼 있습니다. 업무 목록·상세는 조회 인터페이스와 명시적 테스트 대역까지 준비했으며 실제 업무 서버 조회는 미연결입니다. 설계·합의 사항은 [DOCX](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/DOCX/DOCX), 구현·검토 이력은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG), 담당별 수정 요청과 재검토 결과는 [PR](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/PR)에서 관리합니다.

## 현재 구현 범위

| 담당 | 영역 | 현재 동작 |
|---|---|---|
| 1번 | 공통 서버·DB·인증·사용자·통신 | 실제 로그인·역할 진입, 사용자 관리·비밀번호 변경, 접속 만료·폐기, 공통 응답·오류·트랜잭션·감사 이력, 웹 로그인·사용자 목록 |
| 2번 | 업무·상태·마감 | 기존 화면 구조와 조회 경계 유지. 주입된 서비스의 행 선택 상세·전체/5개 상태 필터·로딩·빈 목록·오류·재시도·늦은 응답 차단 검증. 기본 서비스는 미연결 안내 |
| 3번 | 결과물·제출·검토·이력 | 직원·관리자 내 결과물과 관리자 검토 화면 예시. 실제 제출·파일·승인/반려·이력 저장은 미연결 |
| 4번 | 공지·채팅 | 메모리 공지 등록·조회·관리자 삭제·4개 대상 필터·고정·정렬, 로컬 개인/그룹 방·메시지·메뉴 이동 시 저장소 유지 |
| 5번 | 출퇴근·연차·장치 | 기존 폼과 역할별 화면 예시. 실제 근태 저장·승인·장치 인증은 미연결 |

공통 권한 도우미의 동작 검증은 실제 업무·채팅·파일 API의 접근 권한 검증과 구분합니다. 실제 업무 API, Own/Managed 관리 범위, 제출·승인, 파일 전송과 두 PC·웹 업무 흐름은 후속 합의·구현·검증 대상입니다.

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

로그인한 서버 사용자의 역할에 따라 직원·관리자 화면으로 진입합니다. 로그인 화면의 기존 미리보기 버튼 위치는 서버 연결 설정과 웹 로그인 열기에 사용합니다. 지문 로그인은 5번 담당 연결 전 안내 상태입니다. 회사명은 확정 전까지 `회사명`으로 표시합니다.

공통 API는 Bearer 인증, snake_case 필드와 UTC ISO 8601 기록 시각을 사용합니다. `ApiClient`는 접속 세대·서버·토큰을 요청에 연결하여 이전 접속의 늦은 응답이 새 로그인 상태를 바꾸지 않게 합니다. 현재 접속의 401은 본문 JSON 형식과 관계없이 인증 만료로 처리합니다. 공통 관리자 변경은 잠금 뒤 수행자·접속·관리자 상태를 다시 확인합니다.

실제 API·오류·DB·기능 등록 방법은 [INTEGRATION](docs/INTEGRATION.md)을 따릅니다. 문서의 업무 API 예시는 실제 등록된 업무 API가 아닙니다.

## 공지·채팅의 저장 범위

공지 작성 대상과 목록 필터는 전 직원·기획팀·개발팀·디자인팀입니다. `전체`는 모든 대상이며 각 대상 필터는 그 대상으로 작성된 공지만 표시합니다. 선택한 공지의 본문은 조회 버튼으로 표시하고, 상단 고정을 먼저 정렬하며 관리자 우클릭 삭제를 제공합니다.

공지 목록은 프로세스 메모리에만 보관되고 프로그램 종료 시 사라집니다. 채팅 저장소는 MainForm 단위여서 메뉴 이동 중에는 유지되지만 메인 창을 닫으면 사라집니다. 개인/그룹 방 생성·삭제, 참여자·메시지 조회, Enter 전송과 Shift+Enter 줄바꿈을 제공합니다.

현재 공지·채팅 작성자와 대상은 로컬 예시이며 실제 인증 사용자와의 매핑·서버 권한·DB 저장·두 PC 전송은 미연결입니다. 첨부는 원본 경로·파일명 선택과 표시만 지원합니다. 실제 업로드·다운로드, 중요 공지 자동 팝업 배포와 직원별 확인 기록은 후속 기능입니다. 상단 고정은 자동 팝업 조건과 구분합니다.

## 업무 조회와 상태 기준

`ITaskQueryService`·`TaskSummary`·`TaskDetail`을 조회 경계로 사용합니다. 기본 `TasksControl`은 `UnconfiguredTaskQueryService`로 **조회 연결 필요**를 표시합니다. `TaskQueryTestDouble`은 생성자에 명시적으로 주입하며 화면에 실제 서버 연결이 아닌 테스트 대역임을 표시합니다. 서버 오류를 샘플 목록으로 대체하지 않습니다.

업무 상태는 **진행전 / 진행중 / 제출완료 / 승인완료 / 반려완료**입니다. 배정→진행전, 수락→진행중, 정식 제출·재제출→제출완료, 검토 판단→승인완료/반려완료를 기준으로 합니다. 모든 정식 제출은 승인·반려를 거쳐야 하며 승인완료만 최종 완료입니다. 관리자 본인 업무도 같은 흐름을 사용하고 상위 검토자가 판단합니다.

실제 업무 API·wire 필드·상태 저장 코드·날짜·정렬·페이지·조회 범위를 합의한 뒤 2번이 HTTP 구현을 연결합니다. 서버는 인증 사용자 기준으로 목록과 직접 상세 접근을 각각 검사해야 합니다. 반려 후 복귀 시점·상위 검토자 매핑·자동 검토 요청은 합의 필요입니다. **마감 경과에 따른 자동 결과 제출은 제외**합니다.

## 검증

Windows에서 소스 루트 기준으로 실행합니다.

```powershell
dotnet build .\RemoteWork.sln -c Release
dotnet run --project .\Tests\ApiClient.Tests\ApiClient.Tests.csproj -c Release
dotnet run --project .\Tests\Collaboration.Tests\Collaboration.Tests.csproj -c Release
dotnet run --project .\Tests\TaskQueries.Tests\TaskQueries.Tests.csproj -c Release
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
| [TaskQueries.Tests](Tests/TaskQueries.Tests/README.md) | 명시적 조회 대역과 실제 업무 화면의 목록·상세·필터·오류·응답 순서 |
| `RemoteWork.Server/tests` | 공통 인증·사용자·권한·웹·감사 이력·MySQL 관리자 동시 변경 |
| [RemoteWork.ClientChecks](Tests/README.md) | 별도 테스트 계정으로 실행하는 실제 C#→공통 서버→DB HTTP 점검 |

실행 결과와 사용한 커밋은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG)에 기록합니다. 테스트 대역·로컬 메모리·공통 HTTP 통과를 실제 업무 통합 완료로 표시하지 않습니다. 서버·실제 업무 권한·두 PC·운영 DB의 검증 범위를 각각 구분합니다. 기존 Preview는 이전 화면 검토 자료이며 현재 인증 화면과 같다고 해석하지 않습니다.

## 브랜치와 게시 기준

| 브랜치·태그 | 목적 |
|---|---|
| `alpha` | 현재 통합 개발 코드·화면 |
| `LOG` | 담당별 구현·검토·병합 이력 |
| `PR` | 담당별 수정 요청·회신·재검토 결과 |
| `DOCX` | 기획·설계·합의 문서 |
| `0.0.1V` / `0.0.2V` | 각각 초기 코드 배치·기존 Designer 화면/조회 준비 버전 보존 |

코드 브랜치의 루트 `DOCX/`는 `.gitignore`의 `/DOCX/`로 제외합니다. 로컬 문서는 유지하고 DOCX 브랜치는 별도 요청 시 갱신합니다. 실제 `.env`·비밀번호·토큰·DB·가상환경·bin·obj·임시 검증 결과는 게시하지 않습니다. 푸시할 때마다 해당 브랜치 README를 실제 구현·파일 위치·검증 결과에 맞춰 최신화합니다.
