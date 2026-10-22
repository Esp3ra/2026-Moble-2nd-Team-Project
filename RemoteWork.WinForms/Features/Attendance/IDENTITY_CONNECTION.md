# 5번 지문 본인 확인 연결부

작성: 5번 / 전달 대상: 1번 로그인·출퇴근 연결 담당. 2026-10-22, 통합 기준 alpha `5dcd66e`.

## 범위와 연결 경계

`FingerprintIdentityConnection`은 기존 1번 `IdentityPendingConnection` 또는 캡처한 `ApiClient.Session`을 빌려서 `FingerprintIdentityRelay`에 연결한다. 공통 인증 클래스, LoginForm, MainForm, 서버 API·DB·라파 설정은 수정하지 않는다. 새 기능의 운영 활성화나 타 담당 화면 연결 합의가 끝났다는 의미가 아니다. `REMOTEWORK_ENABLE_DEVICE_IDENTITY_DRAFT=0`을 유지한다.

- 출근: 이미 생성된 `IdentityPendingConnection`만 받는다. 이 연결이 만드는 목적은 `clock_in`이다.
- 정오·퇴근: `ApiClient.CaptureSession()`으로 캡처한 실제 접속과 `noon_check` / `clock_out`을 받는다. 표시용 UserInfo로 접속을 만들지 않는다.
- 기존 서버 경로의 issue / requests 조회 / approvals / complete / challenges 조회 / cancel만 사용한다. 취소 뒤에도 조회하여 서버 상태를 확인한다. 서버가 먼저 검증한 경합을 취소 완료라고 표시하지 않는다.
- 서명된 요청·probe·승인·결과는 원문 바이트 / 원래 base64url 문자열로 전달한다. 키·토큰을 Pi로 보내지 않는다. 저장 파일이나 별도 DB를 추가하지 않는다.
- 서버의 `verified`는 본인 확인만 완료한 것이다. finalize, 일반 토큰 설치, 출퇴근 DB 저장, 정오 일정, 장치 목록/주소 선택은 기존 담당 연결이 별도로 수행해야 한다. 이 view는 finalize 응답 복구 접속의 24시간 예외를 확장하거나 소유하지 않는다.

## 소유권과 재시도

`FingerprintIdentityAttempt` 하나가 한 사용자 동작을 나타낸다. issue/cancel UUID를 보내기 전에 생성하고 메모리에서 유지한다. issue 응답 유실은 원래 UUID로 조회하고, 결과 유실은 원래 challenge와 결과만 확인한다. 센서 호출을 시작한 뒤에는 자동 재스캔하지 않는다. 결과를 받지 못한 경우에는 서버 상태 조회·취소만 가능하다.

`FingerprintIdentityForm`은 Designer.cs/resx 기반이며 생성만으로 네트워크나 센서를 실행하지 않는다. 사용자가 시작 버튼을 눌러야 실행한다. 대기·결과 전송·시간 초과·연결 실패·서버 기능 준비 필요·취소 미확인을 구분한다. 세션 취소/교체와 유효시간 만료를 재확인하고 이전 계정의 늦은 결과를 표시하지 않는다.

폼은 전달받은 attempt를 소유하고 해제한다. connection / pending / session / device는 호출자가 소유한다. 정상 창 닫기는 장치 요청 중지 후 같은 서버 작업 취소를 기다린다. 서버 취소를 확인하지 못하면 안내를 유지하고 닫기를 한 번 더 요청하도록 한다. 강제 Dispose는 로컬 입력만 중지하므로 호스트가 종료하기 전 `await attempt.CancelAsync()`와 원래 접속의 종료/회수 정책을 책임져야 한다. 새 계정으로 바뀐 뒤 이전 요청을 새 토큰으로 취소하지 않는다.

1번이 검토하여 연결할 접점 예시(현재 LoginForm에 설치한 코드가 아니다):

```csharp
// pending은 1번이 이미 획득한 유효한 로그인 대기 접속이다.
var connection = new FingerprintIdentityConnection(pending);
using var device = new HttpFingerprintIdentityDevice(trustedPiAddress);
using var attempt = new FingerprintIdentityAttempt(connection, device, registeredDeviceId);
using var form = new FingerprintIdentityForm(attempt);
form.ShowDialog(owner);
var identityOnly = form.IdentityResult;
// identityOnly != null / verified도 출근·로그인 완료가 아니다.
// 원래 challenge와 finalize UUID를 유지한 확정·토큰 설치는 1번이 담당한다.
```

정오/퇴근은 connection 생성만 `new FingerprintIdentityConnection(capturedSession, "noon_check")` 또는 `"clock_out"`으로 바꾼다. 같은 사용자 동작의 결과 복구 중에는 새 attempt를 생성하지 않는다.

## 확인 요청과 상태

구현: 5번 연결부·view의 로컬 검증 완료. 1번 화면 주입·출근/정오/퇴근 최종 처리·실서비스 활성화·실물 검증은 미실행이다. 관련 선행 서버 PR62와 5번 코어/HTTP PR63·65·67은 원본에 병합되어 있다. 본 변경의 신규 PR은 아직 생성하지 않았다.

1번 검토 시 캡처 접속의 수명, 실제 등록 장치 ID/신뢰 주소 공급, verified 이후 finalize의 원래 UUID와 영수증 복구, 결과 불명 상태에서 창을 닫은 경우의 회수 방식을 확인해야 한다. 기존 로그인 차단을 켜기 전 사용자 승인과 함께 통합 검증한다.

## 로컬 검증

```powershell
dotnet run --project Tests/FingerprintIdentity.Connection.Tests -c Release
dotnet run --project Tests/FingerprintIdentity.Tests -c Release
dotnet run --project Tests/IdentityPending.Http.Tests -c Release
```

새 연결/view 검사는 실제 C# 캡처 접속·relay·WinForms 메시지 루프를 사용하되 서버는 합성 HttpMessageHandler, 센서는 대역이다. 요청·probe·승인·결과 원문, 응답 유실 복구, 단일 스캔, 중복 클릭, 취소 재시도/경합, 세션 교체/Dispose, 잘못된 영수증, 타임아웃/끊김과 창 닫기를 확인한다. 실제 원격 서버·MySQL·물리 센서나 로그인 성공을 확인한 것으로 간주하지 않는다. 기존 Pi loopback TCP 검사는 `Tests/FingerprintIdentity.Http.Tests/README.md`를 따른다.
