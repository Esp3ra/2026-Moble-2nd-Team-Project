# 업무 수정·담당자·관리자 변경 제어 검증

```powershell
dotnet run --project .\Tests\TaskManagement.Tests\TaskManagement.Tests.csproj -c Release
```

명시적 HTTP·workflow 대역을 사용합니다. 실제 공통 ApiClient를 통과한3개 경로/메서드·정확한 JSON 필드·BIGINT 문자열/INTEGER·현재 접속·동일 요청 키와 최소5필드 결과를 검사합니다. 상태별 입력 허용, Unicode 제목/원본 공백/빈 설명, 관리자 선택 복사, 본인 제외와 최소 인원도 포함합니다.

2026-10-20 직접 실행 **82검사 통과**. 실제 WinForms에서 context v1→이전 상세→context v2 경합을 주입해 오래된 입력에 새 버전이 붙지 않는지 검사합니다. 명시적 재조회에서 안정된 context→상세→context를 받은 뒤에만 편집하며, 입력 변경은 이전 확인값을 지우고 별도 확인·실행을 다시 요구합니다. 최소 크기 폼과 작은 관리 업무 페이지의3개 변경 버튼도 컨트롤 경계 안에 있는지 검사합니다.

선택적으로 `--layout-dir <절대 경로>`를 전달하면 합성 자료만 표시한 `management-form-min.png`, `tasks-management-small.png`를 DrawToBitmap으로 저장합니다. 이는 실제 컨트롤의 자동 렌더링이며 수동 UI 사용 검수나 서버 화면 증거가 아닙니다.

불명 응답은 본문/키를 보존하고 결과 확인 전 재전송하지 않습니다. 명시적 not_recorded 뒤 같은 요청/키만 재전송하고, 성공은 명시 확인까지 유지합니다. 자기 관리자 제외 요청의 결과 확인403은 반영 실패로 단정하거나 새 키로 재전송하지 않고 수동 확인 상태를 유지합니다. 계정 변경은 이전 입력·키를 지웁니다. 만료·no-op·잘못된 결과와 연속 편집도 검사합니다.

실제 서버·DB·두 PC·공유 환경 증거가 아닙니다. 별도 `TaskManagement.Http.Tests`의 소유 MySQL·실제 폼/HTTP 검증과 구분합니다.
