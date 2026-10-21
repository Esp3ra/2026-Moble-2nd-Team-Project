<a id="업무-웹-읽기-전용-mysql-검사"></a>
# 업무 웹 조회·등록·수락·재작업 MySQL 검사

<a id="task-web-remainder-checks"></a>
## 현재 취소·연장 웹 / 첨부 취소 / 연속성 검사

`--suite remainder`는 새 `test_task_web_lifecycle_writes.py`, `test_task_continuity.py`, `test_task_shared_attachment_cancel.py`를 소유 MySQL에서 실행합니다. 기존 raw002/004/006/009 뒤 **실제013 SQL**을 명시 fixture로 적용하며 실행 전후 물리DDL·공통 schema1 불변과 정리를 확인합니다. 공유 DB에는 적용하지 않습니다.

웹5행동의 서명 초안·원본 버전/신청 ID·동일 키 복구·현재 권한·CSRF·이전 로그인·ACK 미준비503·쓰기off 결과조회, 같은 TX의 실제3번 파일 해제/과거 제출과 다른 업무 참조 보존·rollback/재시도/제공부 미준비·오래된 RR snapshot 이후 현재 링크를 검사합니다. 연속성은 완전한2번 이력만 증명하고 누락된3번 구간·형식/중복/미지 행동은None을 유지합니다. 기존4번 bridge 소비 검사도4번 코드 변경 없이 실행합니다.

성공 웹은 명시 TESTING 대역 ACK, 첨부 취소는 실제 results/shared 제공부와 **소유 fixture 한정 INTEGRATED_ACK**를 사용합니다. 실제 results 등록만으로 ACK 없는5행동이503인 경우도 별도 검사합니다. 이 세 범위를 운영 ACK 설치로 표시하지 않습니다.

```powershell
python Tests/TaskWeb.Tests/run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir 'C:/Program Files/MySQL/MySQL Server 8.2/bin' --suite remainder --report-dir 'C:/검증결과/새업무잔여검사'
```

최종 실행은 **342통과·실패/생략0,663.64초**이며 물리DDL불변·schema1·소유자원정리를 확인했습니다. 새3모듈과 기존 lifecycle/결과읽기·웹조회/등록시작/관리/3번진입·실제제출충돌을 포함합니다. 같은 목록은 `--suite remainder-regression`으로 재현할 수 있습니다. 실행 후 이 선택자를 추가하고 실제 보고서의 target 목록과 일치함을 대조했으며 제품 코드는 바뀌지 않았습니다.

