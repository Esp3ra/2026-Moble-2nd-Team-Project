# 003 공지 기본 테이블 DDL·검증·복구안

작성: 4번 · 2026-10-17 · 상태: **1번 번호 예약에 따른 개발 검토안 / 공유 DB 미적용**

1번 [N4-01~10 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_공지DB_API_DDL_협의회신.md), 문서 PR #12에서 공지 INTEGER·API·404·작성자 snapshot·기본off를 수용하고 `003_notices.mysql.sql`을 기본 notices 테이블용으로 예약했다. **번호 예약은 이 SQL의 승인·공유 적용이나 version3 확정이 아니다.** 실제 SQL·복구안은 4번 작성→1번 검토, 공유 일정·보존 정책은 팀장 공동 확인 대상이다.

## 범위와 현재 모델

[SQL](003_notices.mysql.sql)은 notices 하나만 생성한다. 공통 users·인증·감사·업무002·기존 행을 변경하지 않고 팝업/확인·파일 테이블도 만들지 않는다. 기본 모델은 공지 PR #7의 `remotework/features/notices/models.py`다.

- id는 signed INTEGER AUTO_INCREMENT, author_id는 signed INTEGER·users.id FK다. C# int/서버 유효 ID는 1..2147483647이며 범위 밖/미존재404 NOTICE_NOT_FOUND다. 업무 task_id BIGINT와 구분한다.
- title VARCHAR(200), content TEXT, target VARCHAR(80), is_pinned BOOL, author VARCHAR(80), created_at DATETIME, attachment_names_json TEXT다. 전부 NOT NULL, PK(id), `(target,created_at)` 인덱스, author_id FK의 MySQL 보조 인덱스가 있다.
- InnoDB·utf8mb4를 명시한다. FK는 사용자 물리 삭제를 RESTRICT/NO ACTION으로 막으며 cascade를 추가하지 않는다. 사용자 비활성화는 FK 삭제가 아니고 작성 당시 author snapshot을 유지한다.
- is_pinned=false·created_at UTC·첨부명[]은 **Python ORM의 기본값**이다. SQL 열에 DEFAULT를 추가하지 않는다. ORM 외의 직접 INSERT는 각 값을 명시해야 한다. 애플리케이션이 created_at을 전달하며 DB CURRENT_TIMESTAMP/서버 현지 시각을 추가하지 않는다.
- title/content 길이·대상4종·파일명/경로 검증은 현재 API 규약이다. 기존 모델에 없는 CHECK·새 collation·JSON 타입을 임의 추가하지 않는다. SQL NULL/타입/FK와 API 입력 검증을 구분한다.
- SQL은 IF NOT EXISTS·ALTER·DROP·INSERT/version writes를 포함하지 않는다. 기존 notices가 있으면 실패하고 현재 열/PK/FK/인덱스·데이터·적용 기록을 먼저 비교한다. init-table로 만든 테이블을 지우고 재실행하는 방법은 제공하지 않는다.

## 스키마 버전·적용 게이트

현재 공통 SCHEMA_VERSION=1·업무002 예약·공지/업무 독립 opt-in 기본off를 유지한다. 이 SQL은 schema_versions에 행을 넣지 않고 check_schema/init-db를 변경하지 않는다. 1번은 실제/기대 버전·부분 적용·호환 검사 지원을 별도 구현해야 한다. **목표version3은002→2가 먼저 승인·적용되는 경우의 후속안**이며 번호003만 보고 실제 버전을 올리지 않는다. 현재 health/version1 성공은 notices 준비 완료 증거가 아니다.

공유 적용 전 순서:

1. 1번이 공지 SQL 타입/FK/default/index·현재 환경과 업무002/버전 지원·실행 순서를 검토한다.
2. 팀장·1·4번이 공지 물리 삭제/감사 보존의 운영 정책과 공식 서버·적용자·시간·사용자 접근을 확인한다. 향후 확인 기록/첨부 보존은 별도 공동 합의다.
3. 기존 공통·업무 데이터를 넣은 전용 MySQL에서 실제003 SQL, API/권한·전후 데이터·실패/재실행·백업/복구를 검증하고 SQL SHA256·코드 SHA·서버/버전을 남긴다.
4. 공유 DB의 백업과 새 환경 복원 가능성, 중단/복구 조건을 확인한다. 테스트 fixture의 dump는 공유 DB 백업이 아니다.
5. 승인된 코드/002/003/기대 버전 지원 순서로 적용하고 실제 버전·실행자·시각·결과를 기록한다. 합의 전에 정식 활성화하지 않는다.
6. 관리자1명·다른 부서 직원2명으로 실제 두 PC의 저장/재시작/모든 부서 조회·삭제/403·접속 만료·장애·감사 보존을 확인한다.

