# 지문 연결·화면 회귀

```powershell
dotnet run --project Tests/FingerprintIdentity.Connection.Tests -c Release
```

실제 `IdentityPendingConnection` / 캡처한 `ApiClient.Session` → 5번 connection / attempt / relay를 연결한다. 공개 합성 wire fixture와 통제한 HttpMessageHandler·센서 대역을 사용한다. WinForms 검사는 실제 메시지 루프와 컨트롤 버튼으로 대기·취소·창 닫기·접속 교체 안내를 확인한다. 실제 원격 서버·MySQL·장치·키·환경설정을 읽거나 변경하지 않는다.

선택적으로 출력 폴더를 인자로 넘기면 대기 화면 PNG 한 장을 만든다. 영상이나 지문 데이터는 저장하지 않는다.

- 원문 전달, 목적별 인증 접속, 원래 issue/cancel UUID와 complete 결과 유지.
- issue/complete/cancel 응답 유실, 결과 없는 센서 오류, 중복 시작, 취소·완료 경합.
- 접속 교체·만료, owner 취소, view Dispose와 늦은 결과 무시.
- 잘못된 challenge/영수증/목적/시각/workflow 응답 거부.
- 본인 확인과 로그인/근태 확정 분리. finalize/revoke 자동 호출 없음.

기존 relay / Pi loopback 검사는 각 테스트 폴더의 설명과 별도로 실행한다. 이 검사의 성공은 운영 UI 주입이나 실물 인증 완료가 아니다.
