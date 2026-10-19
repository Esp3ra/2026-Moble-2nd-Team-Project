# 005 채팅 저장 DDL — 1번 기술 검토

2026-10-19. [SQL](005_chat_storage.mysql.sql)은 PR21 head907ae77의 rooms/members/messages 구조를 기준으로 1번이 번호·자료형·FK·인덱스를 검토한 **개발용 DDL**이다. 채팅 제품 코드는 별도 PR21이며 이 SQL 게시로 병합·실제 DB 적용·운영 승인되지 않는다. 글로벌 schema version은1을 유지하며 파일005를 version5로 기록하지 않는다.

## 구조와 변경 이유

- UUID 방/메시지 식별자는 VARCHAR(36), 내부 정렬 seq는 signed BIGINT, users FK는 현재 공통 users.id와 같은 signed INTEGER다. 회원·방 관계 FK와 메시지 UUID UNIQUE, 참여자/숨김·방별 seq 인덱스를 유지한다. 사용자/메시지/방의 cascade 삭제는 추가하지 않는다.
- InnoDB·utf8mb4·utf8mb4_unicode_ci를 명시한다. 기존 번호 미지정 PR21 SQL은 CHARSET만 명시해 MySQL8.2에서 ORM/SETUP의 unicode_ci와 다른 기본 collation이 선택됨을 독립 검증에서 확인했다. API 런타임 실패를 발견했다는 뜻이 아니며, 이 파일로 물리 DDL을 명확히 맞춘다.
- source의 제목/표시명/본문/UTC·NULL·PK·FK·인덱스 계약을 바꾸지 않는다. UUID의 정규 형식·참여자 권한·현재 활성·메시지 길이는 서버 서비스가 검사한다. SQL이 그 전체 규칙을 대신한다고 표시하지 않는다.

## 적용 전후 확인

1. 대상은 새로 만든 폐기 가능한 MySQL 개발 DB여야 한다. 공통 users/auth_sessions/admin_guard/schema_versions를 준비하고 현재 schema version1·users INTEGER와 utf8mb4_unicode_ci를 확인한다. 사용자 .env·공유 DB를 검증 대상으로 지정하지 않는다.
2. 세 chat 테이블이 모두 없는지 확인한다. 하나라도 존재하면 이 SQL을 실행하거나 삭제·자동 변환하지 말고 현재 타입/길이/NULL/PK/UNIQUE/FK·인덱스/엔진/문자셋/collation과 데이터를 비교한다. 기존 CLI의 열 이름 일치 검사는 이 물리 검토를 대체하지 않는다.
3. SQL 세 문장을 부모→자식 순서로 실행하고 첫 오류에서 중단한다. CREATE/ALTER는 MySQL에서 자동 commit될 수 있어 전체 rollback을 약속하지 않는다. 일반 API 요청이나 서버 시작 시 이 SQL을 자동 실행하지 않는다.
4. 실제 DDL의 seq BIGINT, 사용자 INTEGER, UUID/문자열 collation, 회원/방 FK·message UNIQUE·두 조회 인덱스, 공통 사용자/버전 보존을 점검한다. 실패·중복·본인 숨김/타인 기록·현재 권한 거절을 해당 PR21 코드로 검증한다.
5. 공통 초기화는 chat off의 새 프로세스에서 수행한다. PR21 코드가 병합/준비된 개발 환경에서만 독립 채팅 flag로 활성화한다. `Init-Chat`은 ORM 생성 방식이므로 이미 SQL로 준비한 테이블에 대한 후속 물리 검사 없이 migration 완료를 주장하지 않는다. 같은 프로세스에서 모델 import를 되돌려 격리를 검사하지 않는다.

## 보존·복구·공유 적용

본인 숨김은 자신의 hidden_at만 바꾸고 타인·메시지·감사를 삭제하지 않는다. 사용자는 개발 단계에서 자동 삭제하지 않는 기준을 승인했다. 복원/탈퇴·운영 보존 기간은 별도 정책/기능으로 남긴다.

공유 적용 전에는 팀장이 대상 서버/DB·적용자·시간·백업 위치·복원 전용 DB·실행 SHA와 비밀 전달 경로를 확정해야 한다. 비밀번호/실제 접속값/덤프를 Git에 올리지 않는다. 독립 복원 DB에서 공통+기존002/003/004와 채팅 데이터/FK·계정 권한을 검증하고, 실패 시 신규 사용을 차단한 뒤 승인된 백업 복구 절차를 따른다. 기존 운영 테이블 DROP을 복구 방법으로 제시하지 않는다. 이번 작업은 공유 DB·백업·두PC를 실행하지 않았다.

## 재현 검증

원본 서버의 `tests/test_chat_migration_draft.py`는 MYSQL_TEST_URL의 remote_work_test_ 전용 DB에서만 raw005·실제 FK/UNIQUE/인덱스/collation을 검사한다. `tests/test_task_creation_migration.py`는 raw002와 실제 신규 업무 API/관계/receipt/rollback을 대조한다. 환경 준비와 DB 삭제 범위는 [SETUP](../../docs/SETUP.md)의 전용 테스트 기준을 따른다.

실제 dump/새 DB 복원 검사는 기존002 검증의 안전한 소유 DB/보호 설정 파일 도구를 재사용한다. 별도로 `MYSQL_MIGRATION_TEST_URL`의 폐기 가능한 loopback MySQL과 `REMOTEWORK_RUN_TASK_MIGRATION_DRAFT_TESTS=1`, mysql/mysqldump 도구가 필요하다. 실제 채팅 본문·본인 숨김·FK·collation과 공통 사용자/감사/버전을 복원 후 비교한다. 공유 백업을 대신 검증한 것은 아니다.

채팅 자체의 C#·HTTP/MySQL 재검증은 PR21의 고정 코드 복사본에서 별도로 실행했다. 작성자와1번 검토자의 테스트 결과·최초 collation 대조 실패·005 재실행 결과를 LOG/1번 기술 회신에 구분한다. 코드 PR·LOG PR·원본 병합·운영 적용은 각각 별도 단계다.

