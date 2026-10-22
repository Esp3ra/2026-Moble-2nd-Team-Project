# 제한 로그인 HTTP 전달부 검사

`dotnet run --project Tests/IdentityPending.Http.Tests -c Release`

통제된 HttpMessageHandler에서 pending 전용 인증, 원문 전달,503의 비성공 처리,
일반 ApiClient로의 자동 토큰 승격 없음,401 만료·늦은 응답·owner 종료 취소,
64KiB 응답 제한을 검사한다. 실제1번 서버/DB·Pi·센서·두PC 검사가 아니다.

R62-02 회귀는 TimeProvider로 첫 finalize 응답 유실 → 최초5분 경과 → 같은
challenge/원 finalize UUID 조회·재전송을 검사한다. 새 발급·승인·complete·다른 UUID는
전송하지 않으며, 현재 로그인 변경·외부 취소·서버401과 응답 직전 변경도 유지한다.
최초 확정 시도부터 최대24시간의 로컬 상한은 재조회로 연장하지 않는다.

제품 제공부는 `RemoteWork.WinForms/Infrastructure/IdentityPendingConnection.cs`이다.
`LoginAsync`에 원래 발급UUID와 현재 로그인 세대를 확인하는 thread-safe callback,
소유 화면/접속의 lifetime을 넘긴다. 반환 객체를 기능 수명이 소유하고 종료 때 Dispose한다.
기존 ApiClient에 pending_token을 넣지 않는다. 같은 요청 결과 조회를 먼저 하고
자동 새UUID/재스캔으로 실패를 덮지 않는다.5번 relay가 verified를 반환해도 정상
로그인/근태 완료로 표시하지 않고 실제 finalize 결과·정상 접속 인수를 별도로 확인한다.

`IsCurrent`/`ExpiresAt`은 여전히 신규 활동의5분 수명이다. 해당 값이 false여도 소유
로그인 세대와 lifetime이 유지된 경우 이미 보낸 finalize의 `GetAsync(challengeId)`와
`FinalizeAsync(challengeId, originalFinalizeId)`로만 결과를 복구할 수 있다. 호출부가
활동 만료만으로 Dispose하면 RAM의 원 요청 기록도 사라지므로, 명시 로그아웃/화면
종료·접속 변경에 맞춰 소유 수명을 정리한다. 서버의 더 짧은 보관기한·현재권한·401/
403/410은 그대로 최종 판정이며 이 로컬 상한이 서버 권한을 연장하지 않는다.
