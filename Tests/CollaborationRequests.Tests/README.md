# 협업 요청 HTTP·UI 검사

`dotnet run --project Tests/CollaborationRequests.Tests/CollaborationRequests.Tests.csproj -c Release`

실제 WinForms/ApiClient와 명시 HTTP·업무·직원·쓰기 서비스 대역을 사용한다.
BIGINT/배정 타입, 보내기 연결, 서버 성공 후 입력 초기화, 수신·승인·거절 컨트롤,
작은 비모달 창, 닫기 무효과, 결과 불명·영수증 조회·같은 키 재전송·접속 수명 검사를 실행한다.
공유 서버·MySQL·물리 두 PC 검사가 아니다.

서버: `RemoteWork.Server`에서 `python -m pytest tests/test_collaboration_requests.py -q`.
DB 검사는 MySQL만 사용한다. 별도 폐기용 `MYSQL_TEST_URL`이 없으면 건너뛰며 다른 DB로 대체하지 않는다.
해당 URL의 database는 `remote_work_test_`로 시작해야 한다. **해당 전용 DB 테스트 테이블을 재생성한다.**
실제 프로젝트/공유 DB 주소를 넘기지 않는다. MySQL 검사는 반대 응답 동시성·실제 취소 저장소를 추가한다.
