# 연차 서버/공통 C# 실제 연결 검사

```powershell
dotnet build Tests/LeaveService.Tests -c Release
python Tests/LeaveService.Tests/run_mysql.py
dotnet run --project Tests/Leave.Ui.Tests -c Release
```

첫 두 명령은 소유 native MySQL8.2 RR/원 raw004+016·Flask 인증/W1/연차 API를 검사한다. 환경의 실사용 DB/.env를 읽지 않으며 OS TEMP에 새 DB/서버를 만들고 종료·삭제한다. MySQL8.2 공식 설치경로와 Python requirements/pytest 및 .NET8 Windows가 필요하다. SQLite를 대체로 사용하지 않는다. 공유DB에 pytest를 실행하지 않는다.

8일 기본, 주말/명시휴일 제외, 연차/반차/병가/공가, 예약→승인/반려, 사유/현재 권한·재배정/비활성, 버전/잔여일·중복 경합, 감사 실패 전체 rollback, W1/UUID 중복·원결과·정책OFF/cold 조회·만료tombstone, 엄격 물리FK/CHECK/기본OFF를 확인한다.

마지막 실제 C#→Flask TCP→MySQL fixture는 `HttpLeaveWorkflow` 신청→커밋 후 응답 유실→원UUID복구/POST1회, 목록/잔여/지정검토/반려사유·접속교체를 검사한다. C# assertion은 이 pytest 항목에 포함되므로 합산하지 않는다. 기존 UI 검사115는 별도 대역 검사이며 실제 저장 검증으로 확대하지 않는다.

실제 공유 DDL/부여·담당 설정, 팀 정책수용, 기본 메뉴/폼의5번 주입, 실계정·두PC·전체부하/연차 보존·승인취소·달력 변경은 별도이다. 개발 계약은 [LEAVE_API](../../docs/LEAVE_API.md)를 따른다.
