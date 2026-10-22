# 공통 인증 접속·401 회귀 검증

2026-10-21: `ApiClient.Session.DeleteAsync<T>(path, ct)`와 공통 클라이언트의 동일 메서드를 제공한다.
캡처 접속/취소/기존401 처리를 재사용하고 body·임의 W1 key·자동 재전송을 추가하지 않는다.
첨부 임시 삭제의 현재 소유/참조·결과불명 처리와 삭제 가능 여부는3번 API와2번 소비자가 검사한다.
29시나리오144검사에는 DELETE의 이전 접속·지연 성공/401·현재401·전송 실패1회·외부경로 거절이 포함된다.

Windows 또는 .NET 8 SDK 환경에서 실행합니다. 추가 테스트 패키지는 필요하지 않습니다.

```powershell
dotnet run --project .\Tests\ApiClient.Tests\ApiClient.Tests.csproj -c Release
```

실제 `ApiClient.cs`와 `ApiModels.cs`를 연결하고 응답 시점을 제어하는 HTTP 처리기를 주입합니다. 오래된 요청은 취소 신호를 일부러 무시하게 만들어 접속 세대 검사도 함께 검증합니다. 실제 서버·DB·WinForms 조작을 사용하는 테스트는 아닙니다.

- A의 사용자 조회를 지연한 뒤 로그아웃·B 로그인: 이전 성공/정상 JSON 401/HTML 401/잘못된 JSON 401/빈 401이 B의 사용자·Bearer·만료 이벤트에 영향을 주지 않는지 확인합니다.
- 현재 접속의 4가지 본문 401: 사용자·Bearer 제거, 후속 요청의 Bearer 미첨부와 한 번의 만료 이벤트를 확인합니다.
- 현재 사용자 갱신, 동시에 도착하는 401, 늦은 로그아웃 완료, 겹친 로그인, 같은 사용자 재로그인, 500·502·잘못된 성공 응답, 호출자 취소, 로그인 중 서버 변경, 로그인·로그아웃 반복을 확인합니다.

특정 시나리오만 실행할 수도 있습니다.

```powershell
dotnet run --project .\Tests\ApiClient.Tests\ApiClient.Tests.csproj -c Release -- old-session-success
dotnet run --project .\Tests\ApiClient.Tests\ApiClient.Tests.csproj -c Release -- current-401-html
```

R1의 MySQL 동시 관리자 변경은 `RemoteWork.Server/tests/test_admin_concurrency.py`에서 별도로 검증합니다. 실제 공통 HTTP·화면 점검은 `Tests/RemoteWork.ClientChecks`, 기존 2번 조회 대역 검증은 `Tests/TaskQueries.Tests`입니다. 각 결과는 누적하지 않고 구분해 기록합니다.
