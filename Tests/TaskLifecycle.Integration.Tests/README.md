# 업무 취소·마감 연장의 소유 MySQL 검증

2026-10-21 현재 `run_mysql_check.py`는 기존 업무 조회/관리 검증의 소유권 검사·Windows Job·임시 MySQL 도구를 재사용합니다. 기존 사용자 DB·실행 중 서버·`.env`·SQLite를 사용하지 않습니다. 현재 공통 테이블과 raw002/raw004/raw006 및 PR51의 **실제 `009_task_lifecycle.mysql.sql` 검토본**을 새 DB에 준비합니다. 보존한 미번호 초안과 중복 적용하지 않으며 공유 적용·새 번호·공통 schema1 변경을 실행하지 않습니다.

서버 검사 실행 예시:

```powershell
& <서버 Python> -X utf8 .\Tests\TaskLifecycle.Integration.Tests\run_mysql_check.py `
  --allow-disposable-mysql `
  --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin' `
  --server-pytest-only
```

실행에는 Flask·SQLAlchemy·PyMySQL·pytest가 설치된 기존 서버 Python을 사용합니다. `MYSQL_TEST_URL`과 물리DDL 검사용 `MYSQL_MIGRATION_TEST_URL`/명시 opt-in은 새 소유 DB의 주소만 하위 프로세스에 전달하며, pytest 제한 시간은 600초입니다. 비밀번호·토큰·원본 pytest 출력·HTTP 본문을 출력하거나 보고서에 저장하지 않습니다. 실패 시 정제한 실패 노드만 보고합니다.

서버 모드는 명시한 MySQL bin 경로의 `mysql.exe`·`mysqldump.exe`도 시작 전에 확인하고,검사 하위 프로세스의 `MYSQL_MIGRATION_TEST_BIN_DIR`로만 전달합니다. 복원 시험의 비공개 옵션/덤프 파일 ACL을 위해 현재 Windows 계정 식별 환경변수2개만 추가 전달하며 값을 출력하지 않습니다. 시스템 PATH를 바꾸거나 다른 설치본을 자동 선택하지 않습니다.

결과와 성공/실패 정리 여부는 삭제 대상 runtime 바깥의 새 `ES/tmp/remote_work_member2_lifecycle_mysql_*` 폴더에 `verification.json`으로 보존합니다. `--report-dir`로 소스 밖 새 폴더를 명시할 수 있습니다. `--exercise-cleanup-failure`는 DB 준비 직후 의도적으로 실패하여 소유 자원 정리를 확인합니다.

`--server-pytest-only --pytest-filter 'changed_assignee or physical_generated or missing_storage'`처럼 변경 영향이 있는 검사만 선택할 수도 있습니다. 서버 검사 뒤 물리 DDL과 전역 schema1을 독립 대조합니다. DDL이 다르면 애플리케이션 행·비밀번호·토큰 없이 **변경된 테이블 정의 줄만** 안전 보고서에 남겨 fixture 복구 문제를 확인할 수 있습니다. 자동 증가 할당기의 현재 값만 제외하고 타입·열·인덱스·제약·FK를 비교합니다.

성공하는 취소·연장 흐름은 **TESTING=true·3번 결과물 blueprint 비활성화·명시 기술 연결 테스트 대역**에서만 진행합니다. 서버 모드는 다음5모듈을 실행합니다.

- `test_task_lifecycle.py`:2번 상태/권한·동시성·동일 요청/복구와 미연결 차단
- `test_task_lifecycle_migration.py`:1번의 raw009 물리 제약·현재DB FK·dump/새DB 복원
- `test_lifecycle_result_reads.py`:PR51의 기능off 후 기존 성공 읽기·현재 권한·새 쓰기 차단
- `test_task_lifecycle_model_contract.py`:다른 DB 기본값에서도 ORM이009의 엔진/문자셋/정렬과 한글·이모지 보존
- `test_task_lifecycle_real_boundary.py`:실제3번 results 등록과 정상 GET 후,ACK 누락/불일치/TESTING대역×5경로의503·write_enabled=false·DB 무효과 확인

마지막 모듈은 실제 결과물 경로가 존재해도2번 쓰기를 열지 않는 **미연결 경계 검사**입니다.3번 제품의 취소/pending 소비를 대신 구현하거나 integrated ACK를 주입하지 않습니다. 실제3번 취소/연장↔제출·첨부 경합과 공유 서버·실계정·두PC 검증은 별도입니다. 안전 보고서의 `member3_lifecycle_consumption_verified`와 `shared_environment_verified`도 false를 유지합니다.

`--server-pytest-only`를 생략하면 실제 임시 MySQL과 무작위 loopback 포트의 Flask 서버를 사용한 HTTP 흐름을 검사합니다. 업무 상태·신청·판단·취소는 실제 API로만 만들며, 업무/제출 상태를 직접 seed하거나 성공으로 꾸미는 전용 HTTP 경로를 제공하지 않습니다.

- 직원 업무 등록·수락, 연장 신청·중복 거절·철회·재신청·지정 관리자 승인/반려, 요청 이력 조회
- 관리자 본인 업무의 수락·신청과 자기 판단 금지, 판단자 없는 신청 차단, 현재 지정 관리자 회수
- 연장 대기 중 취소 시 신청 종결, 기본 목록 제외·취소 포함·현재 권한에 따른 상세 열람
- 취소 후 수락·재작업·일반 수정·재배정·관리자 변경·신규 연장 차단
- 같은 키의 성공 재응답과 결과 복구, 연결 확인 제거 시 503·업무/신청/receipt 무효과
- 커밋된 DB의 배정·최초 수락·설명·이전 마감/신청 이력·감사/receipt·물리 DDL·전역 schema1 보존

최종 실제 HTTP 실행은 **391 시나리오 검사 + 독립 MySQL 15검사**를 통과했습니다. 업무/배정/취소 각3, 연장 이력5, 성공 receipt/감사 각19를 독립 DB 조회로 대조했고, 소유 HTTP·MySQL·DB·runtime 정리를 확인했습니다. 3번 결과물 테이블은 빈 상태 보존만 확인했으며 실제 제출·첨부·판단 경합을 검사했다는 뜻은 아닙니다. 이 수치는 서버 pytest·C# 화면 검사와 구분합니다.

2026-10-20 미번호 초안 당시 서버 정책 검사는 **56개 통과 후, 마지막 권한 보완·DDL 복구의 영향 4개를 추가 확인**했습니다. 4개에는 기존 스키마 검사의 재실행 3개와 새 현재 담당자/신청자 불일치 차단 1개가 포함됩니다. 당시 물리 DDL/schema1 및 정리도 통과했으며 중복 실행을 누적한 숫자로 보고하지 않습니다. 소유 DB 준비 직후 의도한 실패에서도 DB·프로세스·runtime 정리를 확인했습니다. 위의5모듈/정식009 기준 최신 실행 결과는 [LOG 현재 검증 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#member2-lifecycle-integration-check-20261021)을 따릅니다.
