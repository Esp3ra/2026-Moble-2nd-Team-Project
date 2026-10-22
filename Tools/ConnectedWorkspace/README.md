# 연차·지문 근무 동시 연결 실행기

기존 LoginForm·MainForm·Designer 화면을 재사용하는 명시 실행기다. 별도 모의 로그인이나 새 UI를 만들지 않는다. 기본 제품 실행/서버 설정은 바꾸지 않는다.

`Start-Connected-Workspace.cmd`를 명시 실행하면 기존 로그인 후 MainForm.Shown에서 같은 접속에 연차·현재 근무 연결을 각각 설치한다. 한쪽 연결 실패/미제공이 다른 쪽 설치를 막지 않는다. 현재 서버에 열린 근무가 있어야 기존 monitor가 카메라를 시작한다. 열린 근무를 임의 생성하지 않으며 정상 로그인·창 닫기를 지문 출근/퇴근으로 표시하지 않는다.

설정은 PC의 `%LOCALAPPDATA%/RemoteWork/workspace-connections.json`이다. 버전1이며 정확히 `version`, `server_url`, `workday`, `leave_enabled`를 가진다. `server_url`은 현재 client.json과 일치해야 한다. `workday`는 null 또는 `device_id`·`camera` 객체다. `camera` 필드는 `index`, `backend`, `left`, `top`, `right`, `bottom`이며 기존 ActivityCameraOptions 범위를 검사한다. 실제 장치 UUID·Pi 주소는 기존 enrollment-endpoints.json에서 선택/해결하며 계정 소유권은 서버가 판단한다. 비밀번호·Bearer·서명키는 이 설정에 넣지 않는다. 양쪽 비활성/중복·모르는 필드·다른 서버·미등록 장치를 거부한다.

```powershell
dotnet run --project Tools/ConnectedWorkspace -c Release -- --check-config
# 설정 검사만으로 카메라/서버/Pi를 열거나 로그인하지 않는다.
```

실제 지문 로그인은 서버가 기존 `IDENTITY_FINGERPRINT_REQUIRED`를 반환할 때 기존 LoginForm/FingerprintLoginForm 흐름을 따른다. 새 경로404/키/재시작 재조정 미준비를 `/scan`으로 우회하거나 서버 OFF를 켜지 않는다. 정오·퇴근은 이미 병합된 WorkdayStateConnection/PR73·74의 MainForm 연결을 사용한다. 로그인 종료/만료 시 같은 기존 소유자가 정리하고 메뉴는 같은 monitor를 빌린다.

연차는 PR79의 `HttpLeaveWorkflow.cs`가 제품 소스에 있는 검토된 결합/병합 빌드에서만 같은 captured Session당 하나를 설치한다. Draft adapter를 복사하거나 새로운 HTTP endpoint를 만들지 않는다. 없는 빌드는 연차 미연결 안내만 제공하고 지문·근무 경로는 계속한다. 연결 설치가 실제 연차쓰기 허용을 의미하지 않으며 D1/D2가 미설정이면 기존 readiness/버튼 차단을 유지한다.

검증은 `Tests/WorkdayActivity.Tests`의 기존51+새30=81항목이다. 실제 MainForm/공통 reader/통합 조립을 쓰되 로그인/열린 근무 HTTP와 monitor·연차 workflow는 명시 대역이다. 설정 오류와 두 연결의 독립 시도, 실패 격리, 정책OFF, 같은 adapter/monitor 메뉴 공유, 로그인 종료를 확인한다. 실제 계정·키·운영 DB·라파·카메라 성공으로 확대하지 않는다. 실제 PR79 결합/서버·MySQL 검증은 연차 UI 검사와 별개다.
