# 현재 근무 조회 소비 검사

`dotnet run --project Tests/WorkdayState.Tests -c Release`로 실행한다.
실제 captured ApiClient에 명시 HTTP 대역을 연결한39검사이며 실제 서버·DB·센서·두PC 검증은 아니다.
본인 근무 UUID/날짜/UTC·정오 기록/대상·불량 필드/불명 상태·401 폐기와403/404/409/503 구분을 확인한다.
조회는 GET 한 번이며 자동 재시도·근태 쓰기·token 설치·MainForm 일정/감시 조립을 하지 않는다.
서버 쪽 실제 MySQL 검사는 `RemoteWork.Server/tests/test_identity_workday.py`다.
원본 identity/enrollment fixture를 사용하므로 반드시 새 소유 MySQL 전용 DB와
`MYSQL_TEST_URL`을 사용한다. DB명은 `remote_work_test_` 접두사를 요구한다.
기존 사용자 `.env`/DB·SQLite를 사용하지 않는다.
