# DB 변경 기록

현재 공통 스키마 버전은 **1**입니다. 최초 테이블 생성은 `flask --app remotework init-db` 또는 `run.py --init`으로 수행합니다. 공통 모델 정의는 `remotework/models.py`입니다. `init-db/create_all`은 기존 열의 migration이나 공유 DB 적용을 대신하지 않습니다.

| 번호 | 파일 | 상태 |
|---|---|---|
| 002 | [업무 조회 SQL](002_task_queries.mysql.sql) · [검토·검증·복구 절차](002_task_queries.md) | 1번 번호 예약, 2번 개발 검토 초안. 공유 DB 미적용, 실제 버전1 유지 |
| 003 | [공지 기본 SQL](003_notices.mysql.sql) · [검토·검증·복구 절차](003_notices.md) | PR #12 N4 회신으로 1번 번호 예약, 4번 SQL/복구 검토안. 공유 DB 미적용, 실제 버전1 유지 |
| 004 | [공통 요청 결과 SQL](004_write_receipts.mysql.sql) · [검토·복구·검증](004_write_receipts.md) | W1 공통 지원 개발 초안. 003 공지 예약과 분리, 공유 미적용·전역버전1 유지 |
| 005 | [채팅 저장 SQL](005_chat_storage.mysql.sql) · [검토·검증·복구](005_chat_storage.md) | 2026-10-19 PR21 구조를1번이 검토해 번호·콜레이션을 명시한 개발 DDL. 채팅 코드 PR21·기존 번호 미지정 SQL의 정리와 연결, 공유 미적용·전역version1 유지 |
| 006 | [결과물 SQL](006_results.mysql.sql) · [기능·설정 안내](../../docs/RESULTS.md) | 최초에는 번호만 예약했으나 현재 PR46으로3번 개발 SQL/API가 원본 alpha에 병합됨. 결과물/판단·첨부·이력과 명시 개발 관계 저장소를 포함하며 공유 적용/운영 관계 확정·전역version6 승인은 아님 |
| 007 | [지문 등록 SQL](007_device_enrollment.mysql.sql) · [설정·검증·복구](007_device_enrollment.md) | 2026-10-20 사용자 승인으로1번 서버 API와 함께 개발 구현. 독립 metadata/명시 초기화·기본off·공유 미적용·전역version1 유지 |
| 008 | [공지 팝업 SQL](008_notice_popups.mysql.sql) · [검토·검증·복구](008_notice_popups.md) | PR45 검토 후 원본 alpha 병합. 공지 삭제 시 해당 확인 행 CASCADE 삭제·삭제 감사 유지 정책 확정. 기본off·명시 준비·공유 적용은 별도, 전역version1 유지 |
| 009 | [업무 취소·연장 SQL](009_task_lifecycle.mysql.sql) · [검토/검증/적용 경계](009_task_lifecycle.md) | 최초 번호 예약 후2번 alpha73a05d5의 개발 초안을1번이 대조해 번호화. 신규2테이블의 InnoDB/utf8mb4_unicode_ci 명시,3번 소비 검증/전체 배포확인·공유 적용은 미완료. schema1 유지·전역version9 승인 아님 |

005·006은 별도 개발 SQL이다. 최초 006 번호 예약 당시의 미작성 안내를 PR46 병합 상태로 정정한다. 002의 신규 등록 초기 업무/배정 버전1/1과 공통 schema version1은 서로 다른 값이다. 3번의 제출 참조는 기존 `task_assignments(task_id, revision)`을 사용하며, 실제 006과 ORM의 참조 인덱스·복합 FK를 함께 대조한다. MySQL의 즉시 FK 검사 때문에 현재 `tasks`의 배정 포인터에 순환 FK를 무단 추가하여 최초 등록 순서를 깨뜨리지 않는다.

003 공지 검토안을 보존하며004는 별도 공통 지원이다. 004 번호는 전역version4 적용을 뜻하지 않으며 users 공통 FK만 직접 참조한다. 업무 준비 API에는002 업무 테이블이 추가로 필요하다. `db.schema_status(required_versions=...)`는 실제 적용 기록과 요구 기록을 비교하되 물리 DDL/호환성을 증명하지 않는다. 코드·DDL·공유 적용 검토는 [W1 지원 계약](../../docs/WRITE_SUPPORT.md)과 분리해 진행한다.

W1-05 회신에 따라 002와 업무 ORM의 상태 CHECK는 다섯 소문자 코드만 정확히 허용하도록 기존 IN과 HEX 비교를 AND로 결합합니다. SQLite의 TEXT와 BLOB 구분도 유지합니다. SQLite ORM·MySQL ORM·실제 MySQL SQL의 제약을 별도로 검증하며, 기존 공유 테이블의 제약을 자동 변경하지 않습니다.

