# 004 공통 요청 결과 저장소 — 검토용 MySQL DDL

담당1번, W1-03/05. [SQL](004_write_receipts.mysql.sql)은 `write_receipts` 새 테이블1개만 만든다. users.id와 같은 signed INTEGER FK, SHA256 key_hash PK·request_hash, 제한된 행동 결과·상태·UTC 생성/만료 시각과 만료 인덱스다. 원본 요청·키 원문·토큰·본문·파일은 저장하지 않는다. FK cascade 삭제는 추가하지 않는다.

002 업무 번호·003 공지 예약과 분리해004를 공통 receipt용으로 사용한다. **파일 번호4는 현재 schema version4 적용을 뜻하지 않는다.** 이 테이블의 직접 의존은 기존 공통 users다. 업무 준비 API에는002 업무 모델도 필요하지만003 공지를 대신 적용하거나 공지 PR을 병합하지 않는다. 실제 공통version1·기본off 유지이며 공유 DB 적용·전역 버전 변경은 검토/팀장 일정 후다.

## 전용 환경 검증

1. 삭제 가능한 loopback MySQL8의 고유 `remote_work_test_` DB·계정을 준비한다. 실제.env/공유DB를 테스트 URL로 사용하지 않는다.
2. 기존 공통 테이블과 합성 계정, 필요한002 초안을 준비한다. 004는 검토한 SQL로 적용하고 ORM create_all과 열/타입/NULL·users FK·PK/인덱스를 대조한다. 테이블이 이미 있으면 멈추며 기존 것을 DROP/교체하는 절차가 아니다.
3. 같은 키 동시 요청·같은 본문 replay·다른 본문409·권한 회수·성공 결과 조회·만료 키 재실행 거절·receipt 실패 시 업무/감사 rollback을 검증한다. 공통/업무 행과 schema_versions1의 보존을 확인한다.
4. 코드 검토와 실제/기대 버전·DDL/백업/복구·실행자/시간을 정한 뒤 공유 적용을 별도 승인받는다. 이 문서는 공유 적용 명령/승인이 아니다.

DDL 자체는 새 CREATE1개다. HTTP 중에는 생성하지 않으며, flag만 켜서 기존 테이블을 변환하지 않는다. 새 전용 DB의 명시적 init 과정은 등록된 모델을 생성할 수 있으나 기존 DB migration을 대신하지 않는다. 현재 [공통 사용 계약](../../docs/WRITE_SUPPORT.md)의 서명 키/24시간 결과 보관·정리 정책과 함께 검토한다.

## 실패·복구 계획과 실제 범위

MySQL DDL과 업무 transaction rollback은 구분한다. 정식 적용 전 별도 백업과 새 DB 복원 검증을 하고 적용 실패 시 실제 테이블/열/인덱스/FK·코드 flag·schema_versions를 확인한다. 존재하는 사용자/업무/공지 테이블을 지우거나004를 반복 실행해 덮어쓰지 않는다. 애플리케이션 flag를 내리고 원인/호환성을 검토하며 운영 DB 자동 초기화는 하지 않는다.

receipt는 성공 결과와 같은 transaction에 저장한다. 서명된 만료 시각 때문에 receipt 물리 정리 후에도 같은 만료 키를 새 작업으로 처리하지 않는다. 백업 복원/키 회전으로 이미 성공한 작업의 결과 기록이 사라진 경우 자동 재실행으로 복구하지 않고 업무 데이터/감사를 대조한다. 공유 장애복구·키 보존·정리 주기는 팀장이 정식 배포 계획에 기록해야 한다.

자동 검증은 `tests/test_write_requests.py`의 실제 raw004↔ORM 대조·공통 행/버전 보존·원자성·기한/경합과 `tests/test_task_write_support.py`의 실제 C#→TCP HTTP→MySQL이다. 기존002의 dump→새 DB 복원 회귀도 별도로 실행한다. **004를 포함한 공유 환경 백업/복원·정식 migration 실행은 이번에 수행하지 않았다.** 구체 실행 수치는 해당 LOG 회신을 따른다.
