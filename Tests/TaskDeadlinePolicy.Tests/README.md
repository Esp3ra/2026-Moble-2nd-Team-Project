# 확정 연장 정책의 로컬 입력 검사

`dotnet run --project Tests/TaskDeadlinePolicy.Tests -c Release`

한국 자정 경계, 다섯 상태별 입력 가능 여부, 마감 전/후·현재 기한과 희망일, 반려완료에서의 연장 준비와 원본 상태 보존을 검사합니다. 실제 WinForms 컨트롤을 실행하되 보이지 않는 위치에 둡니다. 서버·DB·실계정은 사용하지 않습니다. 신청/철회·관리자 판단·기한 반영 및 3번 제출 차단이 구현되었다는 검사가 아닙니다. 접속 변경/창 수명 회귀는 기존 `Tests/TaskInputSession.Tests`를 함께 사용합니다.