## 전용 MySQL의 raw DDL 검증

`tests/test_notice_migration_draft.py`는 배포 도구가 아니다. 아래 **별도 URL과 명시적 opt-in**이 둘 다 있어야 접속한다. 둘 다 없으면 실DB 검사를 생략하고, 하나만 있거나 잘못되면 실패한다. 일반 DATABASE_URL/MYSQL_TEST_URL·.env를 이 DDL 검사의 연결 설정으로 사용하지 않는다.

```powershell
# RemoteWork.Server 폴더, pytest와 서버 의존성이 설치된 Python
$env:PYTHON_DOTENV_DISABLED = '1'
$env:NOTICE_MIGRATION_TEST_URL = 'mysql+pymysql://테스트계정:비밀번호@127.0.0.1:포트/remote_work_test_notice_ddl?charset=utf8mb4'
$env:REMOTEWORK_RUN_NOTICE_MIGRATION_DRAFT_TESTS = '1'
# PATH에 mysql/mysqldump가 없는 경우에만 설치 폴더 지정
$env:MYSQL_MIGRATION_TEST_BIN_DIR = 'C:\Program Files\MySQL\MySQL Server 8.2\bin'
python -m pytest -q tests/test_notice_migration_draft.py
```

MySQL loopback·remote_work_test_ 접두사·utf8mb4만 허용한다. 테스트는 연결 서버에 고유 이름의 **새 DB를 직접 만든 뒤 자신이 생성한 DB만 삭제**하므로 해당 전용 계정에 CREATE/DROP DATABASE 권한이 필요하다. 지정한 기존 DB를 초기화하거나 삭제하지 않는다. 실제 자격증명은 Git에 올리지 않는다.

검증 항목:

- 공통7개 테이블과 사용자/접속/로그인 실패/grant/감사/버전1 fixture만 먼저 생성, notices는 create_all로 만들지 않고 실제003 SQL 적용.
- ORM과 실제 SQL의 열·signed 타입·NULL·SQL 기본값 없음·PK/FK/인덱스·InnoDB, 기존 공통 행과 버전 보존.
- 잘못된 FK/NULL 거절, 다른 관계가 없는 작성자 비활성 후 snapshot/공지 보존·사용자 삭제 FK 거절.
- raw DDL 위의 Flask test_client로 실제 공통 인증·관리자 등록/삭제·직원 타부서 조회/삭제403·순차200/404·감사1건.
- 기존 notices 재적용 거절·행 보존, users 누락 시 실패, 실제002 SQL·업무/배정/관리자 fixture 전후 보존.
- 실제 mysqldump로 DDL 전 공통 fixture 백업→별도 새 DB에 mysql 복원→같은 원본 데이터 확인→003 재적용. 접속 파일/dump는 임시 경로·제한 권한으로 만들고 finally에서 제거하며 출력/커밋하지 않는다.

이 모듈 자체는 TCP 서버·C#·두 PC를 실행하지 않는다. 공지 actual C#→HTTP→raw MySQL 검증을 별도로 실행한 경우에는 별도 결과로 기록한다. SQL/격리 테스트 통과는 운영 정책·DDL 최종 승인·공통 버전 지원 완료나 공유 적용이 아니다.

## 실패·복구와 기존 데이터

003은 단일 CREATE이며 테스트 환경에서 users 누락·기존 테이블 충돌 시 기존 데이터를 보존하는지 확인한다. 이를 임의의 전체 배포가 자동 rollback된다는 보장으로 확대하지 않는다. MySQL [DDL의 암묵적 commit](https://dev.mysql.com/doc/refman/8.0/en/implicit-commit.html)과 [atomic DDL](https://dev.mysql.com/doc/refman/8.0/en/atomic-ddl.html)은 일반 트랜잭션의 ROLLBACK으로 이미 완료된 DDL을 취소하는 것과 다르다.

오류 발생 시 기능을 비활성 상태로 유지하고 실제 테이블/행·버전·선행002·감사·후속 참조를 조사한다. 자동 DROP/무조건 재실행/기존 DB 덮어쓰기를 하지 않는다. 새 공지 데이터가 있으면 보존·내보내기·참조를 포함한 승인된 복원 계획이 필요하다. 전용 테스트의 복구는 **이전 dump를 새 DB에 복원**한 검증이며 실제 공유 장애 복구를 수행한 것이 아니다.
