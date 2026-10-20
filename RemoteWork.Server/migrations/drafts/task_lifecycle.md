# 업무 취소·마감 연장: 2번 소유 미번호 DDL 초안

정책은 2026-10-20 팀장이 확정했습니다. 이 문서는 정책 재승인 요청이 아니라 **공통 DB·배포 기술 연결 대상**입니다. 1번 PR50에서009 번호만 예약한 것을 확인했으며 이 제공물은 SQL 내용 대조용 미번호 초안입니다. `schema_versions`와 기존 테이블을 변경하지 않습니다. 실행 SQL은 [task_lifecycle.mysql.sql](task_lifecycle.mysql.sql)입니다. SQLite 관리·이관은 포함하지 않습니다.

## 소유 저장소

- `task_cancellations`: 업무당 한 취소 표시. 실제 업무·배정·결과·파일·검토를 삭제하지 않습니다. 사유·실행자·UTC 시각을 보존합니다. 취소 상태를 여섯 번째 업무 상태로 추가하지 않습니다.
- `task_deadline_extensions`: 신청 당시 배정에 복합 FK, 원래/희망 마감일·신청자·사유·대기/승인/반려/철회/취소 이력을 보존합니다. 생성된 nullable `pending_revision`의 `(task_id,pending_revision)` UNIQUE로 배정당 대기 1건을 DB에서도 강제합니다. 종료 이력은 삭제하지 않습니다.
- 식별자·row_version은 signed BIGINT, 배정 차수·사용자 ID는 signed INTEGER입니다. HTTP BIGINT는 10진 문자열, 사용자 ID/차수는 JSON 정수, 시각은 UTC ISO 8601입니다. 날짜 판단은 한국 시간입니다.
- FK는 CASCADE DELETE를 사용하지 않습니다. `created_at`/`decided_at`은 기존 공통 DATETIME 정밀도와 같습니다.

## 실행 경계와 배포 확인

기본 꺼짐 `REMOTEWORK_ENABLE_TASK_LIFECYCLE_DRAFT=1` 또는 app config `ENABLE_TASK_LIFECYCLE_DRAFT=True`는 모델·읽기·쓰기 **경로 등록**만 허용합니다. 등록만으로 쓰기가 실행되지 않습니다. 모든 취소·연장 mutation은 공통 쓰기가 활성화되고 물리 테이블/대기 제약이 대조되며 아래 버전별 배포 확인이 명시될 때만 실행됩니다.

```python
app.extensions['task_lifecycle_integration_ack'] = {
    'contract': 'member2-task-lifecycle-v1',
    'schema': 'task-lifecycle-draft-v1',
    'mode': 'integrated',
    'member1': 'common-write-and-ddl-v1',
    'member3': 'cancel-and-extension-submit-guards-v1',
}
```

이 값은 기능 플래그를 편하게 우회하는 설정이 아닙니다. 1번의 공통 action/키/결과·DDL 적용과 3번의 취소/연장 대기 제출 차단이 실제 같은 배포에서 검증된 뒤 연결 담당자가 주입해야 합니다. 현재 2번 구현은 이 확인을 자동 주입하거나 다른 담당자의 코드를 수정하지 않습니다. 없거나 불일치하면 `503 TASK_LIFECYCLE_INTEGRATION_REQUIRED`; GET context의 `write_enabled`는 false입니다.

테스트 전용 `TEST_DOUBLE_ACK`는 `TESTING=True`, 결과물 blueprint 비활성인 별도 MySQL fixture에서만 허용됩니다. 실제 3번 제출·검토 통합을 검증했다는 뜻이 아닙니다. 실결과 blueprint를 활성화하면 이 대역은 거부됩니다.

## 1번 연결 항목

1. 미번호 SQL의 공통 migration 순서·실제 DDL·백업/배포/복구를 대조합니다. 정책이 아닌 기술 적용 확인입니다.
2. 기존 action `task.deadline_extension_requested`, `_approved`, `_rejected`와 신규 action `task.cancelled`, `task.deadline_extension_withdrawn`의 등록을 확인합니다. 신규 action은 기존 공통 등록 API로만 등록하고 타 authorizer를 덮어쓰지 않습니다.
3. 모든 action의 target은 task ID입니다. approve/reject/withdraw는 HTTP 본문에 없는 경로의 extension ID를 내부 receipt fingerprint에 포함하므로 다른 신청에 키를 재사용하면 409입니다. 성공 최소 결과는 8필드만 보존합니다. 키는 현재 권한을 대체하지 않습니다.
4. 현재 지정 관리자/현재 담당자가 바뀌면 기존 성공 receipt도 현재 권한을 다시 검사합니다. 잃은 권한으로 키 조회·재실행을 우회하지 않습니다.

## 3번 연결 항목

`remotework.features.tasks.lifecycle_support`는 결과물/검토 ORM을 import하거나 수정하지 않는 읽기 API를 제공합니다.

```python
ensure_not_cancelled(db, task_id)  # 공유 guard 및 현재 관계 검사 후: 취소면 409
pending_extension_facts(db, task_id, current_assignment_revision, lock=True)  # bool
```

3번 담당자가 실제 결과 제출·재제출·검토·첨부의 각 쓰기 경로에서 취소를 막고, 진행중 제출 준비/제출 트랜잭션에 같은 현재 배정의 대기 연장을 연결해야 합니다. 기존 reviewer 공급자는 유지해야 하며 2번은 결과 검토자를 지정하지 않습니다. 업무 취소 후에도 기존 결과·파일·검토 이력의 허용된 읽기는 보존합니다. 상태·수정 버전/배정은 같은 guarded transaction에서 다시 확인합니다.

잠금 순서는 공통 guard → actor/session → 업무 → 현재 배정 → 관리자 관계 → 관련 사용자 ID 오름차순 → 신청 행입니다. 네트워크 호출을 트랜잭션 안에 넣지 않습니다. 취소는 대기 연장을 같은 트랜잭션에서 `cancelled`로 끝냅니다. 연장 승인은 마감일과 row_version만 바꾸며 업무 상태·수락·제출 이력을 변경하지 않습니다.

## flag off의 기존 데이터

2번 조회/수락/관리 기능은 lifecycle 기능을 나중에 꺼도 기존 취소 테이블을 감지하고 취소 업무의 쓰기를 차단합니다. 기본 목록은 제외하고 `include_cancelled=true`로 열람합니다. 두 테이블이 모두 없고 flag off일 때만 기존 동작을 유지합니다. 부분 테이블·필수 열·생성 열·단일 대기 제약 불일치는 503으로 닫습니다. feature off에서 metadata를 등록하기 위해 ORM을 강제 import하지 않습니다.
