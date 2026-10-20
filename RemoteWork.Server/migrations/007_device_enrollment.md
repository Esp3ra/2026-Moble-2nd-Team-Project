# 007 지문 등록 전용 개발 DDL

1번 → 5번·팀장 / 2026-10-20. [API·설정·권한·미완료](../../docs/DEVICE_ENROLLMENT_API.md).
사용자가 실제 API/DB 구현을 승인하여007을 사용한다. 006 결과물 예약과002~005 원문을 변경하지 않는다. 전역schema1 유지이며 파일번호7이 전역version7/공유적용 승인은 아니다.

전용 테이블4개: enrollment_devices(실측 프로필/빈 후보), enrollment_jobs(발급/결과), enrollment_claims(단일 슬롯 점유/격리), enrollment_mappings(서버 확정 회원 연결).
InnoDB·utf8mb4_unicode_ci, users INTEGER FK·UUID36·revision/epoch BIGINT, 양의 값/상태 CHECK를 사용한다.
발급자/접속/요청ID, 장치/슬롯, 장치/회원 유일성 및 작업→claim·mapping 복합FK로 대상/슬롯 결합을 보존한다. CASCADE 삭제는 없다.
키/지문 원본/템플릿/평문Bearer/원문 결과는 보관하지 않는다. issuer_session_hash는 작업 접근 결합이며 응답에 노출하지 않는다.

MySQL8.0.16이상 CHECK 지원·공통 users/schema1·별도 검토된 개발 DB가 전제다. 독립 플래그·키·장치·관리자 설정도 필요하다.
`init-enrollment-draft` 또는 [raw007](007_device_enrollment.mysql.sql) 중 하나를 사용한다. 일반 init-db/HTTP가 등록 전용 DDL을 실행하지 않는다.
create_all 성공이 기존 테이블 호환성 증거는 아니다. 기존 이름이 있으면 열/타입/INDEX/FK/CHECK/engine/collation을 비교하고 별도 ALTER를 검토한다.
raw SQL은 자동 ALTER/덮어쓰기하지 않는다. MySQL DDL은 전체 rollback이 아니므로 부분 생성 실패 후 무조건 재실행하지 않는다.

적용 전 백업을 새 DB로 복원해 데이터/FK를 확인한다. 실패 시 플래그를 끄고 부분 생성 범위와 백업을 비교하여 별도 복구한다.
센서가 저장됐을 수 있는 claim은 DROP/행삭제/만료 청소로 복구하지 않는다. 슬롯 실물·원문 결과·mapping 대조와 운영자 승인이 필요하다.
`tests/test_device_enrollment.py`가 raw DDL/복합FK/공통version 보존과 실제 mysqldump→새 DB 복원을 검증한다. 공유 DB·운영 복구·실제 센서 전원장애 검증과 구분한다.

## 2026-10-20 프로필 CHECK 정정

5번 요청으로 capacity를 **센서 보고 전체 용량**으로 명확히 했다. 이전 `capacity <= maximum_slot - minimum_slot + 1`은 용량300/허용1~299를 잘못 거절하고 상한999를 허용했다.
CLI·ORM·raw007·실행 시 검사 모두 `0 <= minimum_slot <= maximum_slot < capacity`로 정정했다. 허용 구간은 전체보다 좁을 수 있으며 후보도 그 구간 안에 있어야 한다. 기존 프로필/템플릿/계정은 자동 변경하지 않는다.

옛007을 이미 적용한 개발 DB는 `create_all`만으로 CHECK가 바뀌지 않는다. 백업/새DB복원과 `SHOW CREATE TABLE enrollment_devices`를 확인하고 다음 읽기 검사로 부적합 프로필을 먼저 찾아 별도 협의한다.

```sql
SELECT device_id, capacity, minimum_slot, maximum_slot
FROM enrollment_devices
WHERE NOT (key_epoch > 0 AND capacity > 0 AND minimum_slot >= 0
           AND maximum_slot >= minimum_slot AND maximum_slot < capacity);
```

부적합 행이 있으면 장치 실측/승인 범위를 대조한다. 임의 삭제/용량 추정/후보 확대/템플릿 변경을 하지 않는다.
부적합 행이 없고 적용자가 변경을 승인한 전용 개발 DB에서만 다음 명시 ALTER를 검토한다. 애플리케이션/HTTP는 이를 자동 실행하지 않는다.

```sql
ALTER TABLE enrollment_devices
  DROP CHECK ck_enrollment_profile,
  ADD CONSTRAINT ck_enrollment_profile CHECK
    (key_epoch > 0 AND capacity > 0 AND minimum_slot >= 0
     AND maximum_slot >= minimum_slot AND maximum_slot < capacity);
```

이 정정은 파일007 개발 초안의 제약 수정이며 새 전역 버전/공유 적용 승인이 아니다. 테스트에서는 옛 CHECK의 오수용·정정 ALTER의 거절/기존행 보존을 실제 MySQL로 확인한다.
