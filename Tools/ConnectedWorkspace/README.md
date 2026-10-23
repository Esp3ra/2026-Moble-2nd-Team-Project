# 일반 WinForms의 연차·지문 근무 연결

Visual Studio에서 `RemoteWork.WinForms`를 시작하거나 빌드한 `RemoteWork.WinForms.exe`를 열면,
일반 로그인 성공 후 저장된 설정으로 기존 연차·지문 근무 연결을 설치한다. 별도 CMD 실행은 필요 없다.
기존 LoginForm·MainForm·Designer와 근무 상태 바를 사용하며 서버 기능을 자동으로 켜지 않는다.

로그인한 MainForm이 표시될 때 같은 접속에 연차·현재 근무 연결을 각각 한 번 설치한다.
설정 누락·손상·다른 서버 설정이면 일반 로그인을 유지하고 하단에 **지문·근무 연결 설정 필요**를 표시한다.
정상 설정에서 서버 지문 경로가404/503이면 기존 지문 근무 바는 보이되 지문 확인·퇴근 버튼은 비활성화한다.
현재 서버에 유효한 열린 근무가 있어야 기존 monitor가 카메라를 시작한다. 열린 근무를 임의 생성하거나
정상 로그인·창 닫기를 지문 출근/퇴근으로 표시하지 않는다.

별도 클라이언트 CMD는 제거했다. `ConnectedWorkspace` 프로젝트의 `--run`/`--check-config`는
호환·설정 점검용으로 유지하며 제품의 같은 시작 연결부를 사용한다. 필요한 경우 저장소 루트에서
`dotnet run --project Tools/ConnectedWorkspace -c Release -- --run`으로 호출할 수 있다.
제품과 호환 실행기는 동일한 `DpiUnawareGdiScaled` 설정을 사용하므로 진입점에 따라 화면 배율이 바뀌지 않는다.

로컬 실제 서버의 직원 계정으로 새 기본 시작 연결부를 실행해 기존 공지 화면과 지문 근무 바가
함께 표시되는 것도 확인했다. 서버의 지문 기능이 준비되지 않은 상태에서 준비 안내와 비활성
지문 확인·퇴근 버튼을 확인했으며, 검사 접속은 종료했다. 이는 장비 인증 성공 검사가 아니다.

설정은 PC의 `%LOCALAPPDATA%/RemoteWork/workspace-connections.json`이다. 버전1이며 정확히 `version`, `server_url`, `workday`, `leave_enabled`를 가진다. `server_url`은 현재 client.json과 일치해야 한다. `workday`는 null 또는 `device_id`·`camera` 객체다. `camera` 필드는 `index`, `backend`, `left`, `top`, `right`, `bottom`이며 기존 ActivityCameraOptions 범위를 검사한다. 실제 장치 UUID·Pi 주소는 기존 enrollment-endpoints.json에서 선택/해결하며 계정 소유권은 서버가 판단한다. 비밀번호·Bearer·서명키는 이 설정에 넣지 않는다. 양쪽 비활성/중복·모르는 필드·다른 서버·미등록 장치를 거부한다.

### R80-01 수정: 연차 전용 실행과 Pi 설정 분리

`workday:null, leave_enabled:true`이면 Pi topology 파일을 읽지 않는다. 따라서 파일이
없거나 관련 없는 잘못된 Pi JSON이 있어도 연차 전용 설정 검사와 로그인 준비를 진행한다.
근무 연결을 실제로 요청한 경우에만 기존 EnrollmentEndpoints.Load/Resolve를 호출하며
누락·잘못된 JSON·다른 서버·미등록 장치를 계속 거절한다. 필요한 지문 설정을 무시하는
fallback은 없다. 실행기는 InvalidDataException도 처리해 설정 검사 실패를 exit1/안내로 반환한다.

`WorkspaceProfile.Load`의 선택 경로 인수는 파일 검사 도구/테스트에서 독립 소유 설정을
지정하기 위한 것이다. 실제 Main은 기존 기본 경로를 사용한다. Main과 같은 Run 명령 처리부의
`--check-config`도 실제 파일 Load를 통해 검사했으며 테스트의 경로만 소유 임시 폴더로 공급한다.
운영 설정/서버/장치 접근·실계정 로그인·실행 중인 GUI 자동화는 하지 않았다.

