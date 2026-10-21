# 011 — 실패 등록의 운영자 종료·명시 재발급

담당1 → 5번·팀장, 2026-10-21. [서버 계약·사용 순서](../../docs/ENROLLMENT_RECOVERY.md).

`011_enrollment_recovery.mysql.sql`은 **enrollment_recoveries 한 테이블만 추가**한다. 공통 users/schema1·007 jobs/claims·010 physical receipts가 선행하며 전역 version11을 기록하지 않는다. 기존 테이블 ALTER/DROP, claim 삭제, 원래 ticket/접속 변경, 결과 조회창 연장, 센서 명령은 없다. 일반 init-db/007 초기화와 HTTP가 대신 생성하지 않는다.

## 준비와 검토

- 적용 대상 MySQL을 확인하고 기존 DB와 장치 원본 증거를 비공개 백업한다. 원래 writer의 지속 중지와 새 장치 클라이언트의 전환 경계도 확인한다. 5번의 코드 게시와 실제 Pi 배포는 구분한다.
- 새 빈 테스트 DB에서 007→010→raw011을 적용하고 ORM·실제 DDL의 열/nullable/PK/UNIQUE/FK/CHECK/엔진/collation을 비교한다. 011의 signed INTEGER는 users.id, signed BIGINT는 기존 job revision과 맞춘다.
- source/new 작업의 `(enrollment_id, device_id, reserved_slot)` 복합 FK와 운영자 users FK는 같은 DB를 참조하며 CASCADE를 넣지 않는다. 원래 작업당 복구1개, 새 작업당 이전 복구1개로 UNIQUE를 둔다.
- 같은 초 안의 중지→관찰→확인 순서를 보존하기 위해 네 증거 시각과 재발급 시각은 **UTC DATETIME(6)**이다. 초 단위 DATETIME으로 바꾸면 이 구분이 사라지므로 준비 검사에서 거절한다.
- 실제 원본 보고서의 SHA256과 전달받은 JSON 자료의 SHA256을 다른 열에 저장한다. 요약 파일을 원본 보고서나 장치 MAC으로 표시하지 않는다. 개인정보·슬롯 전체 목록·원문 보고서·Bearer·키를 테이블에 넣지 않는다.

테이블 준비가 승인된 해당 환경에서 raw SQL 또는 아래 CLI 중 하나만 사용한다. CLI는 없는 011 테이블만 만들며 기존 부적합 테이블을 수선하지 않는다.

```text
# 먼저 검토한 환경의 등록·durability·recovery 옵션을 명시한다.
python -m flask --app remotework init-enrollment-recovery-draft
```

## 기능 활성화와 복구는 별도

`REMOTEWORK_ENABLE_ENROLLMENT_RECOVERY_DRAFT=1`은 등록/010 옵션이 모두 켜져 있어야 한다. 기본은 off다. 테이블이 없거나 준비 검사에 맞지 않으면 `503 ENROLLMENT_RECOVERY_SCHEMA_REQUIRED`이며 HTTP에서 DDL을 실행하지 않는다. 준비 검사는 계약의 주요 물리 조건을 대조하지만 모든 CHECK 표현식의 의미까지 증명하는 공유 DDL 승인 도구는 아니다.

테이블 생성이나 flag만으로 어떤 작업도 종료·재발급되지 않는다. 서버 운영자가 비공개 자료를 대조한 CLI preview→`--apply`로 한 작업을 명시 종료하고, 그 운영자의 현재 관리자 로그인에서 새 대상·요청 UUID를 정한 재발급 API를 호출해야 한다. 두 작업 모두 현재 권한/장치 설정/공통 guard와 DB transaction을 사용한다.

## 검증과 되돌림

`tests/test_enrollment_recovery.py`는 raw011을 소유 MySQL에 적용해 microsecond/FK/CHECK, 현재 권한, 보고서 해시/시각, 원래 이력 보존, 중복/동시 재발급의 단일 효과, DB 오류 rollback, 새 작업의010→complete/매핑, 실제 mysqldump→새 DB 복원 후 old/new 연결·claim revision 보존을 검사한다. 합성 키·장치/센서 결과를 사용하며 실제 센서 저장을 증명하지 않는다.

실제 적용 시에는 실행 SHA·대상 DB·테이블 준비·백업 복원 결과·운영자/장치 근거·API 결과를 따로 기록한다. 이상이 있으면 신규 등록과 recovery 기능을 끄고 writer 차단을 유지하며 읽기/증거를 보존한다. 코드 flag를 끄는 것은 DB 이력의 역변환이 아니다. 복구 이력/작업/claim을 삭제하거나 old ticket을 되살리지 않으며, 이미 재발급한 작업의 센서 쓰기를 막은 후 영향 범위를 확인한다. 백업 복원도 대상 환경과 이후 기록 유실을 검토한 별도 운영 절차다.
