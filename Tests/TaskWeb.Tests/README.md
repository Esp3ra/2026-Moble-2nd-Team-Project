# 업무 웹 읽기 전용 MySQL 검사

`RemoteWork.Server/tests/test_task_web.py`의 현재 인증·관계·읽기 경계를 새 소유 MySQL에서 검사합니다. 사용자 `.env`·설정 DB·실행 중 서비스·SQLite를 사용하지 않습니다. 기존 소유권/Windows Job helper와 실제 raw002/004/006/009를 재사용하며 전역 schema version1과 물리DDL을 보존합니다.

후속 신청함은 `test_task_web_lifecycle.py`와 기존 lifecycle3개 대상(매개변수 포함6검사)을 함께 실행합니다. 기존6검사의 성공 변경은 TESTING·실제 results 비활성·명시 lifecycle 대역이며 실제3번 통합 성공이 아닙니다. 웹은 계속 GET/HEAD/OPTIONS만 제공합니다.

서버 의존성이 설치된 Python으로 다음을 실행합니다. 보고서 폴더는 소스 밖의 **새 경로**로 지정합니다.

```powershell
python Tests/TaskWeb.Tests/run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir 'C:/Program Files/MySQL/MySQL Server 8.2/bin' --report-dir 'C:/검증결과/새업무웹검사'
```

명시 opt-in·Windows·mysqld 경로가 없으면 실행하지 않습니다. 임시 경로/실행 MySQL의 datadir·port·버전·엔진·격리 수준을 확인한 뒤 합성 사용자를 생성하고 Flask의 실제 공통 로그인/요청을 검사합니다. 자격증명·연결 URL·임의 child 출력을 보고서에 남기지 않으며 기존 DB에 초기화/복구/삭제를 실행하지 않습니다. 마지막에는 소유 MySQL/프로세스/DB/runtime을 정리하고 `verification.json`에 안전한 요약만 남깁니다.

## 2026-10-21 직접 실행

- **31 passed in 66.75s**, 실패/생략0.
- cold subprocess: 기본 off의 모델/두 웹 경로 미등록, opt-in 시 기존 두 업무 API와 GET/HEAD/OPTIONS만 유지.
- MySQL·공통 cookie 로그인: 본인/지정 관리자와 관계 회수,전체/5상태·취소,상세 복귀·빈/403/404,strict query/ID,CSRF·만료/채널 분리,HTML 이스케이프,전체 정렬,읽기/거부된 쓰기 DB 무효과,503/500 안내.
- 독립 재검사: raw DDL 불변·schema_versions `[1]`, 소유 자원 정리 확인.

근거는 소스 밖 `ES/tmp/remote_work_member2_task_web_20261021_server/verification.json`입니다. 일반 무DB 실행의1통과/30명시 생략을31개 실제 실행에 더하지 않습니다. 이 검사는 Flask test client의 실제 서버 코드/MySQL 검사이고 loopback HTTP·두 PC·공유 계정의 완료 근거가 아닙니다. 별도 브라우저 확인과 미실행 범위는 [업무 웹 안내](../../docs/TASK_WEB.md)를 따릅니다. 취소·연장 실제 쓰기/3번 제출·첨부 소비·integrated ACK는 검증하거나 활성화하지 않습니다.

## 같은 날 신청함 후속 검사

최종 **62 passed in 136.57s**,실패/생략0입니다. 기존 웹31+새 신청함25+기존 lifecycle6이며 앞선31개를 다시 합산하지 않습니다. 신규는 cold opt-in,내/관리·현재 배정/신청자/관리자 회수·5처리 상태/정렬·strict query·cookie/채널/만료·schema503·이스케이프·전체 목록·무변경·API 필드/결과 보존을 포함합니다. 새 모듈의 단독 수집25개와 무DB2통과/54생략도 실제62개에 더하지 않습니다.

근거는 `ES/tmp/remote_work_member2_task_web_inbox_20261021_server_final/verification.json`입니다. 중간 실행은 작업 입력 전환으로 중단돼 완료로 세지 않습니다. 실제3번 취소/연장 소비·공유 계정/두PC와 integrated ACK는 여전히 미검증입니다.

게시 전 다른 채팅의 PR55/56 병합 alpha `395d70d`를 보존해2번 변경을 합쳤습니다. 그 결합 소스 `129cc1c`에서 동일62개를 다시 실행해 **62통과·실패/생략0,138.70초**,독립 물리DDL 불변/schema1·소유 자원 정리를 확인했습니다. 근거는 `ES/tmp/remote_work_member2_task_web_inbox_20261021_merged/verification.json`입니다. 이 반복62개를 고유 검사 수에 더하지 않습니다. 이후 게시 문서만 갱신하며 다른 담당 제품 검증/실물 등록 완료로 확대하지 않습니다.
