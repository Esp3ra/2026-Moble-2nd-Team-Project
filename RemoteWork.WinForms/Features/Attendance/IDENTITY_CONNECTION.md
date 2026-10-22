# 5번 지문 본인 확인 연결부

작성: 5번 / 전달 대상: 1번 로그인·출퇴근 연결 담당. 2026-10-22, 통합 기준 alpha `5dcd66e`.

## 범위와 연결 경계

사용자의 로그인 연결 요청으로 `LoginForm` → `FingerprintLoginForm` → 기존 5번 relay/Pi HTTP → 1번 finalize → 정상 접속 인수를 연결했다. 1번 소유 `ApiClient`에는 별도 partial 파일의 캡처 로그인 수명과 최소 취소/현재 접속 정리를 추가했고 `EnrollmentEndpoints`에는 읽기 전용 장치 ID 목록을 제공했다. 서버 API/DB·실제 설정·키·Pi와 MainForm은 수정하지 않았다. 상대 담당자의 신규 변경 수용·운영 활성화 완료를 의미하지 않는다. 실제 `REMOTEWORK_ENABLE_DEVICE_IDENTITY_DRAFT=0`은 유지한다.

- 출근: 이미 생성된 `IdentityPendingConnection`만 받는다. 이 연결이 만드는 목적은 `clock_in`이다.
- 정오·퇴근: `ApiClient.CaptureSession()`으로 캡처한 실제 접속과 `noon_check` / `clock_out`을 받는다. 표시용 UserInfo로 접속을 만들지 않는다.
- 기존 서버 경로의 issue / requests 조회 / approvals / complete / challenges 조회 / cancel만 사용한다. 취소 뒤에도 조회하여 서버 상태를 확인한다. 서버가 먼저 검증한 경합을 취소 완료라고 표시하지 않는다.
- 서명된 요청·probe·승인·결과는 원문 바이트 / 원래 base64url 문자열로 전달한다. 키·토큰을 Pi로 보내지 않는다. 저장 파일이나 별도 DB를 추가하지 않는다.
- 서버의 `verified`는 본인 확인만 완료한 것이다. 출근 로그인은 별도 `ApiClient.IdentityLogin`이 원 finalize UUID로 서버 저장을 확정한 뒤, 반환된 정상 Bearer로 auth/me의 계정·활성·만료를 대조하고 원 로그인 세대에만 원자적으로 인수한다. 지문 view 자체는 토큰을 설치하지 않는다. 정오·퇴근 화면/일정·활동 서비스 자동 수명 연결은 후속이다.

기존 비밀번호 로그인을 먼저 사용하고 **서버의409/IDENTITY_FINGERPRINT_REQUIRED**에서만 지문 흐름으로 전환한다. OFF 또는 실제 당일 열린 근무의 재로그인 성공은 기존 흐름을 유지한다. 401/403/404/503에서 임의 지문/일반 로그인으로 우회하지 않는다. 기존 topology 파일의 현재 서버 일치/HTTP 명시 설정과 장치별 Pi 주소를 사용하며, 파일에 장치가 있다는 이유로 계정 매핑/권한이 승인된 것으로 취급하지 않는다. 장치가 하나면 로그인 창에서 명시한 자동 시작으로 OLED 입력 대기를 연다. 여러 개면 선택 후 시작한다. 일반 `/scan` 결과·표시 이름·센서 슬롯을 정상 토큰으로 사용하지 않는다.

## 소유권과 재시도

`FingerprintIdentityAttempt` 하나가 한 사용자 동작을 나타낸다. issue/cancel UUID를 보내기 전에 생성하고 메모리에서 유지한다. issue 응답 유실은 원래 UUID로 조회하고, 결과 유실은 원래 challenge와 결과만 확인한다. 센서 호출을 시작한 뒤에는 자동 재스캔하지 않는다. 결과를 받지 못한 경우에는 서버 상태 조회·취소만 가능하다.

두 새 폼은 Designer.cs/resx 기반이며 생성만으로 I/O를 시작하지 않는다. 기본 독립 view는 시작 버튼을 사용하고 로그인 호스트만 명시적인 autoStart를 전달한다. 대기·결과 전송·시간 초과·연결 실패·서버 기능 준비 필요·취소 미확인을 구분한다. 세션 취소/교체와 유효시간 만료를 재확인하고 이전 계정의 늦은 결과를 표시하지 않는다.

