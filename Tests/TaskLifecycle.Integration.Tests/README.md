# 업무 취소·마감 연장의 소유 MySQL 검증

`run_mysql_check.py`는 기존 업무 조회/관리 검증의 소유권 검사·Windows Job·임시 MySQL 도구를 재사용합니다. 기존 사용자 DB·실행 중 서버·`.env`·SQLite를 사용하지 않습니다. 현재 공통 테이블과 raw002/raw004/raw006, **번호 없는** `migrations/drafts/task_lifecycle.mysql.sql`만 새 DB에 준비합니다. 공유 migration 번호·버전을 추가하지 않습니다.

서버 검사 실행 예시:

```powershell
& <서버 Python> -X utf8 .\Tests\TaskLifecycle.Integration.Tests\run_mysql_check.py `
  --allow-disposable-mysql `
  --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin' `
  --server-pytest-only
```

실행에는 Flask·SQLAlchemy·PyMySQL·pytest가 설치된 기존 서버 Python을 사용합니다. `MYSQL_TEST_URL`은 새 소유 DB의 주소만 하위 프로세스에 전달하며, pytest 제한 시간은 600초입니다. 비밀번호·토큰·원본 pytest 출력·HTTP 본문을 출력하거나 보고서에 저장하지 않습니다. 실패 시 정제한 실패 노드만 보고합니다.

결과와 성공/실패 정리 여부는 삭제 대상 runtime 바깥의 새 `ES/tmp/remote_work_member2_lifecycle_mysql_*` 폴더에 `verification.json`으로 보존합니다. `--report-dir`로 소스 밖 새 폴더를 명시할 수 있습니다. `--exercise-cleanup-failure`는 DB 준비 직후 의도적으로 실패하여 소유 자원 정리를 확인합니다.

`--server-pytest-only --pytest-filter 'changed_assignee or physical_generated or missing_storage'`처럼 변경 영향이 있는 검사만 선택할 수도 있습니다. 서버 검사 뒤 물리 DDL과 전역 schema1을 독립 대조합니다. DDL이 다르면 애플리케이션 행·비밀번호·토큰 없이 **변경된 테이블 정의 줄만** 안전 보고서에 남겨 fixture 복구 문제를 확인할 수 있습니다. 자동 증가 할당기의 현재 값만 제외하고 타입·열·인덱스·제약·FK를 비교합니다.

이번 통합 확인은 **TESTING=true·3번 결과물 blueprint 비활성화·명시 기술 연결 테스트 대역**에서만 진행합니다. 실제 3번 취소/연장 차단 연결, 공유 서버·실계정·두 PC 검증이 아닙니다. 실제 연동 완료로 보고하지 않습니다.

`--server-pytest-only`를 생략하면 실제 임시 MySQL과 무작위 loopback 포트의 Flask 서버를 사용한 HTTP 흐름을 검사합니다. 업무 상태·신청·판단·취소는 실제 API로만 만들며, 업무/제출 상태를 직접 seed하거나 성공으로 꾸미는 전용 HTTP 경로를 제공하지 않습니다.

- 직원 업무 등록·수락, 연장 신청·중복 거절·철회·재신청·지정 관리자 승인/반려, 요청 이력 조회
- 관리자 본인 업무의 수락·신청과 자기 판단 금지, 판단자 없는 신청 차단, 현재 지정 관리자 회수
- 연장 대기 중 취소 시 신청 종결, 기본 목록 제외·취소 포함·현재 권한에 따른 상세 열람
- 취소 후 수락·재작업·일반 수정·재배정·관리자 변경·신규 연장 차단
- 같은 키의 성공 재응답과 결과 복구, 연결 확인 제거 시 503·업무/신청/receipt 무효과
- 커밋된 DB의 배정·최초 수락·설명·이전 마감/신청 이력·감사/receipt·물리 DDL·전역 schema1 보존

최종 실제 HTTP 실행은 **391 시나리오 검사 + 독립 MySQL 15검사**를 통과했습니다. 업무/배정/취소 각3, 연장 이력5, 성공 receipt/감사 각19를 독립 DB 조회로 대조했고, 소유 HTTP·MySQL·DB·runtime 정리를 확인했습니다. 3번 결과물 테이블은 빈 상태 보존만 확인했으며 실제 제출·첨부·판단 경합을 검사했다는 뜻은 아닙니다. 이 수치는 서버 pytest·C# 화면 검사와 구분합니다.

서버 정책 검사는 **56개 통과 후, 마지막 권한 보완·DDL 복구의 영향 4개를 추가 확인**했습니다. 4개에는 기존 스키마 검사의 재실행 3개와 새 현재 담당자/신청자 불일치 차단 1개가 포함됩니다. 마지막 실행의 물리 DDL/schema1 및 정리도 통과했으며 중복 실행을 누적한 숫자로 보고하지 않습니다. 소유 DB 준비 직후 의도한 실패에서도 DB·프로세스·runtime 정리를 확인했습니다.
