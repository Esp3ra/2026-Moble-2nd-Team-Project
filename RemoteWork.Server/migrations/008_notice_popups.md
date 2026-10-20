# 008 공지 팝업 확인 저장 — 격리 개발 검토안

작성4번 / 공통 FK·DDL 검토1번 / 삭제·보존 정책 및 공유 적용·복구 담당 확정은 팀장.

[1번 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6283590252)은 설정 명명·독립 등록·명시 준비 방향을 기술 수용하고 **008 작성·검토 번호만 예약**했다. SQL 최종 승인·전역version8·공유 적용 승인이 아니다. 006 결과물·007 장치 등록 번호와 분리한다.

## 사용자 후속 요구: 확인 기록 보존

2026-10-20 사용자가 공지 삭제 후에도 확인 기록 보존을 선택했다. 따라서 현재 CASCADE raw008/ORM은 **최종 요구를 충족하지 않는 비교·문제 재현용 검토안**이며 실제 앱/공유DB에 적용하지 않는다. 부모 공지 참조와 불변 확인 증거를 분리하는 변경안을1번과 맞춘 뒤 모델/SQL/테스트를 갱신해야 한다. 아직 새 필드/FK·보존 구조를 확정·구현한 것이 아니다.

## 적용 범위와 보류 조건

- `008_notice_popups.mysql.sql`은 기존 로컬 확인 모델을 재현하는 **raw MySQL 검토 자료**다. 공통 `schema_versions`의1과 notices/users를 바꾸지 않는다. counter/event/cursor·로그인 경계 저장을 포함하지 않는다.
- 확인 행의 `(notice_id,user_id)`는 signed INTEGER 복합PK, `confirmed_at`은 서버 UTC의 최초 확인 시각이다. SQL 시간 default·자동 timestamp 갱신 없이 앱에서 UTC를 저장한다. 사용자 FK는 RESTRICT, 공지 FK는 CASCADE라는 **미승인 기존 개발안**을 노출한다.
- **팝업 off는 DB FK를 끄지 않는다.** 확인 행이 있는 공지를 기본 삭제 API로 삭제하면 이 검토안에서는 확인 행도 CASCADE 삭제된다. 삭제 감사는 같은 TX에1건이며 두 번째 삭제는404다. 이는 보존 영향의 재현 결과이며 무영향·정책 승인 근거가 아니다. RESTRICT로 임의 교체하거나 논리 삭제를 도입하지 않았다.
- 공지/확인/감사 각각의 보존, 기본 삭제200/후속404와의 정합성을 팀장이 결정하고1번이 최종 FK/물리DDL을 검토하기 전에는 **공유/운영 DB에 적용하지 않는다**. 이미 확인 테이블을 적용한 환경에서는 옵션을 껐다는 이유로 기본 삭제가 무해하다고 가정하지 않는다.

## 독립 등록과 명시 준비

기본 공지 옵션과 `REMOTEWORK_ENABLE_NOTICE_POPUP_DRAFT=1`을 함께 켜야 팝업 Blueprint/전용 CLI를 등록한다. 앱의 명시적 `ENABLE_NOTICE_POPUP_DRAFT=False`가 환경변수보다 우선한다. 별도 PopupBase/metadata가 부모 테이블의 실제 Column을 FK로 참조하되 공통 Base에는 확인 테이블을 등록하지 않는다. 초기화 대상은 자식 Table 하나뿐이다.

공통 준비 → 기본 공지 준비 → 두 옵션 명시 → 전용 준비라는 순서다. 삭제 가능한 전용 개발 DB에서만 검토한다.

```powershell
# RemoteWork.Server 폴더, 별도 테스트 DB를 가리키는 비공개 설정을 먼저 확인
$env:REMOTEWORK_ENABLE_NOTICE_DRAFT = "1"
$env:REMOTEWORK_ENABLE_NOTICE_POPUP_DRAFT = "1"
.\.venv\Scripts\python.exe -m flask --app remotework init-db
.\.venv\Scripts\python.exe -m flask --app remotework notices init-table
.\.venv\Scripts\python.exe -m flask --app remotework notice-popups init-table
```

`init-db`와 `notices init-table`은 확인 테이블을 생성하지 않는다. 부모/users/notices/schema_versions 및 공통version1 기록이 없으면 자식 생성 전 중단한다. 기존 `prepare_popup` 모듈도 같은 전용 함수를 호출한다. 서버 시작/HTTP pending·confirm 중 DDL은 없다. raw008과 전용 CLI는 같은 구조의 **서로 다른 최초 준비 경로**이며 raw CREATE를 중복 실행하지 않는다.

