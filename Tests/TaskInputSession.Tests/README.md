# 2번 입력 준비창의 로그인·종료 수명 검사

Windows와 .NET 8 SDK에서 실행합니다.

```powershell
dotnet run --project .\Tests\TaskInputSession.Tests\TaskInputSession.Tests.csproj -c Release
```

실제 `Application.Run` 메시지 루프와 제품 WinForms·ApiClient를 사용합니다. 로그인 응답만 명시적 HTTP 대역이며 업무 조회/키/결과/쓰기 HTTP는 요청 시 실패하도록 막습니다. 실제 서버·DB·두 PC·수동 전체 GUI 검증이 아닙니다.

수정 전 코드는24검사 중5시나리오가 실패했습니다. 수정 후 **43검사·8시나리오**가 통과했습니다. 로그인 교체 직후 UI 취소 콜백이 실행되기 전의 CheckedInput 폐기, 콜백 후 이전 업무/날짜/사유 제거와 확인 버튼 차단, 취소 토큰만 만료된 어댑터, 표시 전 만료, 닫기/Dispose, 기존 기본·미리보기 생성자, 실제 TasksControl 버튼의 연장창 세션 주입을 검사합니다. 업무 GET·키·결과·쓰기 서비스/HTTP 요청0회를 확인합니다.

workflow만 받는 등록 생성자는 원래 확인 버튼이 비활성입니다. 해당 경계 검사만 현재 필드와 일치하는 로컬 확인값을 reflection으로 주입하고, 별도 스레드의 실제 ApiClient.ClearIdentity 후 UI 콜백 전 getter가 null인지 확인합니다. 이 한 검사를 사용자가 실제 등록 버튼으로 만든 입력이나 실제 서버 로그인으로 설명하지 않습니다.

확인값은 불변 로컬 입력이며 저장 권한이 아닙니다. 서버/API·마감 연장 조건·실제 수락/재작업 쓰기·3번 기능은 추가하지 않습니다. 기존 관련 회귀는 TaskQueries.Tests, TaskPreparation.Tests, TaskCreation.Tests, TaskStartPreparation.Tests에서 확인합니다. 실행별 증거와 미실행 범위는 [LOG/2번 구현 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)에 구분합니다.
