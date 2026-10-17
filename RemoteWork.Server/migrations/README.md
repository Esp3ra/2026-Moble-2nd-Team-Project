# DB 변경 기록

현재 공통 스키마 버전은 **1**입니다. 최초 테이블 생성은 `flask --app remotework init-db` 또는 `run.py --init`으로 수행합니다. 공통 모델 정의는 `remotework/models.py`입니다. `init-db/create_all`은 기존 열의 migration이나 공유 DB 적용을 대신하지 않습니다.

| 번호 | 파일 | 상태 |
|---|---|---|
| 002 | [업무 조회 SQL](002_task_queries.mysql.sql) · [검토·검증·복구 절차](002_task_queries.md) | 1번 번호 예약, 2번 개발 검토 초안. 공유 DB 미적용, 실제 버전1 유지 |
| 003 | [공지 기본 SQL](003_notices.mysql.sql) · [검토·검증·복구 절차](003_notices.md) | PR #12 N4 회신으로 1번 번호 예약, 4번 SQL/복구 검토안. 공유 DB 미적용, 실제 버전1 유지 |
| 004 | [공통 요청 결과 SQL](004_write_receipts.mysql.sql) · [검토·복구·검증](004_write_receipts.md) | W1 공통 지원 개발 초안. 003 공지 예약과 분리, 공유 미적용·전역버전1 유지 |

003 공지 검토안을 보존하며004는 별도 공통 지원이다. 004 번호는 전역version4 적용을 뜻하지 않으며 users 공통 FK만 직접 참조한다. 업무 준비 API에는002 업무 테이블이 추가로 필요하다. `db.schema_status(required_versions=...)`는 실제 적용 기록과 요구 기록을 비교하되 물리 DDL/호환성을 증명하지 않는다. 코드·DDL·공유 적용 검토는 [W1 지원 계약](../../docs/WRITE_SUPPORT.md)과 분리해 진행한다.

W1-05 회신에 따라 002와 업무 ORM의 상태 CHECK는 다섯 소문자 코드만 정확히 허용하도록 기존 IN과 HEX 비교를 AND로 결합합니다. SQLite의 TEXT와 BLOB 구분도 유지합니다. SQLite ORM·MySQL ORM·실제 MySQL SQL의 제약을 별도로 검증하며, 기존 공유 테이블의 제약을 자동 변경하지 않습니다.

추가 기능은 합의한 등록 절차로 모델을 import합니다. 현재 업무 조회 모듈은 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`일 때만 등록하며 기본 off를 유지합니다. 한 프로세스에서 이미 import한 모델은 Base.metadata에 남을 수 있으므로 초기 등록 점검은 새 프로세스에서 수행합니다.

이미 배포한 테이블을 변경할 때는 담당자끼리 번호·DDL·FK·데이터 영향을 조정하고 전용 테스트 DB의 전후·실패/복구 검증, 공유 DB 백업·복원 확인을 거쳐 적용합니다. 002의 목표 버전2 지원과 기대 버전 검사는 1번의 별도 코드 검토 대상입니다. 번호 예약이나 SQL 파일 추가만으로 버전 증가·공유 적용·팀 전체 합의 완료로 표시하지 않습니다.

003은 기본 notices 테이블만 대상으로 하며 팝업/확인 테이블 번호는 미정입니다. 002→version2 승인·적용 이후의 version3은 후속 목표안일 뿐이며 공통 실제/기대 버전·부분 적용 지원은 1번 검토/구현 전입니다. 공지 API/ID/404 수용과 DDL 최종 승인·공유 적용을 구분합니다. 테스트는 NOTICE_MIGRATION_TEST_URL과 REMOTEWORK_RUN_NOTICE_MIGRATION_DRAFT_TESTS로 002의 전용 설정과 분리합니다.
