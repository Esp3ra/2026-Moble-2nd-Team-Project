# 5번 근태 화면과 활동 상태 확인

2026-10-22 연차 화면 보완: 신청 유형·기간/사유 글자수 즉시 요약, 관리자 차감 안내, 신청 목록의 상태 필터/조회 전·빈 목록·실패 안내를 추가했다. `LeaveOnly`는 같은 Designer UserControl의 연차 전용 표시이고 기본 출퇴근 메뉴는 유지한다. `PreviewOnly`는 별도 체험 창에서 자식 창 제목에 실제 저장 없음을 표시하는 용도이며 인증/저장 권한을 부여하지 않는다. 실제 서버 연동 없이 직접 누르는 [별도 화면 체험](../../../Tools/LeavePreview/README.md)을 제공한다. 체험 데이터는 그 Tools 실행 파일의 메모리에만 있으며 기본 프로그램의 연차 저장 미연결 상태는 유지한다. 실제 연차 HTTP/잔여일·담당자 승인/정책·MySQL 연동은 후속이다.

새 회원 지문 등록 전용 `FingerprintEnrollmentForm`·C# 중계/라파 비동기 작업 통신을 추가했다. 기존 `UserManagementForm` 버튼·MainForm에는 아직 연결하지 않았다. 이 기능은 기존 `/scan` 시연·출퇴근/로그인 본인확인과 구분하며 [구현/주입 접점](../../../Devices/RaspberryPi/FINGERPRINT_ENROLLMENT.md)의 마지막 후속 절을 따른다. 등록 완료는 서버 `completed` 응답 뒤에만 표시한다.

`AttendanceControl`의 **활동 상태 확인** 버튼은 별도 `ActivityPresenceForm`을 엽니다. 직원·관리자 모두 자기 PC의 카메라를 직접 시작할 수 있습니다. 관리자에게 다른 직원의 카메라를 보여주는 기능은 아닙니다. 출퇴근 표는 예시로 표시합니다. 연차 목록과 잔여 일수의 예시 값은 제거했고, 실제 조회·신청·승인 저장은 아래 연결 전 상태로 구분합니다.

현재는 모달 확인 창을 열고 직접 시작하는 로컬 점검 기능입니다. 창을 닫으면 감지도 종료됩니다. 사용자가 확정한 최종 운영 범위는 **출근부터 퇴근까지 다른 업무 화면을 사용해도 계속 확인**하는 것이며, 출퇴근 저장·백그라운드 수명 관리·C#지문 요청/회원 일치는 후속 공동 연동입니다. 지문 요청은 현재 [기존 장치 점검 도구](../../../Tools/DeviceCheck/README.md)의 수동 버튼에서만 제공합니다. 이번 PR 게시 후 해당 후속 구현은 대기합니다.

후속으로 화면과 독립적인 `ActivityMonitor`와 공유 상태 view를 준비했습니다. 기본 근태 진입은 여전히 독립 수동 점검 모드입니다. **실제 MainForm 주입·출근/퇴근 API 연결은1번/5번 공동 연결 전**이며 자동 출근이나 정오 지문 요청을 구현했다고 표시하지 않습니다. 필수 지문 시점(출근/로그인·근무 중 정오12시·퇴근/로그아웃), 지문 특징정보 탈퇴 시 삭제·인증 기록1년은 사용자 요구로 별도 전달하며 아직 실행/삭제 정책으로 연결하지 않았습니다.

## 1번에게 제공하는 감지 서비스 접점

`IActivityMonitor`는 `Id`, `IsRunning`, `IsStopping`, `Latest`, `Changed`, `StartAsync`, `StopAsync`, `IsCurrent`, `Dispose`를 제공합니다. 기본 `ActivityMonitor()`는 기존 카메라/조도 소스를 생성하지만 생성만으로 I/O를 시작하지 않습니다. `ActivityMonitor(IActivityCamera, ILightMotionSource)`는 대역/실제 의존성 주입을 지원합니다.

1번은 MainForm의 실제 로그인 세션당 인스턴스 하나를 소유하고, **서버 출근 저장 성공과 실제 work_session_id 확인 뒤** 아래 접점을 호출합니다. 예시는 미연결 서버/DTO를 구현한 코드가 아닙니다.

```csharp
var session = AppServices.Api.CaptureSession();
var monitor = new ActivityMonitor();
var context = new ActivityMonitorContext(serverWorkSessionId,
    () => session.IsCurrent, session.Cancellation);
await monitor.StartAsync(context, cameraOptions, piBaseUri);
var page = new AttendanceControl(role, monitor);
// 정상 퇴근 저장 성공 또는 접속 종료: await monitor.StopAsync();
// MainForm 최종 해제: monitor.Dispose();
```

