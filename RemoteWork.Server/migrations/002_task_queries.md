# 002 업무 조회 DDL 검토·검증 절차

작성일: 2026-10-17 · 담당: 2번 · 상태: **개발 검토 초안 / 공유 DB 미적용**

1번의 [업무 조회 규약 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/7b0c81588db667deeccc8ad67f3249090f0276de/1번/2026-10-17_업무조회_API_DB_규약확정_회신.md) M1-07~09에서 `002_task_queries.mysql.sql` 번호와 검토 절차를 수용했습니다. 번호 예약은 이 SQL의 최종 채택이나 공유 적용 승인이 아닙니다. 기준 ORM은 `alpha 329d4a3`의 `remotework/features/tasks/models.py`입니다.

## 현재 범위

[SQL 초안](002_task_queries.mysql.sql)은 기존 공통 스키마에 업무 조회용 `tasks`, `task_assignments`, `task_managers`를 생성합니다. 테이블 생성 3문장에 현재 모델의 인덱스를 포함하며 기존 공통 테이블·행은 변경하지 않습니다.

- 사용자 참조는 signed INTEGER, 업무 ID와 참조는 signed BIGINT입니다.
- 현재 PK·FK·CHECK·인덱스·NULL 정책을 그대로 유지합니다. MySQL FK 타입·인덱스 조건은 [공식 FK 규칙](https://dev.mysql.com/doc/refman/8.4/en/create-table-foreign-keys.html)을 확인합니다. 삭제 cascade와 현재 배정 포인터의 역방향 복합 FK는 추가하지 않습니다.
- `row_version=1`과 UTC 기록 시각은 Python ORM의 기본값입니다. SQL 서버 기본값은 없고, 업무 상태 초기값도 정하지 않습니다. UTC 시각은 애플리케이션이 직접 전달합니다.
- 현재 배정 포인터는 정합한 배정 행을 가리켜야 하지만 이를 보장하는 복합 FK는 없습니다. 쓰기 트랜잭션·잠금·버전 책임과 새 FK 도입은 2·3번 공동 확인 전입니다.
- 문자열 CHECK의 대소문자 비교는 DB collation을 따릅니다. 현재 모델의 CHECK를 정확한 소문자 강제 수단으로 설명하지 않습니다. 이 초안에서 collation이나 제약을 임의 강화하지 않습니다.
- MySQL 기본 스토리지 엔진은 InnoDB, 공통 DB 문자셋은 utf8mb4여야 합니다. 테스트는 실제 생성 결과와 FK 강제를 확인합니다.

공통 **실제 스키마 버전은 1**입니다. 이 SQL은 버전2 행을 넣지 않으며 `SCHEMA_VERSION`, `check_schema()`, `init-db`도 바꾸지 않습니다. 적용 후 목표 버전2의 기대 버전 검사·적용 기록 지원은 1번이 별도 코드 검토로 준비해야 합니다. 현재 health의 버전1 응답은 업무 DDL 적용 여부를 증명하지 않습니다.

## 격리 MySQL 검증 실행

테스트는 배포 도구가 아닙니다. [전용 테스트](../tests/test_task_migration_draft.py)는 아래 두 설정이 모두 있을 때만 MySQL에 연결합니다. 둘 다 없으면 MySQL 검증은 생략되고, 하나만 있거나 잘못된 값이면 실패합니다.

```powershell
# RemoteWork.Server 폴더, 개발 의존성(pytest 포함)이 설치된 Python 사용
# 삭제 가능한 전용 MySQL만 지정. 실제 비밀번호는 Git/로그/문서에 쓰지 않습니다.
$env:MYSQL_MIGRATION_TEST_URL='mysql+pymysql://전용테스트계정:비밀번호@127.0.0.1:포트/remote_work_test_migration?charset=utf8mb4'
$env:REMOTEWORK_RUN_TASK_MIGRATION_DRAFT_TESTS='1'
# PATH에 mysql/mysqldump가 없을 때만 실제 설치 폴더를 지정
$env:MYSQL_MIGRATION_TEST_BIN_DIR='C:\Program Files\MySQL\MySQL Server 8.2\bin'
python -m pytest -q tests/test_task_migration_draft.py
```

URL은 `mysql+pymysql`, loopback 주소, `remote_work_test_` 접두사의 DB, 선택적인 utf8mb4 설정만 허용합니다. 테스트는 이 주소의 전용 서버에 고유 이름의 DB를 직접 만들고, 자신이 생성에 성공한 DB만 끝에 삭제합니다. 따라서 이 전용 계정에는 테스트 DB 생성·삭제 권한이 필요합니다. 기존 URL의 DB를 초기화하거나 삭제하지 않습니다. 일반 `DATABASE_URL`·`MYSQL_TEST_URL`·실제 `.env`를 연결 정보로 사용하지 않습니다.

복구 검증에는 `mysql`과 `mysqldump`가 필요합니다. 임시 접속 파일로 비밀번호를 전달하고 명령 인자나 출력에 싣지 않습니다. 첫 옵션의 `--defaults-file`과 `--no-login-paths`로 사용자·시스템 기본 설정이나 로그인 파일을 연결에 사용하지 않습니다. dump는 테스트의 임시 폴더에 두고 복구 대상은 새로 생성한 전용 DB입니다. 접속 파일과 dump는 성공·실패 모두 finally에서 제거합니다. 실제 업무 dump·계정·토큰은 저장소에 포함하지 않습니다.

검증 대상은 다음과 같습니다.

1. 공통 7개 테이블만 생성하고 사용자·인증·grant·감사 이력·버전1 데이터를 넣습니다. 업무 모델 `create_all`로 SQL 실행을 대신하지 않습니다.
2. 실제 SQL 파일로 업무 3개 테이블을 생성하고 ORM과 열 타입·NULL·PK/FK·CHECK·필수 인덱스를 비교합니다. MySQL의 FK 지원용 자동 인덱스는 따로 구분합니다.
3. 공통 행의 전후 일치, 잘못된 FK/상태/날짜/차수/버전의 거절, cascade 없는 이력 보존을 확인합니다.
4. 실제 raw DDL 위에서 Flask `test_client`의 프로세스 내부 요청으로 공통 Bearer 인증과 본인·지정 관리자 GET/필터/직접 상세 권한을 확인합니다.
5. 재실행·기존 테이블 충돌을 거절하고, 두 번째 생성에서 실패한 뒤 앞 테이블이 남는 것을 확인합니다.
6. DDL 전 공통 데이터를 실제 mysqldump로 백업하고 새 테스트 DB에 mysql로 복원합니다. 원래 공통 행이 같은지 확인한 다음 SQL을 다시 적용합니다.

이번 DDL 테스트는 별도 TCP 서버·C# HTTP·WinForms·두 PC를 실행하지 않습니다. 테스트 성공은 공유 환경 적용·쓰기 경합·3번 통합 성공을 뜻하지 않습니다. 테스트 실행 결과와 실행하지 못한 범위는 해당 날짜의 `LOG/2번`에 별도로 기록합니다.

## 공유 적용 전 확인할 게이트

1. 1번이 DDL·FK·버전 검사·적용 기록 코드를 검토하고, 2·3번이 상태/날짜 의미·배정/제출 관계를 확인합니다.
2. 팀장이 공식 서버·운영 창구·적용 시간과 담당자를 확인합니다. 실행할 코드 SHA, 현재 스키마, DDL SHA256과 기존 데이터 영향을 기록합니다.
3. 공유 DB의 백업·복원 가능 여부를 전용 환경에서 실제로 확인합니다. 이 테스트의 fixture dump가 공유 DB 백업을 대신하지 않습니다.
4. 합의된 코드/DDL/버전 검사 순서와 기능 활성화 시점을 정합니다. 현재 기본 off를 유지하며 다른 기능 등록을 변경하지 않습니다.
5. 적용 후 버전·실행자·시각·결과와 기존 이력 보존·인증·목록·상세·권한 검증을 기록합니다. 실제 두 PC 검증은 별도 수행합니다.

## 실패·복구 시 주의

SQL에는 `IF NOT EXISTS`를 넣지 않습니다. 테이블이 이미 있으면 중단하고 현재 열·제약·행과 적용 기록부터 확인합니다. 무조건 재실행하거나 기존 테이블을 덮어쓰지 않습니다.

[MySQL DDL의 암묵적 commit](https://dev.mysql.com/doc/refman/8.4/en/implicit-commit.html) 때문에 일반 트랜잭션의 rollback만으로 앞서 생성한 테이블이 사라지지 않습니다. 이 초안에는 자동 DROP·자동 rollback SQL이 없습니다. 공유 DB에서 실패하면 기능을 비활성 상태로 두고 변경 이후의 행·배정·제출 참조부터 확인한 뒤 승인된 복구 절차를 적용해야 합니다. 새 업무 데이터가 있으면 이력·관계까지 포함한 복원 검토가 필요하며 테이블 삭제로 되돌리지 않습니다.

테스트에서 수행하는 복구는 **DDL 전 dump를 새 전용 DB에 복원하는 절차**입니다. 기존 공유 DB를 덮어쓰는 복구나 운영 중 장애 복구를 완료했다는 뜻이 아닙니다.