전용 CLI는 새 테이블 생성 전/기존 테이블에서 열 집합·타입/INTEGER signed·nullable·default/자동 증가·복합PK 순서·두 FK의 대상/열/ondelete/onupdate·필수 user_id 선두 비고유 인덱스·예상 밖 UNIQUE·InnoDB/utf8mb4_unicode_ci를 비교한다. NO ACTION/RESTRICT는 동일한 즉시 거절 의미로 정규화한다. 불일치 시 차이 항목만 안내하고 자동 ALTER/DROP/행 수정하지 않는다. 번호/생성 성공이 전체 공유 호환 또는 정책 승인 증거는 아니다.

## 부분 실패·재시도·동시 준비

- MySQL DDL은 전체 트랜잭션 rollback을 보장하지 않는다. 부모 누락은 DDL 전 차단하며 준비 대상은 CREATE TABLE 하나다. DDL 이후 검사/접속이 실패하면 상태를 확인하고 같은 전용 명령으로 물리를 재검사한다. 자동 삭제/ALTER로 숨기지 않는다.
- 같은 전용 CLI끼리는 현재 DB 이름에 묶인 MySQL GET_LOCK을 같은 연결에서 얻고 finally에 해제한다. 다른 기능 요청/공통 guard의 잠금 순서를 바꾸지 않는다. 외부 raw SQL/수동 DDL은 이 잠금에 참여하지 않으므로 적용자를 한 명으로 정해야 한다.
- 동일 초기화를 반복해도 기존 confirmed_at/확인 행·감사·공통version을 변경하지 않는다. 기능 off/on은 기존 테이블을 삭제하지 않는다. raw CREATE의 중복 실패를 무시하지 않는다.

## 실제 검사와 재현

기본 자동 검사:

```powershell
$env:PYTHON_DOTENV_DISABLED = "1"
.\.venv\Scripts\python.exe -m pytest -q tests/test_notice_popup_optin.py tests/test_web_notices.py tests/test_notices.py tests/test_notice_review.py tests/test_chat_storage.py
```

실제 MySQL 회귀는 **소유자가 새로 만든 비어 있는 `remote_work_test_popup_*` DB**와 별도 접속값/명시 opt-in이 필요하다. fixture는 그 DB의 테스트 테이블을 정리하므로 실제 자료가 있는 DB를 지정하지 않는다. URL은 비공개 환경변수로만 전달한다.

```powershell
$env:REMOTEWORK_RUN_NOTICE_POPUP_MIGRATION_TESTS = "1"
# NOTICE_POPUP_MIGRATION_TEST_URL에는 위 전용 DB의 비공개 SQLAlchemy URL을 설정
.\.venv\Scripts\python.exe -m pytest -q tests/test_notice_popup_migration.py
```

raw008/ORM 같은 검사 통과·최초 시각/버전 보존, unsigned/nullable/default/FK onupdate/collation/UNIQUE의 불일치 후 무수정 중단, 팝업 off에서 기본 삭제의 CASCADE/감사 영향, 명시 초기화 동시 실행을 검사한다. 다른 MySQL 전용 검사를 생략했다면 이 파일만의 통과를 전체 MySQL 검증으로 표시하지 않는다.

작성자는 이 코드에서 별도 소유 DB를 만들어 raw008 회귀와 실제 mysqldump→다른 새 DB 복원 후 users/notices/확인/공통version 행·시각 및 엄격 물리 검사를 대조했다. 사용한 합성 계정/DB와 소유 연결을 정리했으며 실행 중 사용자 앱 DB에는 새 팝업 DDL을 적용하지 않았다. 명령/수치·미실행은 PR 본문에 구분한다. 이 격리 복원은 운영 장애복구/공유 적용 완료가 아니다.

## 정식 적용·복구 전에 필요한 결정

팀장이 적용 환경/실행자·백업 보관·복구 실행자를 정하고, 승인할 정확한 SQL/코드 SHA와 옵션을 고정해야 한다. 승인된 백업을 **새 DB에 실제 복원**해 기존 데이터·버전·제약을 비교한 뒤 공유 적용 여부를 결정한다. 이미 확인 기록이 생긴 환경은 테이블/행 보존 백업과 FK 영향을 함께 다루며 단순 DROP rollback·옵션 off만으로 안전하다고 판단하지 않는다.

로그인 기존 공지 제외/15초 주기·닫기/확인·재확인·다중PC·보존 기간, 정확한 로그인 시작 경계/cursor 및 부서 변경·삭제/확인 경합의 최종 계약은 여전히 C09 공동 협의사항이다. 이 문서는 합의된 분리 방향의 구현/물리 검토 자료이며 그 정책을 확정하지 않는다.
