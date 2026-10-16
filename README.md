# 원격 근무 관리 시스템 — 공통 기반 통합

팀장 저장소 `alpha`의 `8f09183ef992606ec4c2b7e3da79ef07983ba019`를 기준으로 1번 담당의 공통 서버·DB·인증·사용자·통신을 통합한 코드입니다. 포크의 `team1/common-foundation`에서 팀장 저장소 `alpha`로 검토를 요청합니다. 설계 자료는 기존 DOCX 브랜치, 협의·검증 기록은 별도 LOG 브랜치의 루트 `1번/`에서 관리합니다.

## 구현한 범위

- Flask/Waitress, SQLAlchemy/PyMySQL, 공통 테이블·트랜잭션·감사 이력·기능 Blueprint 등록 지점.
- 직원·관리자 실제 로그인, 비밀번호 해시, Bearer 토큰·웹 쿠키·CSRF, 접속 만료·로그아웃·폐기.
- 관리자 사용자 생성·조회·수정·비밀번호 초기화·활성/비활성, 본인 비밀번호 변경.
- C# 공통 ApiClient, 서버 주소 설정, 실제 사용자 정보, 인증 만료·연결 상태 처리.
- 웹 로그인·내 정보·관리자 사용자 목록.

2번 담당의 `Features/Tasks` 전체와 조회 인터페이스·DTO·명시적 테스트 대역·`Tests/TaskQueries.Tests`는 기존 코드를 보존합니다. 기본 업무 조회는 여전히 **조회 연결 필요** 상태이며, 다른 담당 기능 화면은 예시입니다. 실제 업무 API·HTTP 어댑터·Own/Managed 서버 범위 검사·상태 변경·파일·제출·승인은 구현 대상이 남아 있습니다. 공통 권한 도우미는 실제 업무 관리 범위를 대신하지 않습니다.

## 실행

Windows, .NET 8 SDK, Python 3.11 이상이 필요합니다. 서버를 실행할 때 패키지 설치를 위한 인터넷 연결이 필요합니다.

1. 프로젝트 루트에서 `Start-Demo.cmd`를 실행합니다. SQLite 데모 계정의 최초 무작위 비밀번호는 서버 창에서 확인합니다.
2. 서버를 켠 상태에서 `Start-Desktop.cmd`를 실행하거나 다음 명령으로 시작합니다.

```powershell
dotnet run --project .\RemoteWork.WinForms\RemoteWork.WinForms.csproj
```

3. 웹은 `http://127.0.0.1:5000/login`에서 같은 계정으로 확인합니다.

팀 공동 MySQL 설정과 계정 발급은 [SETUP](docs/SETUP.md)을 따릅니다. `.env.example`만 공유하며 실제 `.env`·토큰·비밀번호·DB·가상환경·빌드 결과물은 저장소에 포함하지 않습니다. 클라이언트 주소에는 서버 루트만 입력합니다. 다른 PC의 `127.0.0.1`은 서버 PC를 가리키지 않습니다.

## 팀원 연결 자료

| 경로 | 내용 |
|---|---|
| [공통 규약](docs/INTEGRATION.md) | 실제 등록 API·인증·응답·오류·DB·기능 등록·C# 재사용 |
| [공통 요청 회신](docs/1번_공통규약_확인요청_회신.md) | 2번 A1~A4·B1~B5에 대한 구현과 미합의 구분 |
| [업무 조회 경계](RemoteWork.WinForms/Features/Tasks/Queries/README.md) | 2번 담당 원본 읽기 인터페이스·대역 안내 |
| [이번 병합 검증](docs/통합검증_2026-10-16.md) | 최신 팀 코드 보존과 재실행 결과 |
| [PR #2 수정 검증](docs/PR2_수정검증_2026-10-16.md) | 관리자 동시 변경·이전 접속 응답·본문 형식별 401 회귀 검증 |

## 검증 실행

```powershell
dotnet build .\RemoteWork.sln -c Debug
dotnet build .\RemoteWork.sln -c Release
dotnet run --project .\Tests\TaskQueries.Tests\TaskQueries.Tests.csproj -c Release
dotnet run --project .\Tests\ApiClient.Tests\ApiClient.Tests.csproj -c Release
cd .\RemoteWork.Server
python -m pip install -r requirements-dev.txt
python -m pytest -q
```

서버 테스트는 기본 임시 SQLite를 사용합니다. MySQL 테스트는 `remote_work_test_`로 시작하는 삭제 가능한 별도 DB에만 연결합니다. 기존 팀 DB를 테스트 대상으로 쓰지 않습니다. 공통 실제 HTTP 점검 도구는 `Tests/RemoteWork.ClientChecks`에 있으며 실행 조건은 [테스트 안내](Tests/README.md)에 있습니다.

기존 화면의 기능별 Form/Designer/resx 구조와 `ForceDesignerDPIUnaware` 설정을 유지합니다. `DOCX/`는 코드 게시 대상에서 제외하고 기존 원격 Preview는 이전 화면 검토 자료로 보존합니다. 이번 공통 인증 화면과 같다고 해석하지 않습니다. 0.0.1V·0.0.2V 태그는 기존 화면 버전을 유지합니다.

## PR #2 수정 요청 반영

팀장 [PR 브랜치의 1번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/1번/2026-10-16_PR2_공통기반_수정요청.md)을 기준으로 관리자 사용자 변경을 잠금 구간에서 재인증하고 현재 관리자 상태를 다시 조회하도록 수정했습니다. C#은 접속 세대·취소·늦은 응답 차단을 적용하고 현재 접속의 401을 JSON 파싱과 독립적으로 처리합니다. 취소된 이전 접속 요청은 공통 화면에서 무시합니다.

기존 Features와 조회 인터페이스·대역·테스트는 변경하지 않습니다. 최초 공통 검증 기록과 수정 전 결함 재현·수정 후 결과는 위 별도 검증 문서에서 구분합니다. 코드 PR은 [#2](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/2)를 계속 사용하고, PR #3의 기존 문서 병합이 코드 수정 승인·병합을 뜻하지는 않습니다.
