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