추가 기능은 합의한 등록 절차로 모델을 import합니다. 현재 업무 조회 모듈은 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`일 때만 등록하며 기본 off를 유지합니다. 한 프로세스에서 이미 import한 모델은 Base.metadata에 남을 수 있으므로 초기 등록 점검은 새 프로세스에서 수행합니다.

이미 배포한 테이블을 변경할 때는 담당자끼리 번호·DDL·FK·데이터 영향을 조정하고 전용 테스트 DB의 전후·실패/복구 검증, 공유 DB 백업·복원 확인을 거쳐 적용합니다. 002의 목표 버전2 지원과 기대 버전 검사는 1번의 별도 코드 검토 대상입니다. 번호 예약이나 SQL 파일 추가만으로 버전 증가·공유 적용·팀 전체 합의 완료로 표시하지 않습니다.

003은 기본 notices 테이블만 대상으로 합니다. 팝업 확인 저장은 최초008 예약 후 PR45의 실제 SQL/검토가 병합됐으며 현재 상태는 아래 절을 따릅니다. 002→version2 승인·적용 이후의 version3은 후속 목표안일 뿐이며 공통 실제/기대 버전·부분 적용 지원은 1번 검토/구현 전입니다. 공지 API/ID/404 수용과 DDL 최종 승인·공유 적용을 구분합니다. 테스트는 NOTICE_MIGRATION_TEST_URL과 REMOTEWORK_RUN_NOTICE_MIGRATION_DRAFT_TESTS로 002의 전용 설정과 분리합니다.

## 005 채팅 저장

1번 PR #25 head2173492의 [005 SQL](005_chat_storage.mysql.sql)과 [안내](005_chat_storage.md)를 동일 내용으로 포함합니다. 기존 번호 없는 chat_storage.mysql.sql을 대체하며 둘을 중복 적용하지 않습니다. ORM 생성도 InnoDB·utf8mb4·utf8mb4_unicode_ci를 명시합니다. 개발 기술 검토와 공유 적용 승인은 별개이며 공통 schema version1을 유지합니다. 기존 테이블은 열 이름뿐 아니라 실제 타입·제약·인덱스·엔진·collation을 비교한 후 처리하고 자동 ALTER/DROP하지 않습니다.

## 008 공지 팝업 확인 — 개발 구현 병합·공유 적용 별도

[검토 SQL](008_notice_popups.mysql.sql) · [설정·검증/복구](008_notice_popups.md). 최초 예약/보존 미합의 기록 이후, 팀장이 **공지와 해당 확인 행 함께 삭제·삭제 감사 유지**를 확정하고 PR45를 병합했습니다. [현재 정책](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#notice-delete-confirmations-policy-20261020)을 따릅니다. 006/007과 분리·전역schema1·팝업 기본off를 유지하며 공유008 적용/활성화는 별도입니다. 과거 ‘확인 보존 미구현’을 현재 코드의 차단 오류로 반복하지 않습니다.

## 009 업무 취소·마감 연장 — 초안 수신·1번 번호화/검증

[팀장 확정 A~E 정책](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-management-policy-approved-20261020)에 따른 D/E의2번 모델/저장·조회/폼·미번호 SQL이 alpha73a05d5에 도착했습니다. 최초 예약 당시의 ‘초안 없음’ 안내를 정정하며1번이 [009 SQL과 검증 경계](009_task_lifecycle.md)를 제공합니다. 신규 D/E 제품/flag/action은2번 구현이고 본 변경이 중복 추가하지 않습니다.3번 실제 제출/판단/첨부 소비 확인 전에는 통합ACK를 넣거나503 차단을 해제하지 않습니다.

- 기존 `task.updated/reassigned/managers_changed`는 현재 지정 관리자 권한과 기존5필드 결과를 사용합니다. 본인을 관리자에서 제외한 성공 뒤 조회403을 성공 취소로 취급하거나 과거 권한으로 우회하지 않습니다.
- `task.deadline_extension_requested/approved/rejected`는 공통 action 허용 목록에 있습니다. 신청의 기존 `_own` 준비 검사와2번이 명시 연결한 승인/반려 callback·실제 route/모델을 구분합니다. 모두 업무5필드와 `extension_id/requested_due_date/due_date`의8필드 최소 결과를 유지합니다. 통합 연결 확인 전 제품 변경은503입니다.
- `task.cancelled`·`task.deadline_extension_withdrawn`은2번이 앱 초기화의 `register_write_action`으로 현재 접근 callback/8필드를 명시 등록합니다. 기존 built-in action을 이 함수로 덮어쓰지 않으며 receipt에 사유/업무 본문·개인정보·파일을 추가하지 않습니다. target은 task_id, 결정 경로 extension_id도 fingerprint에 넣는 계약을 대조했습니다.
- 신규 D/E 모델은 기존 `tasks.task_id` signed BIGINT·`task_assignments(task_id, revision)`의 BIGINT/INTEGER·users signed INTEGER와 FK 타입/현재 DB를 맞춰야 합니다. 취소는 여섯 번째 업무 상태/물리 삭제로 구현하지 않고 이력을 보존합니다. 연장 대기1건의 DB/잠금 경계·동시 신청/판단·업무 버전 증가를 명시한 초안이 필요합니다.
- 기존3번 `ExternalFacts`에는 `reviewer_id/extension_pending`만 있고 취소 여부가 없습니다.2번의 취소/연장 저장과3번의 제출·판단은 동일 task/배정 잠금·현재 관계·유효 기한을 같은 DB transaction에서 재검사하도록 연결해야 합니다. 현재의 명시 개발 `SubmissionGate`를 실연장 신청 모델이나 공유 환경의 기본 false로 취급하지 않습니다.
- 1번은 수신한 초안을 전용MySQL에서 대조하고 raw009 FK/제약·정책/권한·재전송·새DB 복원까지64검사를 통과했습니다.2번의 TESTING 대역과3번 실제 경로는 구분하며,3번 제출·판단·단계별 첨부의 취소/연장 경합과 실제 대상 환경/백업복구/공유 적용은 후속입니다. 전체 통합ACK·공유 적용 완료로 표시하지 않습니다.
