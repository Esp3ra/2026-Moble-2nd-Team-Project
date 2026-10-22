# 연차 화면 체험

`Start-Leave-Preview.cmd`를 더블클릭하거나 저장소 루트 PowerShell에서 실행한다.

```powershell
dotnet run --project .\Tools\LeavePreview\LeavePreview.csproj -c Release
```

.NET8 Windows Desktop runtime과 빌드 가능한 SDK가 필요하다. 실제 서버 로그인·라파·카메라·DB·비공개 설정은 사용하지 않는다. 독립 실행 파일이며 일반 프로그램의 로그인 우회 모드가 아니다.

1. **직원 · 신청과 내역** 탭에서 `연차 신청` → 유형·기간·사유 입력 → `신청`을 누른다. 체험 창의 메모리에만 예시 신청이 추가된다.
2. 신청 창을 닫으면 목록이 갱신된다. `전체 / 검토 대기 / 승인 / 반려`를 바꿔 확인한다. 상태 필터는 이미 받은 목록만 표시하며 추가 통신을 하지 않는다.
3. **담당 관리자 · 검토** 탭에서 신청 행을 직접 선택 → `신청 검토` → 의견을 입력하고 `승인` 또는 `반려`한다. 처리 완료한 신청은 다시 처리할 수 없다.
4. 직원 탭으로 돌아오면 같은 신청의 변경 상태를 볼 수 있다. 신청자5번은 예시 식별값이며 실제 계정이 아니다. 담당이 아닌 예시 직원99번은 양쪽 목록에서 제외된다.

상단에 **실제 저장 없음**을 계속 표시하고 신청/검토 창 제목에도 체험임을 표시한다. 프로그램 종료 시 데이터는 사라진다. 파일/SQL 저장·실제 잔여일 계산·서버 API를 구현한 도구가 아니다. 병가·공가는 잔여 연차를 차감하지 않는 사용자 정책을 화면에 안내한다. 실제 승인 권한/잔여일·휴일·중복 기간·원자적 차감은 서버 연동 후 검증해야 한다.

제품 기본 실행은 기존 `UnconnectedLeaveWorkflow`를 유지한다. 실제 프로그램에서 서버 연결 전 `입력 확인`은 저장 완료를 표시하지 않는다. 체험 어댑터·합성 로그인 handler는 **이 별도 Tools 프로젝트 안에만** 있고 실제 서버 요청을 보낼 transport가 없다.

## 디자이너로 수정

Visual Studio에서 솔루션을 열고 `RemoteWork.WinForms/Features/Attendance/LeaveApplicationForm.cs`, `LeaveDecisionForm.cs`, `AttendanceControl.cs`를 **디자이너 보기**로 연다. 컨트롤 배치는 `.Designer.cs`, 리소스는 `.resx`, 동작은 `.cs`에 분리돼 있다. 도구상자에서 Label·TextBox·ComboBox 등을 배치할 수 있다. 체험 바깥 창은 이 프로젝트의 `LeavePreviewForm.cs`를 디자이너로 연다. 실행 중인 체험 창을 닫은 뒤 다시 빌드한다.

## 검증

```powershell
dotnet run --project Tests/Leave.Ui.Tests -c Release
dotnet run --project Tools/LeavePreview -c Release -- --check
# 선택: 외부 폴더에 화면 PNG 생성
dotnet run --project Tools/LeavePreview -c Release -- --check "$env:TEMP\leave-preview"
```

제품 UI **76검사**(기존57 포함), 도구 **9검사**를 각각 실행한다. 실제 WinForms·캡처한 합성 로그인·메모리 workflow를 사용한다. 목록의 본인/지정 범위, 필터로 권한 확대·구접속 목록 복구 금지, 빈 응답/조회 실패 구분, 신청/검토 UI와 같은 예시 상태의 반영을 확인한다. 실제 MySQL/HTTP API·운영 계정·장비·휴가 차감 검증이 아니며 다른 지문 검사 수치와 합산하지 않는다.
