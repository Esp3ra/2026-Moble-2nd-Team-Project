# 공지 DB 연결 (4번 로컬 구현)

4번 공지 전용 모델·API의 검토용 개발 초안입니다. API/DTO·테이블 구조·배포 migration 번호·정식 활성화는 1번 협의 대상이며 공유 DB에는 적용하지 않았습니다.

## 저장과 권한

- `notices` 테이블 하나에 `id`, `title`(200자), `content`(5000자), `target`, `is_pinned`, `author_id`(users.id FK), `author`(작성 당시 이름), `created_at`(UTC), `attachment_names_json`을 저장합니다.
- 대상은 전 직원·기획팀·개발팀·디자인팀입니다. 부서별 테이블을 따로 만들지 않습니다.
- 사용자 결정: 로그인한 직원도 모든 부서 공지를 조회합니다. 대상 필터는 정확히 해당 대상으로 작성된 공지만 조회하며 권한 제한을 뜻하지 않습니다.
- 등록·삭제는 서버에서 관리자만 허용합니다. 수행자·접속·역할을 공통 관리자 트랜잭션으로 재확인하고 공지 변경과 감사 기록을 함께 commit/rollback합니다.
- 작성자·날짜는 서버에서 결정합니다. 클라이언트가 보낸 작성자·ID 등 지원하지 않는 필드는 거절합니다.
- 첨부는 파일명 최대 20개만 보관합니다. 원본 절대경로·파일 본문을 저장하거나 업로드하지 않습니다. 실제 다운로드·자동 팝업·직원별 확인 기록은 미구현입니다.

## 서버 준비

프로젝트 루트에서 `REMOTEWORK_ENABLE_NOTICE_DRAFT=1`을 명시적으로 설정한 서버만 공지 API/모델을 등록합니다. 기존 업무 플래그와 독립적이며 기본 서버는 공지 모델을 import하지 않습니다. 기존 공통 로그인·DB 설정을 준비한 뒤 아래를 **명시적으로 한 번** 실행합니다. 이 명령은 없는 공지 테이블만 만들고 기존 공지를 삭제하지 않습니다. 기존 열을 자동 이관하는 migration은 아닙니다.

```powershell
$env:REMOTEWORK_ENABLE_NOTICE_DRAFT = "1"
Set-Location .\RemoteWork.Server
& .\.venv\Scripts\python.exe -m flask --app remotework notices init-table
```

새 DB에는 기존 공통 `init-db`가 먼저 필요합니다. Blueprint가 등록되어 있으므로 새 DB 초기화 때 공지 모델도 생성됩니다. 앱 시작이나 API 요청에서는 DDL을 실행하지 않습니다. 공통 schema_versions의 1을 임의 변경하지 않았으며 공유 DB migration/버전 정책은 1번과 협의해야 합니다.

수정한 서버와 클라이언트를 모두 다시 실행합니다. `Start-Demo.cmd`는 위 플래그를 지정한 PowerShell에서 실행할 때 별도 SQLite 데모 DB에 공지 테이블을 준비하며 종료 후에도 공지를 보존합니다. MySQL을 사용하려면 기존 `.env.example`/`docs/SETUP.md`에 따라 MySQL·SECRET_KEY를 준비하고 위 명령을 실행합니다. 초기 로컬 구현 환경에는 `.env`가 없어 실제 MySQL에는 적용·검증하지 않았습니다. 공유 적용은 1번 합의·백업·migration 절차를 따른 뒤 진행합니다. 데모 실행은 MySQL 저장 검증을 대신하지 않습니다.

테이블이 없으면 `503 NOTICE_SCHEMA_REQUIRED`와 준비 명령을 응답합니다. 공통 테이블·접속 정보가 없거나 연결에 실패하면 공통 DB 오류가 반환됩니다. 메모리 예시나 JSON 파일로 성공을 대체하지 않습니다.

## API 제안

공통 Bearer 인증, snake_case, `{ok,data,request_id}` 응답을 사용합니다.

| 메서드·경로 | 결과 |
|---|---|
| GET `/api/v1/notices?page=1&limit=100` | items/page/limit/total. 고정 우선, 날짜·ID 내림차순 |
| GET `/api/v1/notices?target=디자인팀` | 해당 대상만 필터. 기본 limit 100, 최대 100 |
| GET `/api/v1/notices/{id}` | `{notice: ...}`. 없는 ID는 404 |
| POST `/api/v1/notices` | 제목·본문·대상·고정·파일명 전달. commit 후 201 `{notice: ...}` |
| POST `/api/v1/notices/{id}/delete` | 관리자 삭제·감사 이력 저장. `{message: ...}` |

삭제 action 경로는 공통 ApiClient에 임의로 새 메서드를 추가하지 않기 위한 로컬 제안입니다. 부서 ID 테이블이나 공통 인증 규약을 새로 정의하지 않았습니다.

## 클라이언트 흐름

`NoticeEditForm → INoticeService → HttpNoticeService → AppServices.Api → 공지 API → DB`로 연결합니다. 기본 화면은 실제 HTTP 서비스만 사용합니다. 정적 샘플 공지 3개를 제거했고 목록의 List는 현재 화면의 서버 응답 캐시일 뿐입니다.

첫 화면·메뉴 재진입 시 목록을 읽습니다. 조회 버튼은 목록을 다시 읽고 선택한 ID의 상세를 읽습니다. 대상 필터·고정 우선·열 정렬과 조회 버튼 상세 표시를 유지합니다. 목록은 100개씩 모든 페이지를 읽어 필터 범위를 누락하지 않습니다. 페이지 사이에 다른 사용자가 등록/삭제했다면 재조회로 최신 목록을 확인합니다.

저장 대기 중 버튼·입력을 잠그고 중복 클릭을 막습니다. commit 응답 성공 때만 작성 창을 닫습니다. 오류 시 본문·대상·파일 선택을 유지하고 제목 아래 오류를 표시합니다. 통신 시간 초과는 저장 실패가 확정된 것이 아닐 수 있어 자동 재전송하지 않으며 수동 재등록 전 목록 확인을 안내합니다. 창 정리 후 늦은 응답은 UI에 반영하지 않습니다.

## 검증

`RemoteWork.Server/tests/test_notices.py`는 별도 테스트 DB에서 저장·앱 재생성 후 유지·모든 부서 조회·관리자 권한·입력 검증·감사 rollback·삭제·페이지·테이블 준비를 검사합니다. `Tests/Notices.Tests`는 C# HTTP 대역·실제 WinForms·선택적 실제 로컬 HTTP/SQLite 연계를 검사합니다. 실제 MySQL·공유 DB·두 PC 연결은 별도 검증 대상입니다.


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
