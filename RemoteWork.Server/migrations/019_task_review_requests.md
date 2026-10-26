# 제출 결과에 대한 수동 검토 요청

2026-10-26 사용자 확정 범위다. 현재 본인에게 배정된 업무의 최신 제출완료 결과에 요청 내용을 저장하고 기존 결과물 기능이 지정한 현재 검토자에게 보여 준다. 업무 상태·버전·배정·제출 회차·결과 판정을 바꾸지 않는다. 같은 제출 회차의 확인 대기 요청은 하나만 허용한다. 검토자의 **요청 확인**은 결과 승인·반려와 다르며, 원 결과가 판단됐거나 업무가 취소된 요청은 조회할 때 종료로 표시한다.

[019 SQL](019_task_review_requests.mysql.sql)은 독립 테이블 한 개의 명시 CREATE다. 공통 `users`, 002 업무·배정, 004 W1 저장소, 006 제출 저장소가 선행한다. 취소 자료가 있으면 기존009의 조회 검사를 함께 따른다. 새 테이블은 독립 metadata에 있어 서버 시작, HTTP, `init-db`가 만들지 않는다. `schema_versions`는 기존1을 유지한다.

## 서버232 담당자의 명시 적용 절차

이 파일과 소유 MySQL 검사는 공유 서버232에 적용했다는 증거가 아니다. 실제 서비스의 DB명·연결·현재 코드·관계 어댑터와 선행 스키마를 먼저 확인한다. 기존 DB·설정·외부 파일 저장소를 비공개 백업하고 독립 MySQL에 복원하여 기존 자료·DDL을 대조한다. 최신 원격의 번호019 중복 여부도 배포 직전 다시 확인한다.

검토한 배포 구간에서 서비스 쓰기를 멈추고 정지 상태 백업을 다시 확보한 뒤, 운영자가 관리하는 MySQL 세션에서 현재 DB와 새 테이블 부재를 확인한다. 비밀번호를 명령행에 넣지 않는다. 아래는 **MySQL 클라이언트 안에서** 실행하는 명령이다. 경로와 DB명은 실제 배포 경로로 바꾼다.

```sql
SELECT DATABASE(), @@hostname, @@port, @@version;
SHOW TABLES LIKE 'task_review_requests';
SOURCE /reviewed/deployment/RemoteWork.Server/migrations/019_task_review_requests.mysql.sql;
SHOW CREATE TABLE task_review_requests;
SELECT version FROM schema_versions ORDER BY version;
```

기존 테이블이 있으면 SOURCE를 반복하거나 자동 ALTER/DROP하지 말고 검토된019와 물리 구조를 대조한다. DDL은 MySQL에서 자동 커밋되므로 부분 실패를 전체 트랜잭션 rollback으로 취급하지 않는다. 런타임은 실제 열/타입/null·PK·AUTO_INCREMENT·InnoDB/collation·보존형FK·유일 대기키·강제CHECK·생성식을 검사한다. 없거나 다르면 `503 TASK_REVIEW_REQUEST_SCHEMA_REQUIRED`이며 자동 복구/생성을 하지 않는다.

기존 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`, `REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT=1`, `REMOTEWORK_ENABLE_RESULTS_DRAFT=1` 배포에 연결한다. 별도 기능 플래그는 없다. 기존 results facts 어댑터와 지정 검토자 관계를 그대로 읽는다. 결과물 어댑터 설치·운영 관계가 준비되지 않았다면 이를 먼저 확인하며 테스트용 관계나 통합 ACK를 운영 설정에 주입하지 않는다.

같은 서버를 시작한 뒤 실제 계정으로 제출완료 업무 선택→현재 결과·검토자 확인→요청 내용 저장→받은 요청 조회→요청 확인을 점검한다. 업무 상태·버전·회차가 유지되고 기존 결과 승인·반려 뒤 요청이 종료 표시되는지 확인한다. 세션·선택·현재 배정/검토자 변경, 같은 회차 중복, 원 W1 결과 복구도 점검한다. 실제 WinForms와 웹, 두 PC 및 공유232 결과는 별도 실행 증거로 남긴다.

장애 시 새 요청 동작을 중지하고 요청·감사·W1 결과를 보존한다. 기존 결과물을 변경하거나 테이블을 DROP하거나 백업으로 현재 데이터를 덮어쓰는 것을 기본 복구로 사용하지 않는다.

## API 계약

- `GET /api/v1/tasks/{task_id}/review-request-context`: 현재 본인+최신 제출완료만. `data.context`에 업무·버전·배정·최신 submission/round/description·현재 reviewer와 기존 요청을 반환한다.
- `POST /api/v1/tasks/{task_id}/review-requests`: W1 `task.review_requested`, target=task_id. body는 `expected_row_version`(양의 BIGINT 문자열), `expected_assignment_revision`(정수), `submission_id`(양의 BIGINT 문자열), `reason`(공백만 제외한1~4000 유니코드 문자)다. 저장 원문은 유지한다.
- `GET /api/v1/tasks/review-requests?scope=own|reviewing`: 현재 배정/검토자 권한만 사용한다. `data.items`에는 ACK에 필요한 현재 `row_version`, `task_status`를 포함한다.
- `POST /api/v1/tasks/{task_id}/review-requests/{request_id}/acknowledge`: W1 `task.review_acknowledged`, target=task_id. body는 두 expected 버전이다. 경로 request_id도 W1 fingerprint에 묶는다. 현재 기존 검토자만 pending 요청을 확인할 수 있다.

두 성공 결과는 `request_id,task_id,submission_id,round_no,request_state,created_at,acknowledged_at`만 W1에 보관한다. 요청 원문과 이름은 receipt에 넣지 않는다. 저장 후 상태가 바뀌어도 현재 권한을 재검사한 뒤 원 키·원 body의 성공 결과를 돌려준다. `resolved`는 조회 파생값이므로 과거 성공 영수증은 바꾸지 않는다.
