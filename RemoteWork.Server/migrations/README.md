# DB 변경 기록

## 017 공지 원 요청 영속 복구 — 2026-10-23

[017_notice_operations.mysql.sql](017_notice_operations.mysql.sql)은 공지 생성의 원 UUID·본문 해시·최소 영수증·만료 tombstone을 보존하는 별도 테이블 한 개다. 공지 삭제로 원 UUID가 없어지지 않도록 공지 FK를 두지 않는다. 공통004·users·공지 준비 후 비공개 전체 백업/독립 복원·동일 DB/문자셋/FK를 확인해 명시 적용하며 기존 schema1·공지/첨부 구조를 바꾸지 않는다. 시작/HTTP/init-db의 자동 DDL·자동 정리/ALTER/DROP은 없다. [API·웹·실패/복구 경계](../../docs/NOTICE_RECOVERY.md)를 따르며 코드 제공을 실제 공유 적용으로 표시하지 않는다.

## 015 공지·채팅 첨부 참조 — 2026-10-22

1번은 병합된 PR76 a6dc89c의 세 테이블 선언을 구조 변경 없이 [015_notice_chat_attachments.mysql.sql](015_notice_chat_attachments.mysql.sql)로 예약한다. 1번 명시 적용은015를 사용하고 PR76의 `docs/sql/notice_chat_attachments.mysql.sql`은 원 검토안으로 보존한다. 두 경로를 중복 적용하지 않는다. 기존003/005/013·014 마감 예약·schema1을 바꾸지 않으며 번호화는 공유 설치/정책 승인 완료가 아니다. 이미 제공·병합된 기능을3번에게 재구현하거나 별도 회신하라고 요청하지 않는다.

`remotework.resource_attachment_schema.require_resource_attachment_schema(db, optional=False)`를 제공한다. 현재 같은 MySQL Session의 실제 연결에서 세 테이블의 정확한 열/타입/길이/null/default/signed·PK·InnoDB/collation·현재 DB의 부모 FK와 양방향 RESTRICT/NO ACTION·지원 인덱스·강제 CHECK 식을 확인한다. 세 테이블이 모두 없고 optional=True일 때만 False, 부분/잘못된 구조는503 RESOURCE_ATTACHMENT_SCHEMA_REQUIRED다. 성공 전역 cache·DDL/commit/rollback·권한 허용·모델/HTTP 등록은 없다. 1번이 배포 전 준비 검사로 직접 사용하며3번의 runtime 함수·hot-path 캐시를 변경하거나 연결 작업을 요청하지 않는다.

CHECK 비교는 SQL 토큰 경계와 AND/OR 괄호를 유지하며 문자열 리터럴의 원문 bytes를 보존한다. MySQL의 식별자 backtick·키워드 표기·문자열 밖 `_utf8mb4` 도입자는 처리하지만, `HEX('NOTICE')`·`HEX('not ice')`처럼 실제 제약이 다른 리터럴은 정상015와 같게 취급하지 않는다. [원본 결함 대조·회귀 실행](../../Tests/ResourceAttachmentSchema.Tests/README.md)을 확인한다.

적용 전 부모003/005/013의 실제 타입·현재 FK/문자셋과 동일 DB를 확인하고, 기존 DB 전체·외부 파일 저장소를 비공개 백업해 독립 복원한다. 실제 공지·채팅 현재 권한 콜백/동일TX 소비·원 요청 복구/삭제·보존 경계까지3·4번과 검증한 뒤, 새 세 테이블의 완전 부재를 확인해 raw015를 한 번 명시 적용한다. 일부만 존재하면 자동 CREATE 재시도/ALTER/삭제하지 않고 원인을 대조한다. 서비스 시작/init-db에 자동 적용하지 않는다.

문제 발생 시 해당 자원의 새 첨부 소비를 중지하고 기존 참조·업로드·파일·감사를 보존한다. 일반 task/submission 기능을 대체하거나 역할/admin만으로 우회하지 않는다. 새 참조가 생긴 뒤 테이블 DROP·덤프 덮어쓰기나 채팅 숨김→파일 삭제로 복구하지 않는다. 공지 물리 삭제의 해제/보존과 영속 요청UUID/W1·파일 원문 대조는1·3·4번 공동 후속이다. 실제 사용자 DB·서비스/설정·계정·지문·운영ACK는 이번 번호화/검사 제공으로 변경하지 않는다.

## 014 마감 사건·배정별 확인 — 2026-10-22

[014_deadline_alerts.mysql.sql](014_deadline_alerts.mysql.sql)은 PR70의 두 draft SQL을 구조 변경 없이 합친 명시 적용 파일이다. 기존 draft와 중복 실행하지 않는다. 기존 schema1·업무/회원/배정·확인 보존형 FK와 `(event_id,user_id,assignment_revision)` 키를 유지한다. 앱 시작/전체 init-db로 적용하지 않는다.

