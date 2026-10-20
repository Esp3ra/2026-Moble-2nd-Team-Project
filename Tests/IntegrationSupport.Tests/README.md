# 공통 병행 개발 도구 검사

`dotnet run --project Tests/IntegrationSupport.Tests/IntegrationSupport.Tests.csproj -c Release`

공개 가상 request/result 벡터의 C# 바이트·MAC 대조와 FeatureUiScope의 현재/이전 선택,
UI 큐 지연·페이지 폐기·같은계정 재로그인·logout/401·다른 scope·반복 Dispose·callback 예외를 시험한다.
HTTP와 UI queue는 주입한 대역이다. 실물 센서·실제 WinForms·서버/DB·MainForm 연결 검증이 아니다.
fixture 키는 시험용 공개값이며 제품 PC/장치에 설치하면 안 된다.

자세한 사용법/실행 경계는 [공통 지원 안내](../../docs/TEAM_INTEGRATION_SUPPORT.md)를 따른다.