기본 지문 view는 attempt를 소유한다. 로그인 view의 내부 지문 창은 `ownsAttempt:false`로 같은 attempt를 빌리므로 창을 닫았다 다시 열어도 결과를 재구성하거나 센서를 새로 읽지 않는다. connection / pending / session은 소유자가 해제하며 로그인 view는 자기 device/attempt만 해제한다. 정상 창 닫기는 장치 중지·같은 작업 취소·원 문맥 revoke를 확인한다. 확인 실패는 명시 안내 후 로컬 종료와 서버 취소를 구분한다. 출근 finalize 응답 확인 중에는 닫기/회수를 경쟁시키지 않고 응답 또는 시간 초과를 먼저 기다린다. 응답 불명 뒤 닫기는 출근 취소가 아니라는 안내를 먼저 표시한다. 강제 Dispose는 로컬 정리일 뿐 원격 revoke/근태 rollback이 아니다. 새 계정으로 바뀐 뒤 이전 요청을 새 토큰으로 취소하지 않는다.

`ApiClient.IdentityLogin`은 일반 세대와 캡처 서버 주소, 원 로그인/finalize/revoke UUID를 보유한다. 최초5분 뒤에도 이미 시도한 같은 finalize만 기존 pending의24시간 상한 내에 재확인한다. auth/me를 아직 확인하지 못했으면 정상 접속을 설치하지 않고, 동일 결과 재생의 token/근무/영수증이 달라지면 거절한다. 명시 revoke는 같은 결과 보관 문맥에서 허용하지만 신규 활동 권한은 열지 않는다. 일반 ApiClient의 공유12초 timeout/401·세대 규약을 유지한다.

독립 중계 접점 예시(로그인 전용 호스트는 위 별도 finalize 수명을 사용한다):

```csharp
// pending은 1번이 이미 획득한 유효한 로그인 대기 접속이다.
var connection = new FingerprintIdentityConnection(pending);
using var device = new HttpFingerprintIdentityDevice(trustedPiAddress);
using var attempt = new FingerprintIdentityAttempt(connection, device, registeredDeviceId);
using var form = new FingerprintIdentityForm(attempt);
form.ShowDialog(owner);
var identityOnly = form.IdentityResult;
// identityOnly != null / verified도 출근·로그인 완료가 아니다.
// 정상 토큰 설치는 ApiClient.IdentityLogin의 finalize/auth/me/세대 확인을 거친다.
```

정오/퇴근은 connection 생성만 `new FingerprintIdentityConnection(capturedSession, "noon_check")` 또는 `"clock_out"`으로 바꾼다. 같은 사용자 동작의 결과 복구 중에는 새 attempt를 생성하지 않는다.

## 확인 요청과 상태

구현: C# 로그인→실제 HTTP 서버→Pi HTTP→출근 저장→정상 로그인 인수를 격리 환경에서 검사했다. 센서/UART·OLED·계정/키/준비 상태는 시험용이며 물리 장비가 아니다. 정오·퇴근 화면/스케줄·활동 서비스 자동 수명, 실서비스 활성화·실물/두PC는 미실행이다. 선행 PR62·63·65·67은 병합되어 있고 이번 신규 PR은 아직 생성하지 않았다.

1번 검토 시 공통 로그인·ApiClient partial/기존 취소 보완·pending의 보관 문맥 revoke·읽기 전용 topology, 실제 등록 장치/신뢰 주소와 finalize 원 UUID/원 토큰 인수·미확정 종료를 확인해야 한다. 관련 번호 폴더의 사전 전달 준비본은 비공개로 준비했으며 공개 회신은 기존 PR58 한 곳에 취합할 예정이다. 문서 준비가 상대 승인이나 게시 완료는 아니다. 실제 지문6테이블 적용·개인키/운영 readiness·Pi runtime 전환은 별도 적용 단계이며 테스트용 ready=True를 운영에 설치하지 않는다.

## 로컬 검증

```powershell
dotnet run --project Tests/FingerprintIdentity.Connection.Tests -c Release
dotnet run --project Tests/FingerprintIdentity.Tests -c Release
dotnet run --project Tests/IdentityPending.Http.Tests -c Release
dotnet run --project Tests/IdentityLogin.Ui.Tests -c Release
```

새 연결/view 검사는 실제 C# 캡처 접속·relay·WinForms 메시지 루프를 사용하되 서버는 합성 HttpMessageHandler, 센서는 대역이다. 요청·probe·승인·결과 원문, 응답 유실 복구, 단일 스캔, 중복 클릭, 취소 재시도/경합, 세션 교체/Dispose, 잘못된 영수증, 타임아웃/끊김과 창 닫기를 확인한다. 실제 원격 서버·MySQL·물리 센서나 로그인 성공을 확인한 것으로 간주하지 않는다. 기존 Pi loopback TCP 검사는 `Tests/FingerprintIdentity.Http.Tests/README.md`를 따른다.

별도 실제 서버/MySQL와 C# 폼 연결은 `Tests/IdentityLogin.Ui.Tests/README.md`를 따른다. 그 검사는 공개 합성 계정/독립 키·가짜 UART를 사용하며 사용자의 실제 회원/센서/서버를 변경하지 않는다.