이번 최종 실행 결과와 기존 모듈을 함께 돌린 정확한 목록은 [LOG/2번 이번 검증](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#member2-remainder-check-20261021) 및 소스 밖 `ES/tmp/remote_work_member2_remainder_20261021/mysql_final/verification.json`을 따릅니다. 초기93개와 최종 실행을 합산하지 않습니다. 아래80개 등의 수치는 이전 회차입니다.


업무 웹과 기존 등록·시작·관리 API를 새 소유 MySQL에서 검사합니다. 사용자 `.env`·설정 DB·실행 중 서비스·SQLite를 사용하지 않습니다. 기존 소유권/Windows Job helper와 실제 raw002/004/006/009를 재사용하며 전역 schema version1과 물리DDL을 보존합니다. 아래31/62/85/196/81개 결과는 각각 앞선 회차의 기록이며 현재 실행 결과와 합산하지 않습니다.

<a id="task-web-management-checks"></a>
## 현재 수정·재배정·관리자 변경과 PR52 진입 검사

`test_task_web_management.py`의62개 항목을 추가했습니다. 관리 기능만 켠 경로 등록,GET/검색/확인의 무효과,API와 동일 W1 실행/원본 배정 보존,다섯 상태·현재 지정 관리자·취소,처음 초안 버전의 검색 후 유지,CSRF/변조/중복/대상·이전 로그인 차단,현재 후보 재검사,동일 값 거절,관리자 인계 성공/유실 후403,관계 회수·과거 수락/제출 자료 보존·감사 실패 rollback과 같은 키 복구를 검사합니다. 기존 `test_task_management.py`를 함께 실행해 공통 실행 함수 추출 후 API 계약을 대조합니다.

`test_task_web_results_navigation.py`의16개 항목은 PR52 병합 alpha `cbda025`의 기존 `/results?task_id=<id>&view=own` 진입을 검사합니다. 현재 본인·관리자 본인·취소·기능off,관리 업무만의 권한 미확대,필터를 보존한 현재 상세 새로고침,결과물의 현재 관계 재검사와 이전 선택 힌트의 권한 우회 방지를 다룹니다. 이 모듈은 실제 결과물 제출·공용 첨부 소비·배포 ACK·두 PC 검사가 아닙니다.

`test_task_web_member3_integration.py`2개는 실제 기존3번 blueprint와 로컬 개발 검토자 관계를 사용합니다. 업무 웹 수정 감사가3번 관리자 이력에 나타나는 것과,실제3번 제출 성공 후 이전 버전의 관리자 변경이409로 막혀 추가 감사/receipt/관계 변경이 없는 것을 검사합니다. 첨부 없는 소유 합성 DB·로컬 검토자 범위이며 최종 조직·공용 파일·lifecycle 쓰기·배포 ACK 검사가 아닙니다.

최종 `--suite management-web` 실행은 상류 `ba9e312`와 결합한 소스에서 **80통과·실패/생략0,164.56초**입니다. 물리DDL 불변·schema_versions `[1]`과 소유 MySQL/DB/프로세스/runtime 정리를 확인했습니다. 근거는 소스 밖 `ES/tmp/remote_work_member2_task_web_management_20261021_final/verification.json`입니다. 첫280 실행의 새 테스트8실패는 공통 CSRF400 기대와 최초 GET/준비 필터 조건의 테스트 불일치를 수정한 것으로 제품 규약을 완화하지 않았습니다. 그 실행에서 기존 관리API121·기존 웹33/쓰기48의202개는 모두 통과했습니다. 첫 실행의 새 테스트 통과분이나 진단 재실행을 최종80개에 중복 합산하지 않습니다.

최신 완료 결과는 [LOG/2번 구현·검증](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)의 해당 회차 및 소스 밖 `verification.json`을 기준으로 확인합니다. `--suite management-web`은 이 세 신규 모듈,`--suite web`은 기존 웹 읽기/등록·시작도 포함,`--suite management`는 여기에 기존 관리 API,기본 `--suite full`은 전체 지정 회귀를 수행합니다. 서로 다른 실행을 통과 수로 합산하지 않습니다.

```powershell
python Tests/TaskWeb.Tests/run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir 'C:/Program Files/MySQL/MySQL Server 8.2/bin' --suite management-web --report-dir 'C:/검증결과/새업무웹관리검사'
```

PR52의3번 취소/pending 차단·공용 첨부 제공 코드 병합과 운영 통합을 구분합니다. 이전 management-web 회차는013과첨부소비를실행하지않았습니다. 현재 remainder 회차는위실제013/취소참조해제를별도로검사합니다. 등록/수정첨부저장·운영통합ACK·공유DB는활성화하지않습니다. 취소·연장 실제 변경503은 유지합니다.

## 현재 등록·수락·재작업 연결 검사

아래는 수정·재배정·관리자 변경 연결 전 회차의 완료 기록입니다.

**최종 실행:** PR59 병합 전 전체 소유 MySQL **196통과·실패/생략0,504.69초**, PR59와 결합한 소스의 웹 **81통과·실패/생략0,168.15초**를 각각 확인했습니다. 두 실행을 합산한 고유 검사 수로 표시하지 않습니다. 후자는 기존/상류 메뉴 읽기33+신규 쓰기48이며 변경되지 않은 API의 앞선 전체 회귀를 다시 실행한 수치가 아닙니다. 순수 전이/날짜/cold 등록은 별도로 **172통과,17.43초**입니다. 두 MySQL 실행 모두 물리DDL 불변·schema1·소유 DB/프로세스/runtime 정리를 확인했습니다.

근거는 소스 밖 `ES/tmp/remote_work_member2_task_web_writes_20261021_mysql_final/verification.json`과 `..._mysql_merged/verification.json`입니다. 결합 후 웹만 재현하려면 같은 실행기에 `--suite web`을 추가합니다. API 포함 전체는 기본 `--suite full`입니다. commit 후 응답 생성 실패/감사 실패/결과 조회 실패는 명시 주입 검사이며 실제 네트워크 장애나 실제3번 통합의 완료 근거로 확대하지 않습니다.

실행기는 `test_task_web_writes.py`의 cookie/CSRF·정확한 로그인 바인딩·서명 확인/실행·후보 선택·동일 키 재응답/결과 확인·권한/버전/취소/날짜 검사도 유지합니다. API 추출 회귀는 기존 `test_task_creation.py`와 `test_task_start_api.py`를 함께 실행하고,아래 기존 읽기·신청함·상세·취소 guard와 lifecycle6개 검사도 유지합니다. **최종 실행 결과는 [LOG/2번 구현·검증](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md)과 해당 회차의 `verification.json` 후속 기록을 기준으로 확인합니다.** 수집·무DB 생략·서로 다른 반복 실행을 실제 통과 수로 더하지 않습니다.

별도 소유 MySQL·합성 계정의 브라우저에서는 신규 등록 입력→`not_recorded` 결과 확인→명시 실행→직원 로그인→업무 수락→현재 진행중 재조회와,반려 업무의 재작업 시작→진행중을 확인했습니다. 미리보기 자원 정리는 별도 보고서로 확인하며 Flask 검사 수에 포함하지 않습니다. 실제 공유 계정·두PC·작은 폭/모바일·3번의 실제 제출/첨부 차단·경합·integrated ACK는 이 확인의 완료 범위가 아닙니다.

웹은 별도 옵션에서 확인/실행 POST를 제공하며 query-only에서는 기존 읽기 전용을 유지합니다. 서명 확인폼은 열린 폼에서만 복구하며 닫은 브라우저/메뉴 재진입의 보류 요청 복원은 제공하지 않습니다. 자동 새 키·재전송은 없고 웹 기능 off에서는 웹 결과 경로도 닫힙니다. 기존 API 계약·취소/연장 실제 변경503와 ACK 미설치를 유지합니다. [제품 계약과 제한](../../docs/TASK_WEB.md#task-web-create-start)을 함께 확인합니다.

후속 신청함은 `test_task_web_lifecycle.py`와 기존 lifecycle3개 대상(매개변수 포함6검사)을 함께 실행합니다. 기존6검사의 성공 변경은 TESTING·실제 results 비활성·명시 lifecycle 대역이며 실제3번 통합 성공이 아닙니다. 이 읽기 회차의 웹은 GET/HEAD/OPTIONS만 제공했습니다.

상세 정보 후속은 `test_task_web_context.py`20개와 `test_task_cancellation_current.py`3개를 추가합니다. 새 인증 snapshot 이후 독립 트랜잭션이 재배정/취소/권한 회수를 commit하는 실제 MySQL 경합을 검사하고,취소 guard는 현재 버전8을 전달해 버전 충돌이 이전 snapshot의 마커 누락을 가리지 못하게 합니다. 두 신규 모듈은 명시 소유 MySQL opt-in 없이 DB 검사를 수행하지 않습니다. 일반 SELECT가 옛 취소 부재를 보는 조건과 보완 guard의409·무효과를 구분하며3번 소비/운영 ACK를 활성화하지 않습니다.

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

## 상세 취소·연장 대기와 현재 값 후속

등록·시작 웹 연결 전 상세 후속 실행은 **85 passed in 186.70s**,실패/생략0입니다. 기존62+새 상세20+새 취소 guard3이며 앞선 실행 횟수를 통과 수에 더하지 않습니다. 실제 MySQL의 인증 snapshot 이후 독립 재배정/취소/관리자 회수,현재 버전8과 취소 guard409·추가 변경/receipt/감사 없음,기본off·정보/오류/API 필드 보존을 확인했습니다. 독립 물리DDL 불변/schema_versions `[1]`·소유 자원 정리도 확인했습니다.

근거는 소스 밖 `ES/tmp/remote_work_member2_task_web_context_20261021_mysql_final/verification.json`입니다. 환경 전환으로 중단된 앞선 시도는 완료로 세지 않습니다. 새20개 무DB의1통과/19생략·새3개 단독 수집도 실제85개에 더하지 않습니다. 기존 lifecycle 대역과 새 guard의2번 소유 검사를 실제3번 제출/첨부 소비·공유 환경 통합 완료로 확대하지 않습니다.