사용자는 기존 업무를 도입 시점의 현재 기준 사건으로 명시 등록하도록 승인했다. `deadline_events.establish_baseline`은 현재 인증된 활성 관리자 TX에서 원 업무 버전·배정·담당자·DATE가 검증 계획과 모두 일치할 때만 `task.deadline_baseline` 사건과 동일 TX의 전환 감사(run UUID·사유·배정·기한)를 기록한다. 과거 변경 이력을 소급하지 않고 업무 버전/배정/기한을 바꾸지 않는다. 기존 head는 보존하고 head 없는 고아 기록·계획 변경은 실패한다. 전체 계획은 한 TX로 처리하며 실패 시 전부 rollback한다. 자동 seed·공개 baseline HTTP endpoint는 없다.

적용 순서: 비공개 전체 dump/설정 보존 → 독립 새 MySQL 복원·전행/DDL 대조 → 014·물리 readiness와 현재 기준 전환 리허설 → 원본 변경 여부 재검사 → 같은5000 서비스 중지 → 014 명시 CREATE·기준 전환 → 기존 두 마감 옵션만 명시 ON → 같은5000 재기동·현재 권한/목록/W1 검사. 백업 이후 변경됐으면 다시 백업·검증한다. DDL은 MySQL에서 비트랜잭션이므로 부분 실패 시 자동 재실행/삭제하지 말고 생성된 테이블을 점검한다. 기존 테이블 DROP/덤프 덮어쓰기는 복구 기본 절차가 아니다.

장애 시 새 쓰기를 중지하고 두 마감 옵션을 OFF로 되돌려 같은 서버를 재기동한다. 새 사건/확인/감사는 보존한다. 기존 성공 읽기가 필요하면 결과 읽기 옵션만 ON으로 유지하되 현재 인증·원 actor/배정·TTL 검사와 저장소 readiness가 필요하다. 파일/DB 전체 덮어쓰기 복원은 별도 검토하며, 현재 자료와 새 기록을 먼저 보존한다. 새 지문 OFF·취소/연장 운영 ACK는 변경하지 않는다. 이 문서의 적용 절차와 실제 실행 결과는 구분해 회신한다.

2026-10-21: 공용 첨부 추가3테이블의 검토 파일은 [013_shared_attachments.mysql.sql](013_shared_attachments.mysql.sql)로 일원화했다. 기존 `drafts/shared_attachments.mysql.sql`의 바이트는 그대로이며 검사/안내 경로만 맞췄다. 006 결과물 기반·002/004 선행과 공통 schema1을 유지한다. 012는 별도 PR57 협업용 예약이며 번호를 재사용하지 않는다. 소유 MySQL의 rawSQL/PK/FK·물리 조건·새DB 복원 검토와 실제 사용자 DB 적용은 별개다. 기존 DB의 테이블/열 대조·백업/복원과 기능 연결 검증 뒤 명시 적용하며 앱 시작/init-db에서 자동 적용·기존 테이블 ALTER/삭제·전역version13 기록을 하지 않는다. [공용 첨부 계약과 처리 경계](../../docs/SHARED_ATTACHMENTS.md)를 따른다.

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
| 010 | [등록 MySQL 증거 SQL](010_enrollment_durability.mysql.sql) · [설명/복구](010_enrollment_durability.md) | 1번의MySQL전용저장전commit/서명결과원문보관용독립1테이블. 기본off,007보존·schema1유지.5번연결/기존SQLite이관·실제복구/운영적용은별도 |
| 011 | [실패 등록 복구 이력 SQL](011_enrollment_recovery.mysql.sql) · [준비·검증·적용 경계](011_enrollment_recovery.md) | 1번의 서명·store 증거 없는 만료 작업 운영자 종료/명시 재발급. 007·010 선행, 독립1테이블·기본off·schema1 유지. 기존 작업/예약 삭제·실물 복구/활성화는 아님 |

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
# 016 연차 — 1번 개발 연결 계약

`016_leave.mysql.sql`은 연차 전용5테이블의 명시 CREATE이며 schema_versions[1]·기존 업무/근태를 유지한다. 번호/정책은 이번 개발 구현이며 팀장·5번 계약수용과 공유적용은 별도로 확인한다. 일부 존재 시 재실행·자동ALTER/DROP하지 않는다. 기본8일은 명시 멤버 준비 시만 적용하고, 담당관리자/확정 휴일/정책 확인값 없이 차감하지 않는다. 전체DB/파일 비공개 백업→독립 복원·전행/DDL/파일 대조→016 리허설·엄격검사→실제적용 순서를 따른다. [API/초기 준비·복구/5번 주입](../../docs/LEAVE_API.md), [소유 MySQL/HTTP/C# 검사](../../Tests/LeaveService.Tests/README.md).
