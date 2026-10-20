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