- 메뉴마다 같은 `monitor`를 전달합니다. `AttendanceControl(role, monitor)`는 같은 서비스의 상태 창을 열고, `ActivityPresenceForm(monitor)`는 공유 상태 view입니다. 공유 view의 시작/중지는 비활성화되며 닫기/Dispose는 구독만 해제합니다. 기존 기본 생성자·카메라/조도 주입 생성자는 수동 모드의 시작/중지/닫기 수명을 유지합니다. Designer/resx·MainForm·LoginForm·공통 ApiClient는 수정하지 않았습니다.
- 서비스는 UI 타이머 없이100ms 주기로 입력/30초 판정을 처리합니다. 수동 점검은 동일 파이프라인을 기존 화면 타이머로 호출합니다. 같은 context/설정의 중복 시작은 단일 효과, 다른 활성 context 또는 이전 정리 중 시작은 거절합니다. context는 로그인/근무 수명마다 새로 만들고 실제 서버 근무ID를 전달합니다.
- `ActivityMonitorUpdate`는 monitor ID·generation·sequence·work_session_id·phase·상태/안내·신선한 프레임을 포함합니다. `ActivityMonitorSubscription`은 UI SynchronizationContext.Post 전과 콜백 내부에 현재 세션·인스턴스/세대·근무ID·최신 sequence·view 생존/구독을 재검사합니다. 인증 함수는 서비스 잠금 밖에서 호출해 공통 인증 잠금과의 역전을 피합니다. 취소에서 UI 동기 Invoke/Wait/Result를 사용하지 않습니다.
- 세션 취소는 이전 세대를 즉시 무효화하고 같은 Stop Task를 공유합니다. 자동 모드에서는 IsCurrent가 거짓이 되는 것도 다음 입력 주기에 감지합니다. 정리는 카메라·조도와 입력 루프를 함께 기다리며 기본4초로 제한합니다. 기존 소유 Python의2초 유예는 유지하고, 정리 실패는 오류로 표시·소스 Dispose 후 재사용을 거절합니다. Dispose는 소유 자원을 해제하며 정상 종료에서는 StopAsync를 먼저 await합니다.
- 최신 프레임/신호만 메모리에 유지하고 영상 저장/외부 전송·근태 DB 쓰기·지문 스캔을 추가하지 않습니다. 별도 서비스 API/이벤트는1번 연결 PR에서 대조할 후보 인터페이스이며 실제 로그인/출퇴근 통합·실장비 장시간 검증은 별도입니다.

## 실행

1. 최초 한 번 저장소 `Tools/HandPresence/Setup-Hand-Presence.cmd` 실행. Windows Python 3.13과 모델/전용 가상환경이 필요합니다. 기존 설치가 있으면 재설치하지 않습니다.
2. Python 카메라 시연과 Windows 카메라 앱을 종료합니다.
3. 저장소 루트 PowerShell에서 `dotnet run --project .\RemoteWork.WinForms\RemoteWork.WinForms.csproj` 실행. 기존 공통 서버에 로그인하고 **출퇴근·연차 → 활동 상태 확인**을 누릅니다. 로그인/서버 실행 방법은 루트 README를 따릅니다.
4. **라파 주소**에 현재 장치의 서버 루트 `http://라파주소:8080`을 입력합니다. 기존 Pi 서비스와 메가의 조도 송신이 켜져 있어야 합니다. 주소는 저장소에 개인 설정으로 보관하지 않습니다.
5. 촬영 각도를 손·책상 쪽으로 맞추고 번호0/DSHOW·기본 영역으로 **카메라 시작**. 카메라와 조도 조회를 함께 시작합니다. 장치가 다르면 중지 후 번호/방식을 직접 바꿉니다. 영역·주소는 중지 후 수정하고 재시작합니다.
6. 손을 넣기/정지/빼기, 카메라 영역 움직임, 조도센서 가림/해제와 아래30초 규칙을 확인합니다. 중지 또는 창 닫기로 카메라·조도 조회를 해제합니다.

## 30초 기준

