# 웹 공지사항 — 4번

## 원 요청 복구 연결 — 2026-10-23

공통 W1 활성화 시 [017 원 UUID·최소 영수증·명시 결과 확인](../../../../docs/NOTICE_RECOVERY.md)을 사용한다. 응답 유실 뒤 입력·첨부·키를 고정하고 원 결과를 먼저 조회하며, 미저장 확인 후 같은 요청을 다음 사용자 클릭으로 재시도한다. 이전 문서의 durable 복구 미제공은 구형 호환 경로의 이력이다. 실제017 적용·공유 서버/두 PC 검증은 별도다.

## 범위와 사용

기존 공지 API와 같은 notices 테이블로 웹 목록·상세·작성·삭제를 제공한다. 행 클릭은 선택만, **조회 버튼**을 누르면 오른쪽 상세를 표시한다. 최초/작성 직후에는 본문을 자동 표시하지 않는다. 대상 필터·20개 페이지·열 정렬을 지원하며 고정 공지는 최신 순으로 우선한다. 직원도 모든 부서 공지를 조회하고 작성·삭제는 관리자만 가능하다.

공지 기능은 기존대로 기본 off다. 새 개발 환경은 [003 준비 절차](../../../migrations/003_notices.md)와 공통 [실행 안내](../../../../docs/SETUP.md)를 먼저 따른다. 이미 준비된 환경에서는 서버를 시작하는 PowerShell에 `REMOTEWORK_ENABLE_NOTICE_DRAFT=1`을 설정하고 기존 실행 명령을 사용한다. 명시적 `ENABLE_NOTICE_DRAFT=False`가 환경변수보다 우선한다. 공유 DB 준비·활성화 승인을 뜻하지 않는다.

| 경로 | 동작 |
|---|---|
| GET /features/notices | 활성 시 /notices 이동, 비활성 시503 안내 |
| GET /notices | 목록·필터·정렬·페이지 및 선택/조회 |
| GET/POST /notices/new | 관리자 작성 폼 / 저장 후303 |
| GET/POST /notices/{id}/delete | 관리자 삭제 확인 / 삭제 후303 |

웹 `choice`는 선택 상태, `selected`는 명시 조회다. 단순 조회는 page·target·sort·direction과 선택 ID를 함께 보존한다. 대상 변경은 서버가 폼의 원래 list_target과 비교해1페이지로 초기화하고, 열 정렬 링크도1페이지부터 시작한다. 필터/현재 페이지에 없는 오래된 선택은 다른 공지 본문으로 대체하지 않고 선택 안내를 표시한다. 삭제로 마지막 페이지가 비면 마지막 유효 페이지로 보정한다. 유효 범위 밖 ID는404다. 목록이 비면 상세도 표시하지 않는다. 이 파라미터는 기존 JSON API 계약이 아니다. API 기본 page1/limit100·응답 필드·권한은 유지한다.

## 공통 접점·주요 함수

- `web.py`의 index/create/delete가 기존 웹 쿠키/channel web·CSRF·역할 검사를 사용한다. API는 Bearer/channel api를 유지한다.
- `routes.py`의 query_notices/create_notice_record/delete_notice_record를 웹/API가 공유한다. 관리자 guard 뒤 현재 사용자/접속 및 공지 locking read를 검사하고 변경·성공 감사를 같은 트랜잭션에 기록한다. commit 성공 뒤에만303/성공 응답을 반환한다.
- 공통 `remotework/web.py`의 공지 메뉴 분기, registry의 기존 notice gate 안 웹 blueprint 등록, base.html의 메뉴명·빈 extra_head 블록만 연결한다. 공통 auth/db·전역 CSRF/CSP는 바꾸지 않는다.
- `notices.js`는 행 선택·키보드 선택과 쓰기 중복 클릭 억제만 담당한다. 영속 멱등 보장이 아니며 쓰기 자동 재시도는 없다.
- 템플릿은 Jinja escape, 정적 CSS/JS는 같은 origin을 사용한다. 파일명은 메타데이터만 받으며 실제 request.files는400으로 거절한다.

## 제한·실패 처리

첨부파일 업로드/다운로드, 웹 자동 팝업·확인 저장, 실제 업무 마감 알림은 연결하지 않았다. 기존 공지 모델·003·schema1을 사용하며 새 migration·팝업 모델/경로를 추가하지 않는다. 업무 마감 영역은 미연결 안내만 표시한다.

긴 한글 폼은 multipart로 전송하지만 **요청 전체32KiB**를 유지한다. 본문·파일명·CSRF·boundary의 합계가 초과하면413으로 거절하며 저장하지 않는다. 개별 필드 상한 조합이 모두 한 요청에 들어감을 보장하지 않는다. validation 실패는 입력을 유지하고, DB 결과 불명은 재등록 전에 목록을 확인하도록 안내한다. 클릭 억제/303을 영속 중복 방지로 표현하지 않는다.

## 검증

서버 폴더에서 전용 테스트 환경으로:

```powershell
python -m pytest -q tests/test_web_notices.py tests/test_notices.py tests/test_notice_review.py tests/test_web.py
```

SQLite에서는 실제 MySQL 동시성2개가 skip된다. MySQL 회귀에는 `MYSQL_TEST_URL`이 **삭제 가능한 remote_work_test_ 접두사 전용 DB**를 가리켜야 한다. 실제 사용자/공유 DB는 지정하지 않는다. 테스트는 기존 행을 비울 수 있다. 정확한 실행 환경·통과/실패·미실행은 이번 코드 PR 본문을 따른다. 테스트 수치를 과거 README와 합산하지 않는다.

웹 테스트에는 쿠키/API 채널 분리·직원 쓰기403·CSRF·현재 역할/활성/만료·파일400·요청413·XSS·최대 한글·쓰기 rollback·웹/API 데이터 공유·조회/선택·cold flag 조합을 포함한다. Flask test client/MySQL을 실제 브라우저/MySQL 또는 물리 두PC와 구분한다.

## 협의 근거

[1번 WEB-N4 기술 회신 고정본](https://github.com/jinkyu1452-cmd/Sang1/blob/3a676f19317daf57a3053f715c4fd9aa7ead96c0/1번/2026-10-19_WEB-N4_웹공지_연결규약_기술회신.md)의01~06 기술 의견을 참고한다. 닫힌PR35를 승인 근거로 사용하지 않으며, 웹 구현과 PR38 보완은 사용자의 직접 요청 범위다. 최종 코드 승인/원본 병합은 별도다. C09 팝업·확인·삭제 보존·공유 적용 정책은 이번 웹 기능으로 확정하지 않는다.

## R38-01 페이지 선택 수정

[검토자 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-19_PR38_웹공지_페이지선택_수정요청.md)에 따라 실제 렌더된 조회 폼을 제출하는2·3페이지 첫/중간/마지막 선택 회귀와 필터/정렬·삭제/빈목록 검사를 추가했다. 실제 브라우저 키보드 선택→조회도 별도 확인한다. 수정·작성자 검증과 검토자의 해결 판정은 구분하며 재검토를 요청한다.
