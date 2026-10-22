# 제한 로그인 HTTP 전달부 검사

`dotnet run --project Tests/IdentityPending.Http.Tests -c Release`

통제된 HttpMessageHandler에서 pending 전용 인증, 원문 전달,503의 비성공 처리,
일반 ApiClient로의 자동 토큰 승격 없음,401 만료·늦은 응답·owner 종료 취소,
64KiB 응답 제한을 검사한다. 실제1번 서버/DB·Pi·센서·두PC 검사가 아니다.

제품 제공부는 `RemoteWork.WinForms/Infrastructure/IdentityPendingConnection.cs`이다.
`LoginAsync`에 원래 발급UUID와 현재 로그인 세대를 확인하는 thread-safe callback,
소유 화면/접속의 lifetime을 넘긴다. 반환 객체를 기능 수명이 소유하고 종료 때 Dispose한다.
기존 ApiClient에 pending_token을 넣지 않는다. 같은 요청 결과 조회를 먼저 하고
자동 새UUID/재스캔으로 실패를 덮지 않는다.5번 relay가 verified를 반환해도 정상
로그인/근태 완료로 표시하지 않고 실제 finalize 결과·정상 접속 인수를 별도로 확인한다.