- **손 있음 / 카메라 영상 움직임 / 조도센서 변화** 중 하나라도 확인되면 활동 중이며 무감지 시간을 초기화합니다.
- 세 신호가 모두 없고 카메라·조도 데이터가 정상인 상태가 **연속30초** 이어지면 자리 비움입니다. 시작/복구 직후에는 새30초를 확인하며, 유예 동안 활동 중과 남은 시간을 표시합니다.
- 카메라 지연/오류, Pi 조회 실패·조도 데이터 미수신/중복 관찰 시각 고정은 확인 필요이며 무감지 누적을 끊습니다. 연결 문제를 자리 비움으로 처리하지 않습니다.
- 조도 `recent_motion`의10초 유지 상태로 타이머를 반복 갱신하지 않습니다. `last_motion_at` ID와 `seconds_since_motion`으로 실제 변화 시점을 추정하고 같은 변화는 한 번만 반영합니다. 고정 가림도 지속 움직임이 아닙니다. HTTP 왕복 시간은 이른 자리 비움이 되지 않는 방향으로 보정합니다.
- 영상 움직임은 선택 영역을160×120 회색조/5×5 blur로 축소하고, 프레임 차이에서 전체 노출 변화를 제외한 뒤25단계 이상 바뀐 픽셀2% 이상을 기준으로 합니다. 첫 프레임·영역/재시작·5초 공백 뒤에는 기준을 새로 잡습니다. 손 검출 상자는 움직임 계산 후 그립니다. 시연 기본값이며 실제 환경 정확도와 조명/가림/진동의 영향은 별도 시험이 필요합니다.

창을 여는 것만으로 카메라를 시작하지 않습니다. 처음 프레임 전에는 확인 대기, 수신/추론/전달/표시까지 5초 넘게 오래된 프레임은 영상과 이전 활동 표시를 숨깁니다. 연결·모델 오류는 자리 비움과 구분합니다. 실제 손이 보이지 않거나 손 검출에 실패해도 표시가 바뀔 수 있으며 이 결과는 출퇴근·회원·근무 여부를 저장하거나 판단하지 않습니다. 몸통 검출은 미구현입니다.

준비 단계에는 인식 모델 준비 중·카메라 연결 중·첫 영상 대기·첫 영상 분석을 표시합니다. 지연 시 마지막 단계를 함께 표시합니다. Windows Python의 제어용 stdin 대기 스레드가 먼저 시작되면 NumPy/OpenCV 네이티브 초기화에서 멈추는 재현에 따라 bridge main에서 영상 라이브러리를 먼저 준비합니다. 카메라를 여는 동작은 여전히 사용자의 명시적 시작 이후입니다.

## 구현 경계

