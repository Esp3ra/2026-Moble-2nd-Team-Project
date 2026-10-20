# DB 변경 기록

현재 공통 스키마 버전은 **1**입니다. 최초 테이블 생성은 `flask --app remotework init-db` 또는 `run.py --init`으로 수행합니다. 공통 모델 정의는 `remotework/models.py`입니다. `init-db/create_all`은 기존 열의 migration이나 공유 DB 적용을 대신하지 않습니다.

| 번호 | 파일 | 상태 |
|---|---|---|
| 002 | [업무 조회 SQL](002_task_queries.mysql.sql) · [검토·검증·복구 절차](002_task_queries.md) | 1번 번호 예약, 2번 개발 검토 초안. 공유 DB 미적용, 실제 버전1 유지 |
| 003 | [공지 기본 SQL](003_notices.mysql.sql) · [검토·검증·복구 절차](003_notices.md) | PR #12 N4 회신으로 1번 번호 예약, 4번 SQL/복구 검토안. 공유 DB 미적용, 실제 버전1 유지 |
| 004 | [공통 요청 결과 SQL](004_write_receipts.mysql.sql) · [검토·복구·검증](004_write_receipts.md) | W1 공통 지원 개발 초안. 003 공지 예약과 분리, 공유 미적용·전역버전1 유지 |
| 005 | [채팅 저장 SQL](005_chat_storage.mysql.sql) · [검토·검증·복구](005_chat_storage.md) | 2026-10-19 PR21 구조를1번이 검토해 번호·콜레이션을 명시한 개발 DDL. 채팅 코드 PR21·기존 번호 미지정 SQL의 정리와 연결, 공유 미적용·전역version1 유지 |
| 006 | `006_results.mysql.sql` (작성 예정) | 2026-10-19 1번이 제출본/판단/검토 관계·첨부 메타데이터/화면 이력의 조정용 번호 예약. 작성3번·업무 접점2번·검토1번. 실제 테이블/제약·공동 계약 검토 후 작성하며 공유 적용/전역version6 승인이 아님 |

005는 별도 개발 SQL이며006은 파일 번호만 예약해 아직 실행할 SQL이 없다. 002의 신규 등록 초기 업무/배정 버전1/1과 공통 schema version1은 서로 다른 값이다. 3번의 제출 참조는 기존 `task_assignments(task_id, revision)`을 사용하며, 제출자 일치까지 DB에서 보장하려면 006에서 참조 인덱스와 복합 FK를 함께 검토한다. MySQL의 즉시 FK 검사 때문에 현재 `tasks`의 배정 포인터에 순환 FK를 무단 추가하여 최초 등록 순서를 깨뜨리지 않는다.

003 공지 검토안을 보존하며004는 별도 공통 지원이다. 004 번호는 전역version4 적용을 뜻하지 않으며 users 공통 FK만 직접 참조한다. 업무 준비 API에는002 업무 테이블이 추가로 필요하다. `db.schema_status(required_versions=...)`는 실제 적용 기록과 요구 기록을 비교하되 물리 DDL/호환성을 증명하지 않는다. 코드·DDL·공유 적용 검토는 [W1 지원 계약](../../docs/WRITE_SUPPORT.md)과 분리해 진행한다.

W1-05 회신에 따라 002와 업무 ORM의 상태 CHECK는 다섯 소문자 코드만 정확히 허용하도록 기존 IN과 HEX 비교를 AND로 결합합니다. SQLite의 TEXT와 BLOB 구분도 유지합니다. SQLite ORM·MySQL ORM·실제 MySQL SQL의 제약을 별도로 검증하며, 기존 공유 테이블의 제약을 자동 변경하지 않습니다.

추가 기능은 합의한 등록 절차로 모델을 import합니다. 현재 업무 조회 모듈은 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`일 때만 등록하며 기본 off를 유지합니다. 한 프로세스에서 이미 import한 모델은 Base.metadata에 남을 수 있으므로 초기 등록 점검은 새 프로세스에서 수행합니다.

이미 배포한 테이블을 변경할 때는 담당자끼리 번호·DDL·FK·데이터 영향을 조정하고 전용 테스트 DB의 전후·실패/복구 검증, 공유 DB 백업·복원 확인을 거쳐 적용합니다. 002의 목표 버전2 지원과 기대 버전 검사는 1번의 별도 코드 검토 대상입니다. 번호 예약이나 SQL 파일 추가만으로 버전 증가·공유 적용·팀 전체 합의 완료로 표시하지 않습니다.

003은 기본 notices 테이블만 대상으로 합니다. 팝업 확인 저장은 2026-10-20 1번 회신으로008 작성·검토 번호만 예약했으며, 실제 SQL/CASCADE·보존·공유 적용 승인은 아닙니다. 002→version2 승인·적용 이후의 version3은 후속 목표안일 뿐이며 공통 실제/기대 버전·부분 적용 지원은 1번 검토/구현 전입니다. 공지 API/ID/404 수용과 DDL 최종 승인·공유 적용을 구분합니다. 테스트는 NOTICE_MIGRATION_TEST_URL과 REMOTEWORK_RUN_NOTICE_MIGRATION_DRAFT_TESTS로 002의 전용 설정과 분리합니다.

## 005 채팅 저장

1번 PR #25 head2173492의 [005 SQL](005_chat_storage.mysql.sql)과 [안내](005_chat_storage.md)를 동일 내용으로 포함합니다. 기존 번호 없는 chat_storage.mysql.sql을 대체하며 둘을 중복 적용하지 않습니다. ORM 생성도 InnoDB·utf8mb4·utf8mb4_unicode_ci를 명시합니다. 개발 기술 검토와 공유 적용 승인은 별개이며 공통 schema version1을 유지합니다. 기존 테이블은 열 이름뿐 아니라 실제 타입·제약·인덱스·엔진·collation을 비교한 후 처리하고 자동 ALTER/DROP하지 않습니다.

## 008 공지 팝업 확인 — 작성·검토용 번호 예약

[검토 SQL](008_notice_popups.mysql.sql) · [범위/적용 금지·검증/복구](008_notice_popups.md). 006 결과물·007 장치 등록 번호와 분리하며 전역schema1을 유지합니다. [1번 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6283590252)이 분리 방향/명시 준비만 기술 수용했습니다. 삭제보존 미합의로 공유/운영 DB 적용 및 기본 공지 삭제 무영향 승인을 보류합니다.
