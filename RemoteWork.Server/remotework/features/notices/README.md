## 웹 공지사항 추가 — 2026-10-19

[웹 화면·경로·공통 접점·제한·검증 안내](WEB.md). 목록 선택 후 **조회 버튼**으로 오른쪽 상세를 표시하며 관리자 작성/삭제와 직원 전체 부서 조회를 기존 저장소에 연결한다. 1번 WEB-N4 회신을 반영했고 실제 파일 전송·웹 팝업/확인·마감 알림·새 DDL은 포함하지 않는다. 아래 DB 구현·검증 기록은 당시 이력으로 보존한다.

# 공지 DB 연결 (4번 로컬 구현)

## 현재 통합 상태 — 2026-10-17

[PR #7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/7)은 독립 재검토 후 alpha [733b47a](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/733b47afca123ed3b161e6d96b492a8afc6e163f)로 병합했습니다. 검토 대상 head는 `d58064c`이며 R1·R2 해결을 유지합니다. 독립 검토에서는 격리 MySQL 서버 **110통과·SQLite 전용2개 제외**, 실제 C#→TCP HTTP→raw003 MySQL **37검사 통과**를 확인했습니다. 아래 작성자 검증 수치와 합산하지 않습니다.

003 SQL은 개발 검토안으로 포함됐으며 정식 DDL 승인·공통 실제/기대 버전 지원·공유 DB 적용·삭제/보존 공동 정책은 별도입니다. 공통 schema version1·공지 기본off를 유지합니다. 자세한 [독립 검토·병합 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#r021)을 따릅니다.

아래는 각 작성 시점의 검증·협의 이력입니다. 과거의 재검토 대기·미병합 문구를 현재 상태로 읽지 않습니다.

## 현재 — 1번 N4 회신 반영·003 DDL 검토안, 2026-10-17

기준 원본 alpha `c56f3c87e446adb30309c46287cd7c4aa3cff273`과 기존 공지 head `c7282d1e9e332daf3996aeb00f9a1b20c6ad2e61`을 결합했다. 원본의 업무 입력 준비·정확한 상태 CHECK·002 SQL·공통 로그인/인증/DB 코드를 보존하고 README의 양쪽 내용을 합쳤다.

[병합된 PR #12의 1번 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_공지DB_API_DDL_협의회신.md)에서 N4-01~04·07·10의 공통 재사용·signed INTEGER/C# int `1..2147483647`·범위 밖/미존재404·현재 API/DTO·직원 전체 부서 조회·관리자 쓰기·독립 opt-in을 수용했다. 이 항목은 회신 대기가 아니다. R1/R2는 이전 검토자가 해결을 확인했으며 이번 결합본의 작성자 회귀와 새 DDL은 별도 재검토 대상이다.

예약된 `003_notices.mysql.sql`과 검증·복구안을 추가했다. 실제 SQL로 새 notices 테이블을 만들고 ORM 타입/NULL/default/index/FK, 기존 공통/업무 데이터·버전1 보존, 작성자 비활성 후 snapshot, 재실행/실패, 실제 dump→새DB 복원과 API를 검사한다. **SQL 최종 검토·공통 실제/기대 버전 지원·삭제/보존 정책·공유 적용은 대기**다. version3은002→2가 먼저 승인·적용될 때의 후속안이며 실제version1·기본off를 유지한다. [003 절차](../../../migrations/003_notices.md)를 따른다.

최신 실행 결과·명령·검증 범위는 아래 연결된 공지 안내와 LOG의 4번 후속 검증 기록을 따른다. 팝업/확인 기록·실제 파일 전송·중지된 정렬 조정은 이번 PR에 없다. 아래 과거 회차의 수치/회신 대기 상태는 당시 기록이며 현재 상태로 재사용하지 않는다.

## 이번 결합본 직접 실행 결과 — alpha c56f3c8 기준

Windows / SDK9.0.316·net8.0-windows / 격리 MySQL8.2.0 REPEATABLE-READ. 운영/공유 MySQL·사용자 .env·실제 계정은 사용하지 않았다.

| 실제 실행 | 결과·범위 |
|---|---|
| MySQL 전체 `python -m pytest -q` | **112통과**,107.38초. 공통 인증/동시성·공지 R1/R2·업무 조회/실제002·새 실제003·dump/새DB 복원 포함 |
| 실제 C#→TCP HTTP→raw003 MySQL | **37검증 통과**. notices를 create_all로 만들지 않고 SQL로 생성, 실제 작성 화면/직원 조회/권한/삭제 검사 |
| 기존 DEMO SQLite 전체 pytest | **83통과/29생략**,97.25초. 생략은 MySQL 전용4+업무DDL14+공지DDL11. 지속 관리 DB는 MySQL이며 SQLite는 기존 DEMO 검사만 수행 |
| 앱·C# 검증 프로젝트5개 Release | 모두 **경고0/오류0**. 최신 업무 C# 소스 보존, 최종 alpha 추가분은 Python/문서만 변경 |
| C# 대역/실제 WinForms | ApiClient18시나리오88검증, Collaboration5/85, Notices30, TaskQueries12/140, TaskQueries.Http6/103 |
| 실제 공지 TCP HTTP→임시 SQLite | **37검증 통과**, 기존 DEMO 실행기 경로 |
| 최신 업무 실제 TCP HTTP→임시 SQLite | 실행기 **300검사·권한회수11개 포함**, C#직원43/관리자64. 최종 상태 CHECK 모델로 재실행 |
| 독립 공지/업무 플래그 | 새 프로세스4조합의 모델/경로 등록·기본off 검증 통과 |

서버 전체 결과는 `python -m pytest -q`, 공지 rawDDL만 재현하려면 `python -m pytest -q tests/test_notice_migration_draft.py`와 별도 NOTICE_MIGRATION_TEST_URL/명시적 opt-in을 사용한다. MySQL 전체 실행에는 공통 MYSQL_TEST_URL과 업무DDL의 별도 opt-in도 전용 테스트 서버로 지정했다. SQL 준비·접속 범위는003 절차를 따른다. C#5개는 각 검증 프로젝트의 Release DLL, 기존 DEMO HTTP 실행기는 Tests/Notices.Tests/run_http_check.py 및 Tests/TaskQueries.Http.Tests/run_http_check.py로 실행했다.

MySQL은 새 소유 datadir·임시 loopback포트·합성계정·고유 remote_work_test_ DB만 사용했다. 최신 전체 실행과 raw HTTP가 exit0으로 끝났고 자기 임시 서버 종료/정리를 확인했다. 포함/반복되는 검증 수치를 합산하지 않는다. Flask test_client·실제 TCP·대역·공유 두PC를 구분한다. **공유/운영 DB·공식 migration/version 지원·공유 복구·두PC·웹·장치·수동 전체GUI는 미실행**이다. SQL 검토/공유 적용 승인·원본 병합은 이 결과와 별도다.

## 이전 — PR #7 R1·R2 수정 보고, 2026-10-17

기준 원본 alpha8a652ad와 공지 head7478101을 결합하고 공지 전용 routes.py·회귀만 수정했다. 기존 업무 격리 HTTP 실행기와 공통 인증/DB/관리자 가드는 보존한다.

| 구분 | 현재 상태 |
|---|---|
| 구현·작성자 검증, 재검토 대기 | R1: admin_transaction 뒤 공지를 FOR UPDATE/current read·populate_existing으로 다시 확인해 삭제200/후속404·삭제 감사1건 보장. R2: 현재 INTEGER/C#int에 맞춘 개발 방어 경계1..2147483647, DB 바인딩 전 기존 미존재404로 처리. 일반 DB 예외를404로 바꾸지 않음 |
| 추가 발견·보완 | MySQL DATETIME 저장 정밀도로 등록 응답/재조회 시각이 달라지는 경우를 재현해 flush 후 저장된 값을 refresh하여 응답. 시각 컬럼/공통 규약은 바꾸지 않음 |
| 미합의 | 공지 전용 ID 범위·범위 밖404 유지/400 등 최종 오류, 삭제 응답·API/DTO·보존·DDL 번호/버전·공유 적용/활성화. 1번 회신 전 개발 초안이며 BIGINT 전환이나 공통 규약 확정 아님 |
| 미실행/범위 밖 | 공유/운영DB·두PC·웹·장비·수동 전체GUI, 이번 전체MySQL/실제C#→MySQL. 팝업/확인 기록·파일 전송·중지된 정렬 추가 조정은 PR 밖 |

이번 실제 결과: 서버 SQLite전체74통과/16생략(기존 MySQL2+DDL12+새 R1 MySQL2), 격리MySQL8.2 REPEATABLE-READ 표적32통과(공지 기존+R1/R2/시각/삭제rollback+공통관리자회귀), 실제 공지 C#→HTTP→임시SQLite37검증 통과. 최신 업무HTTP 프로젝트/참조앱 Release 경고0·오류0, 업무 격리 HTTP 실행기300검증·권한회수11개(실행기 내 포함; 중복 합산하지 않음), C#직원36/관리자52검증 통과. 서로 포함/반복되는 수치를 합산하지 않는다.

명령: 서버 `python -m pytest -q`, MySQL 전용 DB에서 `python -m pytest -q tests/test_notice_review.py tests/test_notices.py tests/test_admin_concurrency.py`, 프로젝트 루트 `python Tests/Notices.Tests/run_http_check.py --client-dll <Notices.Tests.dll>`, `python Tests/TaskQueries.Http.Tests/run_http_check.py --client-dll <TaskQueries.Http.Tests.dll>`. MySQL은 새 소유 datadir/프로세스/임시포트·합성계정만 사용하고 종료/정리했다. 이전 전체MySQL/빌드 보고는 아래 당시 결과로 보존하며 이번 실행으로 재사용하지 않는다.

새 회귀를 수정 전 코드에 적용했을 때 SQLite의 큰ID500과 MySQL 동시삭제200/200·중복감사 경고를 탐지했다. 수정 후에는 해당 회귀가 통과했으며 검토자 독립 재확인·1번 계약 회신·원본 병합/정식 적용은 별도다. REMOTEWORK_ENABLE_NOTICE_DRAFT/ENABLE_NOTICE_DRAFT의 기본off와 공통version1·업무002 예약을 유지한다.


4번 공지 전용 모델·API입니다. 현재 API/DTO·ID·권한·등록 방식은 1번 N4 회신 수용 범위이며, 예약003의 SQL 최종 검토·공통 버전 지원·공유 적용·정식 활성화는 별도 대기입니다. 공유 DB에는 적용하지 않았습니다.

## 저장과 권한

- `notices` 테이블 하나에 `id`, `title`(200자), `content`(5000자), `target`, `is_pinned`, `author_id`(users.id FK), `author`(작성 당시 이름), `created_at`(UTC), `attachment_names_json`을 저장합니다.
- 대상은 전 직원·기획팀·개발팀·디자인팀입니다. 부서별 테이블을 따로 만들지 않습니다.
- 사용자 결정: 로그인한 직원도 모든 부서 공지를 조회합니다. 대상 필터는 정확히 해당 대상으로 작성된 공지만 조회하며 권한 제한을 뜻하지 않습니다.
- 등록·삭제는 서버에서 관리자만 허용합니다. 수행자·접속·역할을 공통 관리자 트랜잭션으로 재확인하고 공지 변경과 감사 기록을 함께 commit/rollback합니다.
- 작성자·날짜는 서버에서 결정합니다. 클라이언트가 보낸 작성자·ID 등 지원하지 않는 필드는 거절합니다.
- 첨부는 파일명 최대 20개만 보관합니다. 원본 절대경로·파일 본문을 저장하거나 업로드하지 않습니다. 실제 다운로드·자동 팝업·직원별 확인 기록은 미구현입니다.

## 서버 준비

프로젝트 루트에서 `REMOTEWORK_ENABLE_NOTICE_DRAFT=1`을 명시적으로 설정한 서버만 공지 API/모델을 등록합니다. 기존 업무 플래그와 독립적이며 기본 서버는 공지 모델을 import하지 않습니다. **삭제 가능한 로컬 개발 DB에서만** 기존 공통 로그인·DB 설정을 준비한 뒤 아래를 명시적으로 실행합니다. 공유 DB에서는 이 명령을 정식 migration으로 사용하지 않습니다. 이 명령은 없는 공지 테이블만 만들고 기존 공지를 삭제하지 않습니다. 기존 열을 자동 이관하는 migration은 아닙니다.

```powershell
$env:REMOTEWORK_ENABLE_NOTICE_DRAFT = "1"
Set-Location .\RemoteWork.Server
& .\.venv\Scripts\python.exe -m flask --app remotework notices init-table
```

새 DB에는 기존 공통 `init-db`가 먼저 필요합니다. Blueprint가 등록되어 있으므로 새 DB 초기화 때 공지 모델도 생성됩니다. 앱 시작이나 API 요청에서는 DDL을 실행하지 않습니다. 공통 schema_versions의 1을 임의 변경하지 않았으며 003 번호 예약과 현재version1 유지 기준은 1번 회신을 따릅니다. SQL 승인·실제/기대 버전 지원·공유 적용은 [003 검토/복구 절차](../../../migrations/003_notices.md)의 별도 게이트입니다.

수정한 서버와 클라이언트를 모두 다시 실행합니다. `Start-Demo.cmd`는 위 플래그를 지정한 PowerShell에서 실행할 때 별도 SQLite 데모 DB에 공지 테이블을 준비하며 종료 후에도 공지를 보존합니다. 실제 구현·지속 관리·주요 검증은 MySQL 기준입니다. 삭제 가능한 로컬 MySQL을 준비하려면 기존 `.env.example`/`docs/SETUP.md`를 따릅니다. SQLite는 기존 DEMO 임시 구성으로 종료 후 삭제 예정이며 별도 운영 DB로 확장하지 않습니다. 초기 회차에는 MySQL 미검증이었으나 이번 격리 검증에서는 실제003 SQL·MySQL·C# HTTP를 별도로 검사했습니다. 사용자 .env·공유 DB는 사용하지 않았습니다. 공유 적용은 1번 합의·백업·migration 절차를 따른 뒤 진행합니다. 데모 실행은 MySQL 저장 검증을 대신하지 않습니다.

테이블이 없으면 `503 NOTICE_SCHEMA_REQUIRED`와 준비 명령을 응답합니다. 공통 테이블·접속 정보가 없거나 연결에 실패하면 공통 DB 오류가 반환됩니다. 메모리 예시나 JSON 파일로 성공을 대체하지 않습니다.

## 현재 API — 1번 N4-03 수용

공통 Bearer 인증, snake_case, `{ok,data,request_id}` 응답을 사용합니다.

| 메서드·경로 | 결과 |
|---|---|
| GET `/api/v1/notices?page=1&limit=100` | items/page/limit/total. 고정 우선, 날짜·ID 내림차순 |
| GET `/api/v1/notices?target=디자인팀` | 해당 대상만 필터. 기본 limit 100, 최대 100 |
| GET `/api/v1/notices/{id}` | `{notice: ...}`. 없는 ID는 404 |
| POST `/api/v1/notices` | 제목·본문·대상·고정·파일명 전달. commit 후 201 `{notice: ...}` |
| POST `/api/v1/notices/{id}/delete` | 관리자 삭제·감사 이력 저장. `{message: ...}` |

삭제 action 경로와 응답은 1번 N4-03의 수용 범위입니다. signed INTEGER/C# int 범위는1..2147483647이며 범위 밖/미존재는404 NOTICE_NOT_FOUND, 순차 삭제는첫200/다음404입니다. 부서 ID 테이블이나 공통 인증 규약을 새로 정의하지 않았습니다.

## 클라이언트 흐름

`NoticeEditForm → INoticeService → HttpNoticeService → AppServices.Api → 공지 API → DB`로 연결합니다. 기본 화면은 실제 HTTP 서비스만 사용합니다. 정적 샘플 공지 3개를 제거했고 목록의 List는 현재 화면의 서버 응답 캐시일 뿐입니다.

첫 화면·메뉴 재진입 시 목록을 읽습니다. 조회 버튼은 목록을 다시 읽고 선택한 ID의 상세를 읽습니다. 대상 필터·고정 우선·열 정렬과 조회 버튼 상세 표시를 유지합니다. 목록은 100개씩 모든 페이지를 읽어 필터 범위를 누락하지 않습니다. 페이지 사이에 다른 사용자가 등록/삭제했다면 재조회로 최신 목록을 확인합니다.

저장 대기 중 버튼·입력을 잠그고 중복 클릭을 막습니다. commit 응답 성공 때만 작성 창을 닫습니다. 오류 시 본문·대상·파일 선택을 유지하고 제목 아래 오류를 표시합니다. 통신 시간 초과는 저장 실패가 확정된 것이 아닐 수 있어 자동 재전송하지 않으며 수동 재등록 전 목록 확인을 안내합니다. 창 정리 후 늦은 응답은 UI에 반영하지 않습니다.

## 검증

`RemoteWork.Server/tests/test_notices.py`는 별도 테스트 DB에서 저장·앱 재생성 후 유지·모든 부서 조회·관리자 권한·입력 검증·감사 rollback·삭제·페이지·테이블 준비를 검사합니다. `Tests/Notices.Tests`는 C# HTTP 대역·실제 WinForms·선택적 실제 로컬 HTTP/SQLite 연계를 검사합니다. 실제 격리 MySQL·raw003은 이번 검증에 포함하며 공유 DB·두 PC는 미검증입니다.


## 최신 alpha 등록 반영과 PR 범위

최신 get_blueprints(config)를 보존하고 공지 플래그를 독립적으로 추가했습니다. 공통 factory·auth·ApiClient·MainForm·업무 코드는 그대로입니다. 공지 플래그만 켠 개발 서버 실행 예시는 프로젝트 루트에서 다음과 같습니다.

```powershell
$env:REMOTEWORK_ENABLE_NOTICE_DRAFT = "1"
& .\Start-Demo.cmd
```

클라이언트는 별도 창에서 Start-Desktop.cmd로 실행합니다. 플래그가 꺼진 서버는 공지 API가 미등록이므로 목록 조회 오류가 표시됩니다. 예시 목록으로 성공 처리하지 않습니다. 팝업·직원별 확인 기록·정렬 추가 보완은 이 DB PR에 포함하지 않습니다.

초기 alpha9b8d00c 로컬 구현에서는 서버 SQLite43통과/2생략, 공지/채팅85검증, 공통 ApiClient88검증, 공지37검증(로컬 HTTP/SQLite 포함)을 실행했습니다. 이 결과는 최신 alpha 등록 조정 후 PR 후보의 테스트 결과가 아닙니다. PR 최초 게시 때 최신 결합본 테스트는 미실행이었으며, 이후 사용자 추가 요청으로 실행한 결과는 아래 절을 따릅니다.


## 최신 alpha 결합 검증 — 2026-10-17

사용자의 추가 테스트 요청 후 alpha add1921347cda7697f43c5291ce01c110c46971e + 공지 코드 bb41248aa8f904c0fc94f2229aae364c9a491467에서 직접 실행했습니다. 이후 코드 변경은 검증 결과 문서뿐입니다.

| 실제 실행 | 결과·범위 |
|---|---|
| Release 앱·C# 검증 프로젝트5개 빌드 | 모두 경고0/오류0, SDK9.0.316/net8.0-windows |
| 서버 SQLite 전체 pytest | 66통과/14생략. 생략은 MySQL 전용2+DDL 실DB12 |
| 공통 ApiClient / 공지·채팅 / 공지 기본 | 각각18시나리오88검증 / 5시나리오85검증 / 30검증. HTTP·서비스 대역과 실제 WinForms |
| 기존 업무 UI / 업무 HTTP | 각각9시나리오82검증 / 6시나리오103검증. 명시적 대역, 실제 업무 서버 검증과 구분 |
| 공지 실제 로컬 HTTP→임시 SQLite | 37검증 통과. 실제 작성 화면·직원 조회/권한·DB 삭제 포함 |
| 격리 MySQL8.2 REPEATABLE-READ 전체 pytest | 79통과/1환경실패. mysqldump 경로 누락이 원인이었고 MYSQL_MIGRATION_TEST_BIN_DIR 지정 후 해당1개 재검증 통과(1.05초). 제품 코드 수정 없음 |
| 공지 실제 로컬 HTTP→별도 임시 MySQL | 37검증 통과. SQLite 실행과 별도이며 공통 대역 검증이 재포함돼 합산하지 않음 |
| 새 프로세스의 공지/업무 플래그4조합 | 기본off, 공지만on, 업무만on, 둘다on의 모델·경로 독립 등록 통과 |

MySQL은 전용 임시 datadir·자체 프로세스·임시 포트·합성 계정/remote_work_test_ DB만 사용했습니다. 실제 데모/운영/공유 DB·자격 설정에는 접속하지 않았고 종료 시 자기 임시 서버만 정리했습니다. 두 PC·공유 서버·웹·파일 전송은 미검증이며 팝업·직원 확인 기록·정렬 추가 조정은 이번 PR에 없습니다. 공지 계약·migration 번호·공유 적용·정식 활성화의 1번 협의는 계속 필요합니다.
## 이전: 로그인 화면 결합 재검증

alpha 3b09480의 로그인 화면·README·실행 안내를 보존해 공지 DB 변경과 추가 결합했습니다. 앱/검증 프로젝트5개 Release 경고0·오류0, C# 검증5종 및 실제 공지 HTTP/SQLite37검증을 다시 통과했습니다. 서버 소스는 앞서 검증한 결합본과 동일합니다. 위 서버 전체/격리 MySQL 결과는 앞선 실행 결과이며 이번 로그인 화면 변경에서 다시 실행한 수치로 표시하지 않습니다.


## 공지 팝업 독립 opt-in 검토안 (2026-10-20)

[POPUP](POPUP.md)·[008 검토/복구 범위](../../../migrations/008_notice_popups.md)를 따른다. 1번은 REMOTEWORK_ENABLE_NOTICE_POPUP_DRAFT 이름·기본off·독립 등록·명시 초기화 방향을 기술 수용했다. 공통/기본공지 초기화에는 확인 테이블을 넣지 않으며 기본 DTO도 유지한다. 실제 FK/삭제보존·로그인 경계·공유 적용은 미합의로 Draft 검토 대상이다. 후속으로 직원 MainForm 자동연결/만료·로그아웃정리를추가하고실제GUI+HTTP/MySQL자동표시·확인저장을검증했다. MainForm담당검토와확인기록보존구조는추가협의사항이며실제앱DB활성화는보류한다.


## 최신 삭제 요구 정정 — 2026-10-20

사용자는 공지 삭제 후 확인 기록 보존 요구를 철회하고 **공지와 확인 기록 함께 삭제**를 선택했다. 기존 ON DELETE CASCADE와 일치하며 새 보존 모델/이관은 구현하지 않는다. 삭제 감사는 유지한다. 앞선 보존 구조 미구현/요구 불일치는 당시 요구의 이력이고 현재 차단 사유가 아니다. 팀 공통 DDL·공유 적용·로그인 경계 등 나머지 협의는 별도이며 실제 앱 팝업 활성화는 이번에 하지 않는다. 상세는 POPUP.md와008의 최신 결정 항목을 따른다.


## 2026-10-22 실제 첨부 소비

3번 PR76/a6dc89c의 `results_resource_attachments` 공개 접점을 사용한다. `collaboration_attachments.py`는 관리자 임시 업로드/작성, 현재 공지 대상의 파일 열람 및 현재 채팅 참여/숨김을 재검사하는 4번 콜백이다. 정상 요청에서는 DDL을 실행하지 않는다. 기존 파일명 배열은 구형 공지의 표시용으로 유지하고 실제 전송은 `attachment_ids`(최대10, 중복 금지)로 구분한다. 공지ID는 signed INTEGER, 채팅 방/메시지/파일은 UUID이며 업무 BIGINT로 변환하지 않는다.

공지 등록은 기존 `admin_transaction()`에서 공지 flush → 제공 `replace_resource_files` → 파일명 표시 메타정보/감사 → commit이다. 실패는 자원과 참조를 함께 rollback한다. `notice.created`의 `attachment_count`는 같은 TX의 실제 파일 연결 근거이며 파일명만 있는 구형 공지와 구분한다. 첨부 공지의 삭제/파일 보존은 아직 합의되지 않아409로 차단한다. 확인기록 CASCADE를 파일 삭제로 확대하지 않는다. 파일 기능 OFF여도 이 차단은 유지한다. 타담당 저장 모델 직접 수정/참조 해제나 파일 삭제를 하지 않는다.

클라이언트는 선택 경로를 서버에 보내지 않고 기존 16KiB JSON 조각으로 전송하며, 빈 파일/25MiB 제한/다운로드 전체 크기와 SHA-256을 검사한다. 공지 상세의 **첨부 다운로드** 버튼(본문 우클릭도 가능)에서 현재 조회한 공지의 파일을 저장한다. 메타정보만 있던 과거 공지에 실제 바이트를 생성하지 않는다.

웹은 같은 cookie/CSRF 및 제공 `X-Results-Session` 범위를 사용한다. 원 로그인 범위는 공지 저장 시에도 재검사한다. `FormData`의 csrf_token/body 필드로 Base64 percent-encoding의 32KiB 초과를 피하고, 제공 조각 단위/공통 제한을 늘리지 않는다. LAN HTTP용 SHA-256은 `resource_attachments.js`에서 직접 계산하며 서버 검증도 유지한다. Node crypto와 경계/1MiB 벡터 대조 및 실제 브라우저 전송을 검사했다.

**남은 경계:** 기존 공지 생성 API에는 합의된 durable request UUID/W1 원결과 조회가 없다. 저장 응답 유실/5xx에서는 원래 내용·파일ID를 유지하고 재등록 버튼을 막는다. 업로드 상태 GET을 공지 저장 성공으로 해석하거나 자동 재등록/폐기하지 않는다. 1번과 원결과 복구 계약을 추가 협의해야 한다. 이 제한을 해결했다고 표시하지 않는다. 3번 참조 DDL 3테이블은 로컬 소유 테스트 DB에서만 적용했으며 공용 적용/번호/백업·복원·기동SHA는1번 검토 후 별도 배포다. PR76 자체는 4번 기능의 선행 의존성이다.

현재 기준: 원본 alpha `36f1e55d44cd3fa2fd2c7f634fbcd0d059ce7a9d`·LOG `3a9c66c`에서 PR75/76 병합을 확인했다. 위 제공 의존성은 병합 완료이며 공유 첨부 DDL 번호·적용/배포, 공지 삭제/보존·원결과 복구는 계속 별도 후속이다. 이전 미제공/미병합 표현은 당시 기록이다.
