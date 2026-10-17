# 공지 저장·조회 검증

## 현재 검토 범위 — 2026-10-17

공지 API·ID·권한은 병합된 PR #12의 N4 회신을 반영합니다. 서버의 새 `tests/test_notice_migration_draft.py`는 notices를 ORM으로 생성하지 않고 실제 예약003 SQL로 생성해 타입/FK/default·기존 행/버전·실패·백업/복원을 검사합니다. 접속은 별도 opt-in/전용 URL만 허용합니다. [SQL 검증 절차](../../RemoteWork.Server/migrations/003_notices.md)를 따릅니다.

이 문서의 `run_http_check.py`는 임시 SQLite 실행기입니다. 이번 작성자는 별도 격리 MySQL과 raw003 위에서 같은 C# 검증을 추가 실행했으며 두 경로를 구분해 LOG에 기록합니다. 실제 공유 DB·두PC·SQL 최종 승인·버전 지원·팝업/확인/파일 전송 완료를 뜻하지 않습니다. 이전 수치는 아래 당시 기록으로 보존합니다.

Windows/.NET 8에서 실행합니다. 기본 실행은 명시적 HTTP/서비스 대역과 실제 WinForms 메시지 루프로 동작하며 실제 DB에 연결하지 않습니다.

```powershell
dotnet run --project .\Tests\Notices.Tests\Notices.Tests.csproj -c Release
```

파일명 메타데이터 전송·작성자/경로 미전송·공통 Bearer·페이지 전체 읽기·상세 ID 검증, 저장 완료 전 OK 금지·중복 클릭 방지·저장 실패 입력 유지·재시도, 목록 갱신/실패/복구·화면 종료 뒤 늦은 응답을 확인합니다. 사람의 마우스/키보드 입력이나 육안 검수는 아닙니다.

실제 C# 작성 화면→HTTP→서버→SQLite와 직원 조회·삭제 권한을 검증하려면 먼저 Release 빌드 후 프로젝트 루트에서 다음을 실행합니다. 서버 개발 의존성이 설치된 Python을 사용합니다.

```powershell
python .\Tests\Notices.Tests\run_http_check.py --client-dll .\Tests\Notices.Tests\bin\Release\net8.0-windows\Notices.Tests.dll
```

실행기는 임시 디렉터리의 별도 SQLite·임시 포트·합성 계정만 사용합니다. 사용자 .env 로드를 차단하고 기존 서버·DB 설정과 운영 데이터에 연결하지 않습니다. 종료 시 자기 서버를 닫고 임시 DB를 정리합니다. 고정 합성 암호는 테스트 전용이며 실제 계정으로 생성하지 않습니다.

서버 저장·재시작 유지 등 pytest 검증은 `RemoteWork.Server`에서 아래처럼 실행합니다. `MYSQL_TEST_URL`이 설정돼 있으면 기존 공통 fixture가 해당 테스트 DB를 비우므로 기본 SQLite 검증에서는 제거합니다.

```powershell
$env:PYTHON_DOTENV_DISABLED = '1'
Remove-Item Env:MYSQL_TEST_URL -ErrorAction SilentlyContinue
python -m pytest -q
```

실제 MySQL·공유 서버·두 PC·업로드/다운로드·직원 자동 팝업·확인 기록은 이 실행기의 검증 범위가 아닙니다.


최신 alpha 등록 방식: 서버 공지는 `REMOTEWORK_ENABLE_NOTICE_DRAFT=1`에서만 활성화됩니다. Python HTTP 실행기는 자신의 테스트 프로세스에 이 플래그를 설정합니다. 공지 pytest 모듈의 autouse fixture도 테스트 중에만 켜고 복구합니다. 최초 PR 준비 때는 재실행하지 않았으며, 이후 사용자 요청으로 아래 최신 결합 검증을 실행했습니다.


2026-10-17 최신 alpha add1921 + 공지 bb41248 결합 검증: C# 기본30검증, 별도 실제 HTTP/SQLite37검증 및 실제 HTTP/격리 MySQL37검증 통과. 37개에는 공통 대역/화면 검증이 재포함되므로 실행 간 합산하지 않습니다. 서버 SQLite66통과/14생략, MySQL79통과/1환경실패 후 mysqldump 경로 지정으로 실패1개 재검증 통과. Release 빌드는 경고0/오류0입니다. 실제 공유 DB·두 PC·팝업 검증을 의미하지 않습니다.

그 뒤 새 alpha 3b09480의 로그인 화면·실행 안내 변경을 보존해 추가 결합했습니다. 앱과 C# 검증 프로젝트5개를 다시 Release 빌드(경고0/오류0)하고 ApiClient88·Collaboration85·Notices30·TaskQueries82·TaskQueries.Http103, 실제 HTTP/SQLite37검증을 재실행해 모두 통과했습니다. 서버 소스와 MySQL 경로는 이전 검증본과 같음을 확인했으며, 해당 전체 pytest/MySQL 수치는 위 add1921 결합본 실행 결과입니다. 최신 결합에서 실제 로그인·두 PC·장치 검증은 실행하지 않았습니다.
