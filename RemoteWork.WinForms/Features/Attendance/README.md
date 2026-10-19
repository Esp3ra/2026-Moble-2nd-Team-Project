# 5번 근태 화면과 활동 상태 확인

`AttendanceControl`의 **활동 상태 확인** 버튼은 별도 `ActivityPresenceForm`을 엽니다. 직원·관리자 모두 자기 PC의 카메라를 직접 시작할 수 있습니다. 관리자에게 다른 직원의 카메라를 보여주는 기능은 아닙니다. 기존 출퇴근·연차 표/잔여 일수는 여전히 예시이며 조회·신청·승인 저장은 미연결입니다.

## 실행

1. 최초 한 번 저장소 `Tools/HandPresence/Setup-Hand-Presence.cmd` 실행. Windows Python 3.13과 모델/전용 가상환경이 필요합니다. 기존 설치가 있으면 재설치하지 않습니다.
2. Python 카메라 시연과 Windows 카메라 앱을 종료합니다.
3. 저장소 루트 PowerShell에서 `dotnet run --project .\RemoteWork.WinForms\RemoteWork.WinForms.csproj` 실행. 기존 공통 서버에 로그인하고 **출퇴근·연차 → 활동 상태 확인**을 누릅니다. 로그인/서버 실행 방법은 루트 README를 따릅니다.
4. 촬영 각도를 손·책상 쪽으로 맞추고 번호0/DSHOW·기본 영역으로 **카메라 시작**. 장치가 다르면 중지 후 번호/방식을 직접 바꿉니다. 영역은 중지 후 수정하고 재시작합니다.
5. 손을 넣기/정지/빼기로 활동 중·자리 비움 표시를 확인합니다. 중지 또는 창 닫기로 카메라를 해제합니다.

창을 여는 것만으로 카메라를 시작하지 않습니다. 처음 프레임 전에는 확인 대기, 수신/추론/전달/표시까지 5초 넘게 오래된 프레임은 영상과 이전 활동 표시를 숨깁니다. 연결·모델 오류는 자리 비움과 구분합니다. 실제 손이 보이지 않거나 손 검출에 실패해도 표시가 바뀔 수 있으며 이 결과는 출퇴근·회원·근무 여부를 저장하거나 판단하지 않습니다. 몸통 검출은 미구현입니다.

## 구현 경계

- 화면·설정·수명 관리는 C# WinForms. 영상 취득·손 모델은 기존 Python `CameraWorker`. Pi 프로그램과는 독립적입니다.
- C#이 저장소에서 `Tools/HandPresence/.venv/Scripts/python.exe`와 `winforms_bridge.py`를 찾아 소유한 자식 프로세스 하나를 실행합니다. 셸 명령 조합·다른 카메라 자동 검색·공통 설정 변경은 없습니다.
- 전용 stdin/stdout JSON line v1: 상태·수신 시각/QPC 초·경과 시간·오류·잘린 JPEG만 메모리로 전달합니다. 각 단계에서 최신 값만 유지하고 과거 결과를 숨깁니다. 네트워크 리스너·파일 영상 저장·공통 서버 업로드는 없습니다.
- CPython 3.13 `time.monotonic()`과 Windows .NET `Stopwatch`의 QPC 시각으로 파이프 전달 전 지연도 계산합니다. 근거: [Python time](https://docs.python.org/3.13/library/time.html#time.monotonic), [Microsoft QPC/.NET 설명](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps). 절대 시각/UTC 근태 기록 계약과는 별도입니다.
- 중지 시 제어 메시지를 보내고 2초 내 종료하지 않으면 이 창이 시작한 프로세스 트리만 종료합니다. 타 Python/카메라 프로그램을 종료하지 않습니다. 창 종료와 직접 Dispose에서도 자원을 해제합니다.
- Designer.cs/resx·기존 공개 생성자·MainForm·인증·업무 API/DB 유지. 문서 D5-01~05 신원·장치 신뢰·제품 정책·저장 합의는 여전히 대기입니다.
- 소스 체크아웃과 준비된 Python 환경에서 실행하는 로컬 연결입니다. Python/모델을 포함한 배포 패키지는 제공하지 않습니다.

## 검증

`dotnet run --project Tests/ActivityPresence.Tests`는 실제 WinForms 메시지 루프·대역 카메라·합성 JPEG와 실제 Python stdio 자식 프로세스로 화면, 지연, 잘못된 응답, 재시작, 종료/강제 종료를 검사합니다. 물리 카메라·공통 서버·DB는 사용하지 않습니다. Python 회귀는 `Tools/HandPresence/README.md`를 따릅니다. 실제 웹캠의 손 인식·조명·가림·USB 단절·장시간 확인은 사용자 로컬 확인과 별도입니다.
