# 현재 근무 조회 소비 검사

`dotnet run --project Tests/WorkdayState.Tests -c Release`로 실행한다.
실제 captured ApiClient에 명시 HTTP 대역을 연결한58검사이며 실제 서버·DB·센서·두PC 검증은 아니다.
본인 근무 UUID/날짜/UTC·정오 기록/대상·불량 필드/불명 상태·401 폐기와403/404/409/503 구분을 확인한다.
조회는 GET 한 번이며 자동 재시도·근태 쓰기·token 설치·MainForm 일정/감시 조립을 하지 않는다.
서버 KST10:59:59/11:00/11:59:59/12:00의 `noon_due`와 시간 밖 잘못된 true,
시간 안 잘못된 false를 검사한다.12시 이후에도 이미 저장된 성공 시각은 그대로 읽는다.
서버 쪽 실제 MySQL 검사는 `RemoteWork.Server/tests/test_identity_workday.py`다.
원본 identity/enrollment fixture를 사용하므로 반드시 새 소유 MySQL 전용 DB와
`MYSQL_TEST_URL`을 사용한다. DB명은 `remote_work_test_` 접두사를 요구한다.
기존 사용자 `.env`/DB·SQLite를 사용하지 않는다.

2026-10-23 관련 실제 서버 band는 identity/현재근무/퇴근 후 재접속을 포함해 소유 MySQL에서
68개 통과했다(독립 정책9개 포함). 11/12 경계·중복·다른PC 재접속·시간 후 승인/완료/첫 저장
차단·원 성공 receipt의 시간 후 조회/재응답을 포함하며 DB/프로세스/runtime을 정리했다.
이것은 서버 단독 band이고 C#58검사나 실제 장비·공유 운영 환경 검증과 합산하지 않는다.
