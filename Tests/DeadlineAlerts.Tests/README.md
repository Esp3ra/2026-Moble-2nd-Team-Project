# 마감 알림 정책·수명/UI 검사

`dotnet run --project Tests/DeadlineAlerts.Tests -c Release`

Windows 실제 WinForms 메시지 루프와 명시 IDeadlineAlertSource 대역을 사용한다.
서버 시각 KST 경계·본인/상태/취소·BIGINT/INTEGER·60초/2→4→5분·보충/경과 목록,
닫기/재로그인·다중PC 확인 대역·A→B→A·기한 재검사·현재 창/중요공지/숨김/최소화,
로그아웃/늦은 응답·확인 유실/결과 조회/명시 재시도·옛 사건·동시 확인·조회 순서 경합을 검사한다.
임의 지연이 아니라 실제 Shown/TaskCompletionSource로 순서를 제어한다.

실제 source adapter/API·MySQL 저장/잠금/W1·공통 MainForm/모달 양방향 조립·물리두PC·90일 history는
미실행/연결 대기다. SQLite나 로컬 가짜 저장소를 실제 MySQL 성공으로 표시하지 않는다.