```powershell
dotnet run --project Tools/ConnectedWorkspace -c Release -- --check-config
# 설정 검사만으로 카메라/서버/Pi를 열거나 로그인하지 않는다.
```

실제 지문 로그인은 서버가 기존 `IDENTITY_FINGERPRINT_REQUIRED`를 반환할 때 기존 LoginForm/FingerprintLoginForm 흐름을 따른다. 새 경로404/키/재시작 재조정 미준비를 `/scan`으로 우회하거나 서버 OFF를 켜지 않는다. 오후 업무 전 확인·퇴근은 이미 병합된 WorkdayStateConnection/PR73·74의 MainForm 연결을 사용한다. 로그인 종료/만료 시 같은 기존 소유자가 정리하고 메뉴는 같은 monitor를 빌린다.

연차는 병합된 PR79 수정본의 `HttpLeaveWorkflow.cs`를 같은 captured Session당 하나 설치한다. 일부 소스만 있는 별도 빌드에 adapter가 없으면 연차 미연결 안내를 제공하고 지문·근무 경로는 계속한다. 연결 설치가 실제 연차쓰기 허용을 의미하지 않으며 D1/D2가 미설정이면 기존 readiness/버튼 차단을 유지한다.

2026-10-23 일반 EXE 연결 변경 후 `Tests/WorkdayActivity.Tests` **170항목**과 기존
`Tests/IdentityLogin.Ui.Tests` **179항목**을 통과했다. 전자는 기존124항목에 설정 누락/손상/다른 서버의
일반 메뉴 유지, 표시 후1회 연결, 서버404/503의 지문 바·비활성 버튼, 최소 창 크기, 동일 계정 재로그인과
늦은 응답 차단을 추가했다. 호환 실행기의 실제 `--check-config` 명령 검사도 유지한다.
실제 MainForm/공통 reader/HTTP를 사용하되 계정·현재 근무·monitor·연차 workflow는 명시 대역이다.
설정 오류 검사도 소유 임시 파일만 사용한다. 운영 DB·실제 장비·계정 매핑·키 준비 완료를 의미하지 않는다.
두 실행 DLL의 실제 초기화에서도 DPI 설정과96DPI가 일치함을 별도 확인했다.

PR79의 R79-01/02 수정은 팀장8730b9e·원본 병합dc82322를 수신했다. 이번5번 변경에서
서버 권한 로직을 재구현하지 않았다. 아래는 PR79 병합 전의 실행 이력이며 현재 상태와 구분한다.

## 2026-10-23 실행 근거

- 고정 소스 `285c8aecb615ba9b77b6dab4b672f4dd41a63d85` + PR79 `88f78fa8364a2e50129c20ef3e5ea14739c96cbc`, 외부 결합tree `0c19b4397e6214af9f33855a5a142ccb0296291a`. PR79는 아직 제품alpha에 미병합이다.
- Release0경고/0오류, startup/workday81·연차UI144·기존지문로그인UI179 PASS. 실제 개인정보/계정으로 로그인한 것이 아니다.
- 소유 MySQL8.2·실제 C#/Flask/Pi HTTP의 기존 identity/정오/퇴근14 PASS47.54초, 연차4 PASS22.66초. 각 소유DB/runtime 정리true. UART/OLED·키/열린근무/monitor는 fixture이며 실제 카메라·실물 성공과 구분한다. 새 startup조립은 위81의 합성 연결 검사이고 기존14를 새 실행기 end-to-end로 재분류하지 않는다.
- Workday DLL SHA256 `bc0c04aded85c4524d4ea12bfe578b5ee079679c30b8b81786a3dd11b39c3d9d`, Leave DLL `a71e463309286359be673d1c4c88e0520d0770bb9772959a99fa9db1007f548d` 전후 일치.
- 미실행: 실제계정 연차쓰기/새지문 활성화, 신뢰 restart-readiness 공급 및 Linux 비공개4키 점검, 실제 카메라·센서의 통합 로그인/정오/퇴근·두PC. 실제 activation은 서버/장치 키·회수·기존 불명작업/설정 상호확인 뒤 별도로 진행한다.
