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
