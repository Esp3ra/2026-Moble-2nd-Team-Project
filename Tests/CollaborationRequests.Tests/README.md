# 협업 요청 HTTP·UI 검사

`dotnet run --project Tests/CollaborationRequests.Tests/CollaborationRequests.Tests.csproj -c Release`

실제 WinForms/ApiClient와 명시 HTTP·업무·직원·쓰기 서비스 대역을 사용한다.
BIGINT/배정 타입, 보내기 연결, 서버 성공 후 입력 초기화, 수신·승인·거절 컨트롤,
작은 비모달 창, 닫기 무효과, 결과 불명·영수증 조회·같은 키 재전송·접속 수명 검사를 실행한다.
공유 서버·MySQL·물리 두 PC 검사가 아니다.

PR57 회귀는 결과불명→미기록→재전송409→입력 보존/재준비, 실패한 조회의 키 유지,
공유 수신 중 폼 진입 시 팝업/타이머 중복 없음·공유 소유 보존·독립 폼 정리,
중요공지가 이미 보일 때 표시 양보·종료 후 늦은 응답 차단·지연 표시·상태이력 읽기를 추가한다.
Form.Shown 실제 이벤트를 기다리며 임의의 짧은 지연을 성공 조건으로 쓰지 않는다.

서버: `RemoteWork.Server`에서
`python -m pytest tests/test_collaboration_requests.py tests/test_collaboration_review.py -q -W error::pytest.PytestUnhandledThreadExceptionWarning`.
DB 검사는 MySQL만 사용한다. 별도 폐기용 `MYSQL_TEST_URL`이 없으면 건너뛰며 다른 DB로 대체하지 않는다.
해당 URL의 database는 `remote_work_test_`로 시작해야 한다. **해당 전용 DB 테스트 테이블을 재생성한다.**
실제 프로젝트/공유 DB 주소를 넘기지 않는다. MySQL 검사는 반대 응답 동시성·실제 취소 저장소를 추가한다.
PR57 회귀는 RR의 실제 guard 대기를 확인하므로 전용 MySQL 검사 소유자에게 performance_schema 읽기 권한이 필요하다.
dump/새DB 복원 검사는 MYSQL_TEST_BIN(기본 로컬 MySQL8.2 bin)의 mysql/mysqldump.exe와 새 소유 DB 생성 권한을 사용한다.
합성자료만 새 `remote_work_test_collab_restore_*` DB에 복원하고 그 DB만 정리한다. 기존 DB에 복원하지 않는다.