- 화면·설정·수명 관리는 C# WinForms. 영상 취득·손 모델은 기존 Python `CameraWorker`. Pi 프로그램과는 독립적입니다.
- C#이 저장소에서 `Tools/HandPresence/.venv/Scripts/python.exe`와 `winforms_bridge.py`를 찾아 소유한 자식 프로세스 하나를 실행합니다. 셸 명령 조합·다른 카메라 자동 검색·공통 설정 변경은 없습니다.
- 전용 stdin/stdout JSON line v2: 손/영상 움직임·마지막 감지 경과 시간·수신 시각/QPC·오류·잘린 JPEG를 메모리로 전달합니다. 중간 프레임이 생략돼도 마지막 감지 시각을 보존합니다. v1은 오류로 처리하며 무감지로 대체하지 않습니다. C#은 별도 읽기 전용 클라이언트로 Pi GET /motion을1초 간격 조회하며 공통 Bearer/회원 정보를 보내지 않습니다. HTTP proxy·redirect를 사용하지 않고 응답/취소/크기를 제한합니다. 영상 저장·공통 서버 업로드는 없습니다.
- CPython 3.13 `time.monotonic()`과 Windows .NET `Stopwatch`의 QPC 시각으로 파이프 전달 전 지연도 계산합니다. 근거: [Python time](https://docs.python.org/3.13/library/time.html#time.monotonic), [Microsoft QPC/.NET 설명](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps). 절대 시각/UTC 근태 기록 계약과는 별도입니다.
- 중지 시 제어 메시지를 보내고 2초 내 종료하지 않으면 이 창이 시작한 프로세스 트리만 종료합니다. 타 Python/카메라 프로그램을 종료하지 않습니다. 창 종료와 직접 Dispose에서도 자원을 해제합니다.
- 닫기·중지·오류가 겹쳐도 같은 정리 작업을 기다립니다. 정리 중 부모 창 종료 요청이 들어오면 카메라·조도 해제 후 부모 종료를 이어갑니다. 일반 활동 창 닫기는 메인을 유지하며, 기존 인증 만료 종료는 로그인 화면으로 복귀합니다. 공통 로그인 코드를 바꾸거나 카메라를 인증 수단으로 연결한 것은 아닙니다.
- Designer.cs/resx·기존 공개 생성자·MainForm·인증·업무 API/DB 유지. 문서 D5-01~05 신원·장치 신뢰·제품 정책·저장 합의는 여전히 대기입니다.
- 소스 체크아웃과 준비된 Python 환경에서 실행하는 로컬 연결입니다. Python/모델을 포함한 배포 패키지는 제공하지 않습니다.

## 검증

`dotnet run --project Tests/ActivityPresence.Tests`는 실제 WinForms 메시지 루프·대역 카메라·합성 JPEG와 실제 Python stdio 자식 프로세스로 화면, 지연, 잘못된 응답, 재시작, 종료/강제 종료를 검사합니다. 물리 카메라·공통 서버·DB는 사용하지 않습니다. Python 회귀는 `Tools/HandPresence/README.md`를 따릅니다. 실제 웹캠의 손 인식·조명·가림·USB 단절·장시간 확인은 사용자 로컬 확인과 별도입니다.

30초 회귀는 가상 시각으로29/30초 경계·세 입력 각각·최근 조도 중복·고정 관찰·연결/재연결·놓친 중간 감지/오류를 검사합니다. Pi HTTP 응답 대역과 실제 로컬 loopback TCP도 사용합니다. 명시적 `dotnet run --project Tests/ActivityPresence.Tests -- --pi-check http://라파주소:8080`은 해당 장치에 GET2회를 보내 응답·관찰 시각 갱신만 확인합니다. 지문 요청이나 장치 설정 변경은 없습니다.

## 지문 본인 확인 담당 연결부

캡처한 1번 pending/일반 접속을 5번 relay와 연결하는 `FingerprintIdentityConnection`, 같은 요청의 수명을 관리하는 `FingerprintIdentityAttempt`, Designer 기반 `FingerprintIdentityForm`을 제공한다. 사용자 요청으로 LoginForm과 Designer 기반 `FingerprintLoginForm`도 연결했다. 지문 view의 verified는 본인 확인만이며, 별도 `ApiClient.IdentityLogin`에서 서버 출근 finalize·auth/me·원 로그인 세대 확인을 거친 뒤만 정상 토큰을 인수한다. 요청·결과·취소/원 finalize 복구와 담당자 확인 사항은 [연결 문서](IDENTITY_CONNECTION.md)를 따른다. 실제 공유 서버/키·Pi 활성화, 정오/퇴근 화면·운영 수명은 별도이며 새 기능은 OFF를 유지한다.

## 연차 화면 준비 상태 (2026-10-22)

`LeaveApplicationForm` / `LeaveDecisionForm` / 목록은 기존 Designer.cs·resx를 유지하여 Visual Studio 폼 디자이너와 도구상자에서 수정할 수 있다. 실제 서버 연결 전에도 입력 형식·기간과 화면 배치를 확인할 수 있다. 일반 저장소나 로컬 DB를 추가하지 않았다.

- 유형은 연차·오전 반차·오후 반차·병가·공가이다. 병가·공가는 연차 잔여일 차감 제외로 안내한다. 주말/공휴일·반차 기간·실제 차감 일수·중복 신청·잔여 일수는 서버 정책과 원자적 검증이 필요하며 임의 계산하지 않는다.
- 신청자 표시는 캡처한 실제 로그인 접속을 따른다. 서버 미연결 시 `입력 확인`만 하며 저장 완료로 표시하지 않는다. 목록은 미조회 상태의 빈 목록, 잔여 연차는 `조회 전`으로 표시한다. 예시 근태 기록도 제목에서 예시로 구분한다.
- 관리자 검토는 지정 직원에 대한 서버의 검토 권한 표시와 pending 상태가 모두 있어야 가능하다. UI의 권한 제한은 서버 권한 검사를 대신하지 않는다. 미래 adapter는 목록부터 직원 범위를 제한하고 결정 시 지정 담당 관계를 다시 확인해야 한다. `CanReview`는 해당 접속의 담당 범위 권한이며 처리 상태와 별도다.
- `ILeaveWorkflow`는 5번 내부 주입 접점이다. 실제 HTTP 경로·DTO·DB를 합의하거나 구현한 것이 아니다. 기본 `UnconnectedLeaveWorkflow`는 신청·조회·승인을 저장하지 않는다. 서버 adapter는 같은 캡처 접속에 묶고 상태/권한·원래 요청 UUID에 대한 멱등성과 동시 처리 검사를 구현해야 한다.
- 접속 변경 후 늦은 응답은 무시한다. 전송을 시작한 신청·결정 내용과 UUID는 고정하고 결과가 불명확하면 같은 요청만 확인한다. 결과를 확인하지 않고 새 신청·다른 승인/반려로 전환하지 않는다.

`dotnet run --project Tests/Leave.Ui.Tests -c Release`는 실제 WinForms 루프와 합성 접속/서버 대역으로 5개 유형·미연결·잘못된 기간·중복 클릭·응답 유실·영수증 불일치·담당 범위·접속 종료를 검사한다. 실제 서버/DB 신청 저장·휴가 정책 적용은 미검증이다. 실행 인자에 출력 폴더를 추가하면 신청·검토 폼을 PNG로 확인할 수 있다. 1번 서버 연동·권한/잔여일 정책 협의는 지문 연결 뒤 별도로 진행한다.
