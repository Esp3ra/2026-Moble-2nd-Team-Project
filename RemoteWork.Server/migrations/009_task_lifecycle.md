# 009 업무 취소·마감 연장: 1번 기술 검토 DDL

2026-10-20 1번 → 2·3번·팀장. [2번 제공 계약](../../docs/TASK_LIFECYCLE.md)의 `member2-task-lifecycle-v1`/`task-lifecycle-draft-v1`을 기존 번호 예약009에 대응시킨다. [검토 SQL](009_task_lifecycle.mysql.sql)은 **개발 검토용**이며 공유 적용·운영 배포 승인이 아니다. 공통 schema version1을 유지하고 `schema_versions`를 쓰지 않는다.

## 변경 내용과 소유권

- 2번의 `migrations/drafts/task_lifecycle.mysql.sql`(alpha73a05d5)과 열/타입/PK/FK/CHECK·생성 열/인덱스가 같으며, 신규 두 테이블에 InnoDB·utf8mb4·utf8mb4_unicode_ci를 명시했다. 기존 미번호 초안은 원문으로 보존한다. **두 SQL 중 하나만 적용**한다.
- `task_cancellations`는 기존 tasks를 참조하는 업무당 취소 표시, `task_deadline_extensions`는 이전 배정까지 참조하는 연장 신청/판단/철회 이력이다. 취소의 여섯 번째 업무 상태·물리 삭제·기존 tasks ALTER를 추가하지 않는다. FK는 현재 DB의 부모를 참조하며 CASCADE 삭제를 사용하지 않는다.
- task_id/extension_id는 signed BIGINT, 배정 차수·사용자는 signed INTEGER이다. HTTP의 BIGINT 정규 문자열과 INTEGER JSON정수를 구분한다. 생성 열 `pending_revision`과 `(task_id,pending_revision)` UNIQUE로 배정당 대기1건을 강제하고 종료 이력은 남긴다.
- 2번의 신규 취소/철회 startup 계약과 승인/반려 callback·기존 신청 `_own`을 공통 registry/현재 권한·8필드 결과와 대조했다. 경로의 extension_id도 receipt fingerprint에 포함한다. 번호 부여 때문에 공통 action을 다시 등록하거나 callback을 덮어쓰지 않는다.

## 적용 전 확인

1. 실제 공유 DB에 자동 실행하지 않는다. 별도 소유 MySQL8.0.16+와 기존 공통/002 업무/004 receipt를 준비하고 현재 schema1·tasks/배정/users의 PK/FK signed 타입을 대조한다. 실제3번 경로 검증에는 별도006/결과물 준비가 필요하다.
2. 대상 DB/두 테이블의 존재 여부·현재 자료·SHOW CREATE TABLE을 보관하고 암호/토큰/사유를 공개하지 않는다. 이미 두 테이블이 있으면 이 CREATE를 재실행하거나 `IF NOT EXISTS`로 호환성 차이를 숨기지 않는다. 기존 ORM/미번호 초안의 엔진·collation/생성 열·제약·FK/인덱스까지 대조하고 필요한 이관은 별도 검토한다.
3. 이 SQL은 두 CREATE만 수행한다. MySQL DDL은 암묵 커밋하므로 두 번째 CREATE 실패가 첫 번째 CREATE를 자동 취소하지 않는다. 첫 실패에서 멈춰 부분 준비 상태를 확인하고 기존 데이터를 삭제해 맞추지 않는다.
4. 공통 쓰기/조회·명시 lifecycle flag와 실제 환경 스키마를 확인한다. `require_lifecycle_schema`는 경로의 제한된 준비 검사이며 전체 물리DDL·백업/복구/공유 승인 증거를 대신하지 않는다.
5. `task_lifecycle_integration_ack`의 member1 검토 근거는 이 SQL/테스트와 현재 배포의 공통 연결 대조다. **3번의 실제 제출·판단·첨부 취소 경합/연장 대기 차단이 검증되기 전에는 mode=integrated/member3를 설치하지 않는다.** 본 변경은 어떤 ACK도 주입하지 않는다. TESTING 전용 대역 통과를3번 실제 소비 확인으로 바꾸지 않는다.

## 검증과 복구

`tests/test_task_lifecycle_migration.py`는 소유 `remote_work_test_` MySQL에서 빈 lifecycle 테이블을 raw009로 바꿔 물리 타입/제약·현재DB FK·다른DB 참조 거절·단일 대기/종료 이력·실제 API와 재전송/결과 조회·실제 dump→빈 새DB 복원을 검증한다. 기존 `tests/test_task_lifecycle.py`는 정책/상태/권한·멱등·동시성·연결 미완료 차단을 검사한다.

```text
cd RemoteWork.Server
# MYSQL_TEST_URL/MYSQL_MIGRATION_TEST_URL은 별도 소유 disposable DB만 사용
# REMOTEWORK_RUN_TASK_MIGRATION_DRAFT_TESTS=1, PYTHON_DOTENV_DISABLED=1
python -m pytest -q tests/test_task_lifecycle.py tests/test_task_lifecycle_migration.py
```

1번의 Windows/Python3.14·MySQL8.2 REPEATABLE-READ 소유 환경에서 **64통과/생략0(56.25초)**. 기존57개와 raw0097개를 포함하며 합산하지 않는다. 최초 새 CHECK 검사에서 MySQL3819를 IntegrityError로만 기대한 검사 오류는 해당 OperationalError/정확한 오류번호까지 확인하도록 고쳐 동일 원본 제약이 거절됨을 확인했다. 제품 SQL/제약을 완화하지 않았다.

추가로 고정73a05d5의 별도 검토본에서 제품 코드를 그대로 두고 미번호 SQL 입력만 동일009 바이트로 선택하여, 기존 `Tests/TaskLifecycle.Integration.Tests/run_mysql_check.py`를 실행했다. 실제 loopback HTTP **391검사와 독립 MySQL15검사**가 통과했다. 업무/배정/취소 각3·연장 이력5·receipt/감사 각19·schema1·물리DDL 보존과 소유 프로세스/DB/임시경로 정리를 확인했다. 이 검사도 TESTING 기술 연결 대역/실제3번 blueprint 비활성이며, 위64개/3번 실제 통합/공유 환경 검증과 합산하거나 혼동하지 않는다.

백업은 비공개 옵션 파일로 수행하고 비밀번호를 명령행/로그에 출력하지 않는다. 빈 새DB 복원 후 두 lifecycle 행/제약과 업무·배정·관리자·receipt/감사·schema1 보존을 대조한다. 실제 운영 복구는 검토된 전체 백업과 해당 변경 이후 업무 데이터의 처리안을 포함해야 하며, 이 개발 검사처럼 실제 테이블을 DROP하거나 누락 이력을 추정 복구하지 않는다.

남은 담당: 3번 실제 소비 연결과 경합 검증,2번 원본 미번호 초안/ORM에서의 번호 참조·테이블 옵션 대조,팀장 적용 대상·백업/복구/공유 운영 확인. 실제 계정·공유DB·두PC·부하·운영 이관과 무관한007 장치 준비는 이번009 검증 범위가 아니다. 협업 회신은 기존 PR43 텍스트로 전달하고 팀장이 LOG1/2/3에 취합한다.
