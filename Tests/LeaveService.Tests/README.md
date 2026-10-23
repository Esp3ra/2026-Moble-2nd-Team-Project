# 연차 서버/공통 C# 실제 연결 검사

```powershell
dotnet build Tests/LeaveService.Tests -c Release
python Tests/LeaveService.Tests/run_mysql.py
dotnet run --project Tests/Leave.Ui.Tests -c Release
```

첫 두 명령은 소유 native MySQL8.2 RR/원 raw004+016·Flask 인증/W1/연차 API를 검사한다. 환경의 실사용 DB/.env를 읽지 않으며 OS TEMP에 새 DB/서버를 만들고 종료·삭제한다. MySQL8.2 공식 설치경로와 Python requirements/pytest 및 .NET8 Windows가 필요하다. SQLite를 대체로 사용하지 않는다. 공유DB에 pytest를 실행하지 않는다.

8일 기본, 주말/명시휴일 제외, 연차/반차/병가/공가, 예약→승인/반려, 사유/현재 권한·재배정/비활성, 버전/잔여일·중복 경합, 감사 실패 전체 rollback, W1/UUID 중복·원결과·정책OFF/cold 조회·만료tombstone, 엄격 물리FK/CHECK/기본OFF를 확인한다.

마지막 실제 C#→Flask TCP→MySQL fixture는 `HttpLeaveWorkflow` 신청→커밋 후 응답 유실→원UUID복구/POST1회, 목록/잔여/지정검토/반려사유·접속교체를 검사한다. C# assertion은 이 pytest 항목에 포함되므로 합산하지 않는다. 기존 UI 검사115는 별도 대역 검사이며 실제 저장 검증으로 확대하지 않는다.

PR #79 목록 권한 회귀는 `load_identity`가 실제 RR snapshot을 만든 뒤 별도 DB 연결의 협조 트랜잭션이 공통 guard·현재 운영자/세션·대상 회원 잠금 순서로 재배정 또는 비활성화를 커밋하도록 한다. 이전 관리자의 목록에 사유·이름·부서가 노출되지 않고, 현재 관리자 목록·상세 권한과 일치하는지 검사한다. 비활성 신청자와 재배정된 신청자는 SQL `LIMIT` 전에 제외되어 활성 신청자의 페이지와 `next_cursor`를 소비하지 않아야 한다.

직렬화 함수 자체의 현재 조회 권한 재검사, 관리자 지정 삭제·재배정·비활성 뒤에도 유지되는 본인 이력, 비활성 본인의 401과 현재 권한 없는 접근의 403도 별도로 확인한다. 이 회귀는 합성 회원·지정·신청만 사용하며 운영 설정이나 연차 정책을 확정하지 않는다.

2026-10-23 수정 검증: 원본 `88f78fa`는 별도 원본 checkout을 변경하지 않고 위 두 오류를 실제 HTTP로 재현하여 2건 실패를 확인했다. 수정본은 기존 39건을 유지하고 11개 회귀를 추가해 소유 MySQL에서 전체 **50건 통과**했다(139.62초). 전체 실행·정리는 145.59초였고 MySQL/임시 실행 폴더 종료·삭제를 확인했다. 실제 C# TCP bridge의 14개 assertion은 이 50건 중 1개 pytest에 포함된다. 공유 서버·실계정·두 PC와 수동 UI는 이 결과에 포함하지 않는다.

실제 공유 DDL/부여·담당 설정, 팀 정책수용, 기본 메뉴/폼의5번 주입, 실계정·두PC·전체부하/연차 보존·승인취소·달력 변경은 별도이다. 개발 계약은 [LEAVE_API](../../docs/LEAVE_API.md)를 따른다.
