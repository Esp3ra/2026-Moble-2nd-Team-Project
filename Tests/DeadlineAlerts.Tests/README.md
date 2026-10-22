# 마감 알림 정책·수명/UI 검사

`dotnet run --project Tests/DeadlineAlerts.Tests -c Release`

Windows 실제 WinForms 메시지 루프와 명시 IDeadlineAlertSource 대역을 사용한다.
서버 시각 KST 경계·본인/상태/취소·BIGINT/INTEGER·60초/2→4→5분·보충/경과 목록,
닫기/재로그인·다중PC 확인 대역·A→B→A·기한 재검사·현재 창/중요공지/숨김/최소화,
로그아웃/늦은 응답·확인 유실/결과 조회/명시 재시도·옛 사건·동시 확인·조회 순서 경합을 검사한다.
임의 지연이 아니라 실제 Shown/TaskCompletionSource로 순서를 제어한다.

실제 source adapter/API·MySQL 저장/잠금/W1·실서버 MainForm/팝업 통합·물리두PC·90일 history는
미실행/연결 대기다. SQLite나 로컬 가짜 저장소를 실제 MySQL 성공으로 표시하지 않는다.

PR61 보완은 ReviewRegressionChecks.cs에 모았다. 기존78검사에27검사를 추가해 총105검사다.
업무보기401/403의 전체 폐기, 늦은 목록/업무보기 양방향과 두 업무보기 응답 순서,
과거503의 최신 화면 무효화 금지, Read 성공/Recheck503에도2→4→5→5분과 정상 성공60초 복귀를 검사한다.
수정 전 같은27검사에서3결함의 실패를 재현했고 제품 guard/응답이나 시간 지연을 가짜로 바꾸지 않았다.

2026-10-22 후속은 `ConfirmationRefreshChecks.cs`의57검사다. 기존105를 포함해 **162검사**이며
같은 원본에서 새 검사를 먼저 실행해 성공 receipt + snapshot 없음의 NullReferenceException을 재현했다.
제품에서는 확인 성공을 유지하고 GET만 회복하도록 분리했다.

- 성공 receipt + snapshot 누락/불량·다른 업무 유지·같은 사건 재전송0·새 사건에 옛 확인 전이0.
- GET503/형식 오류·2/4분 backoff·전체 목록 회복/60초 복귀·성공 후 업무보기 차단/회복.
- POST 유실→읽기 전용 결과503/410→성공 receipt→GET 회복. 오류를 미기록으로 바꾸지 않음.
- 잘못된 enum/비성공 null 응답, 늦은 GET,401/403·같은 계정 새 로그인·실제 popup/Inbox 버튼.
- 1번 PR64의 실제 `LoginDeadlineNotifications`/captured Session과 합성 로그인 HTTP handler 대조.
  source optional snapshot 전달·공유 scope 성공 보존·구접속 늦은 응답/borrowed source 소유를 검사한다.

이번 검사에는 실제 deadline endpoint/DB/새 action·DDL·영속 receipt 저장소를 설치하지 않는다.
MainForm 실제 페이지 탐색의 기존 통합 검사는 `Tests/DeadlineNavigation.Tests`에서 별도로 실행하며
그24검사를162개에 합산하지 않는다. 실제 source/API/MySQL 저장·두 PC·90일 history는 여전히 후속이다.
