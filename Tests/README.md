# C# 공통 통신 점검

실제 실행 중인 테스트 서버에 임시 계정 `smokeadmin`(관리자), `smokestaff`(직원)를 생성한 뒤 다음 환경변수를 설정합니다. 일반 사용자 비밀번호를 넣지 말고 별도 테스트 계정만 사용하세요. 이 도구는 접속 증표를 출력하지 않습니다.

```powershell
$env:REMOTEWORK_API_URL='http://127.0.0.1:5000'
$env:SMOKE_ADMIN_PASSWORD='별도_테스트_관리자_비밀번호'
$env:SMOKE_STAFF_PASSWORD='별도_테스트_직원_비밀번호'
$env:SMOKE_QA_DIR=Join-Path $env:TEMP 'RemoteWork-ClientChecks'
New-Item -ItemType Directory -Path $env:SMOKE_QA_DIR -Force
dotnet run --project .\Tests\RemoteWork.ClientChecks\RemoteWork.ClientChecks.csproj
```

점검 내용: 서버와 DB 연결, 관리자·직원 로그인, 관리자 사용자 목록, 직원의 직접 관리자 접근 차단, 로그아웃, 잘못된 응답·연결 실패 안내, 주소 검증. 화면 6개도 화면 밖에서 생성해 렌더링합니다. UI 이벤트를 실제 사람이 모두 누르는 자동화 테스트는 아니며, 스크린샷은 배치 확인용입니다.

서버 테스트는 `RemoteWork.Server/tests/`에 있으며 실행법은 `docs/SETUP.md`를 참고하세요.

## PR #2 인증 회귀 검증

[ApiClient.Tests](ApiClient.Tests/README.md)는 실제 공통 C# 소스에 응답 제어 HTTP 처리기를 주입해 이전 접속 응답·로그아웃/재로그인·본문 형식별 401 처리를 확인합니다. 실제 HTTP·MySQL 점검과 구분해 실행합니다.

```powershell
dotnet run --project .\Tests\ApiClient.Tests\ApiClient.Tests.csproj -c Release
```

서버의 `test_admin_concurrency.py`는 관리자 B의 인증을 완료한 뒤 `admin_guard` 잠금 직전에 대기시키고 A가 B를 강등·비활성화한 후 B 요청을 재개합니다. 실제 MySQL `REPEATABLE-READ`에서 최종 활성 관리자, 거절 응답과 성공한 변경의 감사 이력을 검증합니다. SQLite에서는 이 2개 동시성 테스트를 skip하며, 단일 관리자 보호·변경 rollback 테스트는 양쪽에서 실행합니다. `MYSQL_TEST_URL`은 삭제 가능한 `remote_work_test_` 전용 DB에만 설정합니다.

## 2번 업무 상태 CHECK·예약 DDL 검증

제품 DB의 구현·지속 관리·주요 검증은 MySQL 기준입니다. SQLite는 DEMO 임시 구성으로 종료 후 삭제할 예정이며, 아래 SQLite 검사는 이미 준비한 데모 확인 범위입니다. SQLite 전용 기능이나 별도 스키마 관리 대상으로 확대하지 않습니다.

`RemoteWork.Server/tests/test_task_migration_draft.py`는 SQLite ORM·MySQL ORM·실제 MySQL 002 SQL의 상태 CHECK를 구분해 검증합니다. 다섯 소문자 코드의 INSERT/UPDATE는 허용하고 대문자·혼합 대소문자·앞뒤 공백·탭·NUL·유사 문자열은 DB 제약으로 거절하는지 확인합니다. SQLite에서는 정규 코드와 바이트가 같은 bytes·bytearray·memoryview를 BLOB로 저장하는 경우도 거절합니다. API의 상태 전환 구현이나 3번 제출 정책 검증을 뜻하지 않습니다.

```powershell
# RemoteWork.Server 폴더, 서버 개발 의존성이 설치된 Python
python -m pytest -q tests/test_task_migration_draft.py tests/test_task_queries.py
```

기본 실행은 SQLite ORM 상태 검사와 읽기 회귀를 수행하고 MySQL 항목은 생략합니다. MySQL ORM·raw DDL·실제 백업/복구는 [002의 두 설정 opt-in 절차](../RemoteWork.Server/migrations/002_task_queries.md)가 필요합니다. 자신이 생성한 고유 DB에서만 수행하며 공통 버전1·기본 비활성화·공유 DB 미적용을 유지합니다. MySQL 검사 통과를 공유 DB나 두 PC 검증으로 표시하지 않습니다.
