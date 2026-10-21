<a id="pr55-review-20261021"></a>
## 최신 PR 검토 — PR54 문서 병합·PR55 수정 필요·PR52 보류, 2026-10-21

[PR54](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54)는 기존6문서198행 추가를 검토해 LOG45d5f52에 병합했습니다. [PR55](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55) headffc421b는 공식180검사/Release0/0이 통과했으나 **R55-01 P1(기존 intent의 flagOFF 우회),R55-02 P2(RR snapshot의 늦은 채취 승인),R55-03 P2(terminal 결과와 충돌하는 첫 BLOB)**를 소유MySQL에서 별도 재현해 미병합입니다. [작성자 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55#issuecomment-6305522973)과[고정 SHA·검사·정리 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr55-review-20261021)을 따릅니다. PR52 head0a7a57d는 수정이 없어 기존P1/P2 보류를 유지합니다.

팀장이 채택한 **A1~A6·B1~B7 진행 기준**은 [통합 정책](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/f6ea8e7adad0a3c15dcf03ca97154a8fff781119/2번/공통규약.md#member4-policy-adopted-20261021)에 푸시하고[전원 통합 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305519634)으로 전달했습니다. 정책 채택과 코드 구현·실제 서버/장치 적용·통합 검증은 별도입니다.

### PR55 검토 기준과 차단 결함

head `ffc421b0bcce78c8923b853e13aff4809da865b8`, 실제 alpha base `bb281e317cdd27e2a4796ad39d8712bbddd9828b`, 검토 결합 tree `8e59733d101c1b56bf9fef15bc273afdd6278976`입니다. 무충돌/Ready/CLEAN과 코드 검토 결과를 구분하고 이번에는 병합하지 않습니다. 지문010/새 durability mode의 서버/API/DDL13파일이며 Pi의 SQLite 제거·이관/실물 복구 완료가 아닙니다.

| ID | 확인한 위치 | 실제 재현/필요 수정 |
|---|---|---|
| R55-01 P1 | `enrollment/service.py:185–187,225–227` |ON에서 intent 최초true/동일재전송false·legacy 승인409·미기록complete409 뒤 같은소유DB/작업/원접속/키의 새OFF앱은 MAC/binding 정상 sensor_write200·미기록complete200/completed·Mapping1. physical 원문은NULL. flag와 별개로 영속 intent가 있는 작업의 legacy 재허가·미기록완료를 차단하고 기존완료 재응답과 구분 필요 |
| R55-02 P2 | `enrollment/service.py:194` |로그인 RR snapshot을 만든 capture 요청을 공통guard 직전 대기→다른요청 intent200 commit→재개한capture200. plain db.get 대신 현재/잠금 조회로 intent/결과 존재 후 새채취 거절 필요. 단일 may_store 자체의 current row 검사는 유지됨 |
| R55-03 P2 | `enrollment/durability.py:147–158` |legacy completed/physical row 없음→다른 정상서명 failed 원문 첫기록200·원래complete재생200/completed로 모순. 현재Job.result_hash를 첫 원문/운영자이관에서도 대조해 충돌 원문 거절·동일원문 수용 필요 |

실제 센서 저장/삭제를 실행한 재현이 아닙니다. 서명 결과는 원본 signer/verifier·합성키/명시 대역을 사용하고 같은MySQL의 실제 HTTP/DB 경로를 호출했습니다. R55-01은 새승인MAC/공통binding까지 검증했습니다. R55-03은 상충한 첫 원문이 고정돼 원래 성공 원문의 후속 이관도409가 되는 복구 모순입니다. [수정 요청의 원본 코드 링크/기대 결과](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55#issuecomment-6305522973)를 전달했고 별도 회신용 파일/PR은 요구하지 않습니다.

### 직접 실행 결과

공식6모듈은 `test_enrollment_durability.py`, `test_device_enrollment.py`, `test_enrollment_approval_draft.py`, `test_enrollment_protocol_draft.py`, `test_task_creation_migration.py`, `test_task_write_support.py`이며 **180통과/생략0·197.12초**입니다. Python3.12.14/pytest9.1.1/MySQL8.2 소유DB와 `-W error::pytest.PytestUnhandledThreadExceptionWarning`을 사용했습니다. WriteSupport HTTP DLL을 사전 빌드해 실제 C#검사를 생략하지 않았습니다. WriteSupport·솔루션 Release 각 경고/오류0, defaultOFF module/metadata/route/init/archive CLI 미등록/DB미접속, diff검사도 통과했습니다.

별도 R55-01의1개 검사는 관찰 assertion으로 버그 상태를 재현한 것이며 정상 기능 통과가 아닙니다. R55-02/03의 안전성 검사2개는 기대409와 달리200이므로 **실패 재현/6.56초**입니다. 공식180과 합산하지 않습니다. 공식검사에 포함된 소유dump/restore와 작성자의 실제 서버백업·복원/적용 보고도 구분합니다. 모든 소유MySQL/DB·프로세스·임시runtime 정리를 확인했고 실제 프로젝트/.env·사용자DB·서버·계정·Pi/센서·슬롯·매핑에는 쓰지 않았습니다.

증거: `ES/tmp/remote_work_pr55_checks_20261021/audit/official.txt`, `official-summary.json`, `flag-downgrade-reproduction.json`, `test_flag_downgrade.py`, `supplement-summary.json`; `remote_work_pr55_durability47_20261021/audit/test_terminal_receipt_conflict.py`, `terminal-conflict.txt`, `terminal-conflict-summary.json`입니다.

### PR54·정책 채택과 현재 후속

PR54 head `7e68fcfc7c13f880cc7aaf95df02989e6b1b2ec9`, LOG base `db587cfa9fdfd8df15032002a7b091fccd77ebca`, 실제 merge `45d5f5230ecfebc484097e35dfcd2f53c17dff75`, 검토/실제 tree `bba17bf1f0560e111128a8525e7a4cfe1603d30c`가 일치합니다. 문서6개198추가/삭제0·원문/헤딩/앵커·상대링크89개·LOG2번8문서 보존/비밀값 패턴0을 확인했습니다. 문서 검토로 서버/HTTP/DB/GUI 검사를 추가 실행하지 않았습니다.

이후 본채팅의 사용자 지시로A1~A6·B1~B7을 진행 기준으로 채택해 기존LOG8문서에 기록하고 `f6ea8e7adad0a3c15dcf03ca97154a8fff781119`로 푸시했습니다.90일은 화면조회 기준이고 실제 삭제/감사·백업 보존·지문운영·근태/연차·별도검토요청 정책은 미확정입니다. 제출당시F4미확정은 이력이며 현채택과 구분합니다.

[1번 공지404 원인/지속설정·HTTP/새프로세스 재시작 보고](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305220140)와[5번 추가접점없음 답변](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305188245)을 수신했습니다.4번PC/GUI 재검증·팝업008/지문010 적용·실제 취소/pending 소비·두PC/실물/공유 검증은 별도입니다. 주서버 변경 보고를 이번 검토자의 직접검사로 확대하지 않습니다. PR52는 기존P1/P2 수정 대기이며55기능을 실제폴더에 반영하거나010/통합ACK를 적용하지 않았습니다.

<a id="pr51-53-review-20261020"></a>
## 최신 — PR51·53 병합, PR52 수정 필요, 2026-10-20

[PR51](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/51)의 취소·연장 성공 결과 조회/009 공통 검토를 `bb281e3`에, [PR53](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/53)의 협업 개발 환경 확인 요청 문서를 LOG `6664519`에 병합했습니다. [PR52](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52)는 기존 R52-01 P1(표시한 과거 결과물과 실제 판단 대상 불일치)을 독립 재현했고, R52-02 P2(버전 충돌 후 이전 요청만 반복하고 재준비 불가)를 추가 재현해 **OPEN·미병합**입니다. 공식 검사 통과와 이 JavaScript 결함을 구분합니다.

[고정 SHA·직접 검사·수정 기준](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr51-53-review-20261020)을 따릅니다. 공유 DB 적용·통합 ACK·실제 환경 준비는 별도이며, 회신용 새 MD/PR을 요구하지 않습니다.

### PR51 — 병합과 검사

head `a0aafacd5bc2895dc13c1d8e22e830872408cf93`, 실제 alpha base `2324606217f6f92848f2a7ad5e76dcf98bb164a1`, merge `bb281e317cdd27e2a4796ad39d8712bbddd9828b`입니다. 검토/실제 merge tree `170f666596a7cd2b967f423b8f0c755700eb71fb`가 일치합니다. Ready·head 고정·무충돌과 병합 직전 base를 확인했습니다.

새 소유 MySQL8.2·Python3.12.14에서 공식6모듈 **109개 통과, 실패/생략/경고0(232.98초)**입니다. 스레드 경고를 오류로 처리했고 실제 C#→HTTP→MySQL16검증도 이 실행에 포함돼 있어 별도 합산하지 않습니다. HTTP 검사 DLL Release 경고/오류0입니다. reader 앱별 고정 등록·현재 접속/사용자/배정·관리 관계·키/TTL을 대조했습니다. 쓰기 flag와 통합 ACK를 제거해도 기존 성공 결과만 현재 권한으로 조회하며 cold 읽기 모드는 키 발급/실행을 거절합니다. 조회/등록에서 DDL을 실행하지 않고 기존009가 필요합니다. raw009는 원문 미번호 초안에 신규 두 테이블의 엔진/문자셋/collation 옵션만 추가했고 원문은 보존됐습니다. 물리 제약·타 DB FK 거절·dump/새 DB 복원도 위109개에 포함됩니다.

병합15파일만 실제 작업 폴더에 base/current/incoming 대조·백업·재확인 후 반영했습니다. 병행2번 코드와 DOCX7개를 보존했고 실제 `RemoteWork.sln` Release 빌드는 경고/오류0입니다. 보류된52번 제품 파일은 반영하지 않았습니다.

### PR52 — R52-01 P1 유지, R52-02 P2 추가

head `0a7a57df97767e81680484222587974747bf6dab`를 위 alpha base와 PR51에 결합한 tree `68776b379ba0537f533073e9d1f51cd3f7c58bce`에서 검사했습니다. **서버185·실제 C# HTTP/WinForms41·cookie/CSRF 웹177**, cold 비활성·JS 구문·Release 경고/오류0을 직접 확인했습니다. C# 제품115소스를 컴파일했고 설치되지 않은 ref8.0.29/desktop10.0.9만 audit wrapper에서 설치된8.0.31/10.0.12 경로로 대체했습니다. 원본 제품/검사 assertion은 변경하지 않았습니다. 이전43f69c9 검토의 같은185/41/177과 합산하지 않습니다. 웹177은 실제 브라우저 JavaScript 상호작용 검사가 아닙니다.

기존 [R52-01 P1 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/3번/2026-10-20_PR52_검토회차_불일치_수정요청.md)은 현 head에서도 남아 있습니다. 원본 JS의 실제 등록된 회차/판단 handler를 Node VM에서 실행해 승인·반려 모두 context202→지연 중 과거201 선택→확인창201/1차→실제 POST202를 독립 재현했습니다. 브라우저/공유 DB 판단 실행으로 표시하지 않습니다.

**R52-02 P2:** `static/results.js:245–246`에서 제출409 `TASK_VERSION_CONFLICT` 뒤 결과 조회가 `not_recorded`여도 옛 expected 버전/본문의 재전송만 제공합니다. 원본 JS를 Node VM에서 실행해 `409 → not_recorded → retry409`, 두 전송 버전1·같은 키, 설명/조회 범위 비활성 및 choose/refresh 무요청을 재현했습니다. 확정 실패 상태와 결과 조회를 확인한 뒤 현재 context 재조회·편집/재준비로 복귀할 경로가 필요합니다. 불명확한 통신 결과에서는 원본 키/본문을 유지하고 자동 쓰기 재시도를 추가하지 않습니다. 지연 성공/미기록 충돌·입력 오류·정상 재준비 회귀를 함께 검증해야 합니다. 기존 코드 PR52에서 수정 후 재검토하며 3번 제품 코드를 대신 수정하지 않았습니다.

### PR53 — 요청 문서 병합

head `6dc2be88f7a1da880f13346e2404a8cca2dab16b`, 실제 LOG base `8179d7aeb6be5656047cd1c34d65aaebae56b547`, merge `6664519e4cd73b11056733d4008201f315eea39a`, 검토/실제 tree `09a0e8976479d9a46ac872cb9d790722a1632b51`입니다. 기존2번 MD1개51행 추가이며 원문/헤딩·추가 상대 링크/앵커·LOG2번8개 구조를 보존했습니다. 문서 변경이므로 프로그램/DB 검사를 추가 실행하지 않았습니다. 이미 구현된 업무 등록의 사용 절차와 실효 환경을 확인하려는 요청이며 장애 원인·환경 준비·협업 정책 승인을 확정하지 않습니다.

소유 MySQL·프로세스·임시 runtime 정리를 확인했습니다. 실제 공유 DB/.env·계정·서버·장치·통합 ACK는 변경하지 않았습니다. 결과물의 실제 취소/pending helper 소비 및 경합 검증이 남아 있어 lifecycle 쓰기503을 해제하지 않습니다. PR53 환경 답변을 외부에 보내거나 환경 준비 완료로 기록하지 않았습니다.

근거: `ES/tmp/remote_work_pr51_independent47_20261020/audit/official-six.txt`, `owned-summary.json`, `scope-and-sql.json`; `remote_work_pr51_52_checks_20261020/audit/results-owned.txt`, `web-reprepare-reproduction.json`, `web-decision-selection-reproduction.json`, `web-decision-return-selection-reproduction.json`; `remote_work_pr53_docs_review_20261020/audit/static-review.json`; `remote_work_pr51_52_review_20261020/audit/sync51-result.json`과 실제 merge tree 직접 대조입니다.

<a id="pr52-review-20261020"></a>
## PR52 전체 검토 — R52-01 P1 수정 필요·미병합, 2026-10-20

사용자의 병합 가능 확인에 따라3번 회신/부분 정적 대조를 넘어 전체15파일을 검토했습니다. [PR52](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52) head `0a7a57df97767e81680484222587974747bf6dab`, alpha `43f69c99632b1a084e0bc1c441f46d7f1a8eb26d`를 고정했습니다. GitHub상 CLEAN/무충돌·CI검사 없음과 실제 검토 결과를 구분합니다. **R52-01 P1 재현으로 OPEN·미병합**입니다.1번 PR51은 이번 검토에서 제외했습니다.

### 병합 차단 — 화면 결과물과 실제 판단 대상 불일치

`RemoteWork.Server/remotework/static/results.js`의 `openDecision`은188~201행에서 최신 제출 ID의 context를 기다립니다. 그동안 과거 회차 선택은 활성이고 selected만 변경합니다. 응답 뒤198~200행은 바뀐 selected의 회차/본문/파일을 표시하지만285행의 실제 POST는 최초 context.submission_id를 사용합니다. 따라서 검토자는 과거 반려 결과물을 보고 최신 제출물을 승인·반려할 수 있습니다. 서버 권한/버전 검사만으로 화면의 동일성을 보장하지 못합니다.

원본 JS를 Node VM에서 실행하고 명시 DOM/HTTP 대역으로 실제 등록된 과거 회차 handler를 지연 응답 사이 호출했습니다. `request=/results/data/submissions/202/context`, `writeTarget=202`, `shownSubmission=201`, `dialog=1차`, `roundButtonsEnabled=true`로 **불일치를 재현**했습니다. 실제 브라우저/공유 DB 판단 쓰기 재현으로 표시하지 않습니다. [3번 수정 요청·정확한 원본 행·재검증 조건](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/3번/2026-10-20_PR52_검토회차_불일치_수정요청.md)을 게시하며,업무/제출 캡처와 확인창/POST의 동일성·선택 변경 무효화·승인/반려 지연 회귀가 필요합니다.3번 제품 코드는 대신 수정하지 않았습니다.

### 검토자 직접 실행 — 통과 범위와 실패를 구분

| 검사 | 결과 |
|---|---|
| 공식 결과물 서버 회귀 |**185검사 통과**, 새 소유 MySQL/합성 사용자·업무·파일 |
| 공식 웹 회귀 |**177검사 통과**, 실제 cookie/CSRF/MySQL·결과물/첨부/이력,Flask 요청 검사 |
| 공식 C# HTTP |**41검사 통과**, 실제 loopback HTTP·WinForms controls·파일 bytes·receipt 복구,114소스 컴파일0/0 |
| 기본 off/cold / 독립 인증·입력 |cold 통과 / 명시 identity 대역16검사 통과 |
| Release / JS 구문 |경고0·오류0 / 통과 |
| 별도 JS 지연 재현 |**R52-01 재현 성공·수정 필요** |

소유 MySQL fixture를 사용하는 공식 verify_web→서버/C# 검사를 실행했습니다. 제출 검증 스크립트의 설치되지 않은.NET ref8.0.29/desktop10.0.9 경로만 외부 wrapper에서 설치된8.0.31/10.0.12로 대체했습니다. 원본 제품/검사 assertion은 무수정입니다. 실행25.78초,소유 테스트 MySQL 정리 성공을 확인했고 사용자DB/.env는 사용하지 않았습니다. 웹177은 실제 브라우저 JS를 구동하지 않으므로 화면 경합을 포괄하지 않습니다. 기존3번 작성자 보고 수치를 이번 직접 실행과 중복 합산하지 않습니다.

cookie와API Bearer 채널·CSRF/페이지 session·현재 권한/본문형식 검사와 기존 TX/receipt 재사용에서 추가 구체적인 차단 결함은 찾지 못했습니다. starting.py의 선택body는 expected두필드와 기존 수락/재작업 권한·잠금·KST·취소guard를 유지합니다. 이 사실이 JS 차단 결함을 상쇄하지 않습니다.

실제3번 취소/pending helper 소비·제출/첨부와 취소·연장 경합,공용 첨부·새8행동 표시·공유DB/실계정·두PC·실제 브라우저 전체 흐름은 미실행/후속입니다. 취소/연장503 해제·통합 ack 설치·공유 이관을 실행하지 않았습니다. 회신용 별도 MD/PR은 요구하지 않으며 기존 코드 PR52 수정 후 재검토합니다. 병행2번 읽기 구현/396대역검사는[별도 기록](업무_구현검증.md#member2-read-session-check-20261020)입니다.

수정 요청은 PR 브랜치 [8967d8e](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/8967d8e9a64315e19c79424c5d259b89ab71de2a)에 기존 README와3번 새1문서로 푸시하고 원격 SHA를 확인했습니다. PR52 제품 브랜치/타 담당 코드는 수정하지 않았습니다.

근거: `ES/tmp/remote_work_pr52_review_20261020/web_review_race.cjs`, `official-checks.json`과 고정 소스·공식 검사 직접 실행입니다. 이전 [3번 회신 수신](공통규약.md#member3-followup-received-20261020)의 '전체 승인/실행 아님'은 당시 기록으로 보존합니다.

<a id="pr50-review-20261020"></a>
## 최신 — PR49 코드·PR50 기술 문서 검토 병합, 2026-10-20

[PR49](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/49)의 회원 지문 등록 버튼/장치별 주소 연결은 `f428034`에, 이후 발견한 [PR50](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/50)의006/008 현행화·009 번호 예약/5번 접점 수용 기록은 `8cbdf8b`에 검토 후 병합했습니다. 50번은 기존Markdown2개뿐이며 실제009 SQL·신규 모델/flag/action·공유 적용을 추가하지 않습니다. 최신589 결합으로2번 ABC31파일을 보존했고, 실제/검증 tree `0fc6fae7e063baaad72faa9eb9c697a143014ff7`가 일치합니다. 병합 직후 열린PR은0건이었습니다.

PR50 제출 `0b0cb9e868b479ce4b1fc8102243e03347ae5636`, 최신 base `589917e9a1c1bca799c74ac3b2499c8f055e1c00`, merge `8cbdf8b26ff9023179ab816b84dd9e10f9e7fb12`, 검증/실제 tree `0fc6fae7e063baaad72faa9eb9c697a143014ff7`입니다. 최초 검토 base `f428034` 뒤2번 ABC가 올라와 즉시 병합하지 않고589에 재결합했습니다. 최신 base 대비 기존MD2개/20추가·7삭제이며 새31파일이 전부 보존됩니다. Ready·무충돌·제출 head 고정 조건으로 병합했습니다.

006 SQL/API의 PR46 병합,008 CASCADE/삭제감사유지의 팀장 확정·PR45 병합,공통schema1을 실제 파일·현재LOG와 대조했습니다. 009는SQL파일 없이 번호만 예약했습니다. 공통action/최소결과·register_write_action의 초기화/덮어쓰기 제한,현재권한·FK signedBIGINT/INTEGER·results ExternalFacts의두필드/명시개발Gate와 문서를 대조했습니다. D/E취소·연장 저장은 새589의ABC와 구분된 후속이며 같은TX/잠금 연결을 완료로 주장하지 않습니다.

연장신청의현재준비지원은 정확히 구분합니다. W1의 `task.deadline_extension_requested`에는 이미 `_own` 준비authorizer가 연결돼 있고, 승인·반려authorizer는 미연결503입니다. 실제 연장신청/판단 route·모델과는 다릅니다. migration목차의 “허용목록에만” 한 문장은 이 사실을 명확히 하도록 후속문서수정하며 코드/SQL은 변경하지 않습니다.

5번 dudu110101의 기존PR43 댓글6281140854(2026-10-20T06:35:08Z)을 직접 읽어 head4825efd의 선택형validateDevice/RegistrationStarted·기존2인자호환·PendingOperation 수용과 실행보고를 확인했습니다. 실제5번수용은 제출접점에 한정되며 팀장병합/운영설정/실물배포와 구분합니다. 이 작성자112/46/12보고를 검토자의49 직접실행에 중복합산하지 않습니다.

기존MD2개·상대링크23개(추가행6)·fence2쌍·새tree/diff검사를 통과했습니다. 현재LOG2 정책앵커와005~008실파일/009미작성,source와SQL blob보존을 확인했습니다. 문서만 바뀌어 프로그램 빌드/테스트·MySQL·009이관·공유적용은 이번50검토에서 실행하지 않았습니다. 49의112/46·114·12·125/독립수명검사·Release0/0은 별도기록입니다. 기존원문·LOG/2번8문서와 실제설정/서버/DB·장치를 보존하며 새협의회신MD/PR을 요구하지 않습니다.

근거: `ES/tmp/remote_work_pr50_review_20261020/audit/merge50.json`, `member5-comment.json`, `current-management-policy.md`, `latest-tested-tree.txt`; `remote_work_pr50_docs_review_20261020/audit/static-review.json`, `newbase-static-review.json`와 최신 코드 직접 대조입니다.

<a id="pr49-review-20261020"></a>
## 최신 검토 — PR49 생성 회원 지문 등록 버튼·주소 연결 병합, 2026-10-20

신규 [PR49](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/49) head `4825efd`를 alpha `73b17e0`와 결합 검토해 추가 차단 결함 없이 `f428034`에 병합했습니다. 실제/검증 tree `03098b4a645499c259c9afb4102b705da4a090bf`가 일치합니다. 생성 응답 회원ID·원래 관리자Session을 유지하고 명시 버튼으로47 등록창을 열며, 선택 장치의 Pi 주소 누락은 발급POST/claim 전에 차단합니다. 원본 연결112·기존회원46/14시나리오·중계114·등록창12·ApiClient125/25시나리오와 독립 수명/경합검사·Release0/0이 통과했습니다. 실제 장치/공유 환경 활성화는 별도입니다.

제출 head `4825efd078c2d3faffbf2a188d5346aba0eb0053`, base `73b17e02a6258625f1a7e914f43e0d0b45319c03`, 실제 merge `f428034ec8b4159ec3abc63e61ca9c5d8a9b133e`, 검증/실제 병합 tree `03098b4a645499c259c9afb4102b705da4a090bf`를 고정 대조했습니다. Ready/무충돌과 제출 head 고정 조건으로 병합했고 병합 직후 열린PR은0건이었습니다.

### 변경과 검토

정확히9파일입니다. UserManagementForm의 생성 회원 등록 버튼과 EnrollmentEndpoints/CreatedUserEnrollmentLauncher·장치별 채널, 기존5번 FingerprintEnrollmentForm의 선택형 validateDevice/읽기전용 RegistrationStarted 접점, 원본/신규 검사와 문서뿐입니다. 2번Tasks·공지45/장치runtime48·MainForm·공통 ApiClient/서버 API·DB 제품은 변경하지 않습니다. 5번 소유 코드의 최소 접점도 직접 검토했으며 실제 담당자 회신을 대신한 것으로 표시하지 않습니다.

- 생성 성공 응답의User.Id와 원래 관리자Session/target·selection generation을 유지합니다. 계정 생성은 자동 채취하지 않고, 기존회원 선택/수정은 새 등록 대상이 아닙니다. 서버completed만 완료로 표시하며 대상/접속이 바뀌면 늦은 결과를 버립니다.
- PC의 별도 로컬 설정은 버전/현재서버·소문자 장치UUID→정규Pi루트만 허용합니다. 중복/다른버전/서버·userinfo/path/query/fragment와64KiB초과를 거절하고 HTTPS가 기본입니다. 격리개발HTTP는 명시 선택이며 인증서 검사를 끄지 않습니다. 파일/키/주소를 자동 생성하거나 운영 비밀값을 사용하지 않았습니다.
- 선택 장치 주소 검사는 attempt/발급보다 먼저 합니다. 미시작 실패는 장치 선택 수정/창재진입이 가능하고, 발급을 시도한 뒤에는 같은 작업 재확인으로 고정합니다. 시도한 창을 닫은 뒤 새 작업을 자동 생성하지 않습니다.
- Launcher는 modal닫힘 뒤 PendingOperation을 비동기로 기다리고 view/channel을 해제합니다. 전송중 부모창닫기·동일계정새로그인·로그아웃/서버변경에서도 채널조기Dispose·중복진입·늦은complete를 차단했습니다.

### 검토자 직접 실행

| 원본 검사 | 결과 |
|---|---|
| UserManagement UI 신규 연결 | **112검사 통과**, 실제 버튼/등록창·통제공통HTTP/Pi채널 |
| 기존 회원 관리 UI | **46검사/14시나리오 통과** |
| C#→실제Python loopback 중계 | **114검사 통과** |
| FingerprintEnrollment UI | **12검사 통과** |
| ApiClient | **125검사/25시나리오 통과** |
| 결합 Release / diff | **경고0·오류0**, diff검사 통과 |

독립 경합3시나리오/54대역검증(소유창닫기·동일계정재로그인·서버변경)과 별도19검사(실제등록시작전닫기2회→동일target/재진입·issue/채널0, 전송중중복진입·새로그인늦은완료 차단)도 통과했습니다. 공식112/46/114/12/125와 중복 합산하지 않습니다. 추가하네스 첫 실행은 초기Shown로드 대기 누락을 교정한 뒤19통과했으며 제품 결함이나 제품 수정으로 기록하지 않습니다. Markdown추가 상대 링크5개와 범위·고정tree를 확인했습니다.

Windows/.NET SDK10.0.401·Desktop8.0.31/Python3.12.14, 기존47 소유 auditvenv(Flask3.1.3/tzdata2026.5,requirements동일)를 사용했습니다. 제품/원본 검사 무수정, 통제HTTP·공개합성값·소유loopback/임시파일·SQLite·fakehardware 범위이며 하위프로세스 정리를 확인했습니다. 실제LocalAppData 설정·운영키·사용자서버/공유DB·계정/.env/실물은 사용하지 않았습니다.

이제1번 회원창 버튼·장치별 주소 주입 접점은 코드로 연결됐습니다. 실제 공통서버/MySQL+Pi+관리자버튼 결합·UART 신규저장/삭제·기존템플릿·두PC·TLS/키배포·공유007적용·전원장애·앱재시작복구/운영정책은 별도입니다. 현재 개발 기본off와 기존 관리자/장치 명시 권한·서버검사는 유지하며 실제 활성화/배포를 실행하지 않습니다. 과거1/5번 “버튼/주소미연결”은 당시 이력으로 구분하고 원문·LOG/2번8문서를 보존합니다. 새 협의회신MD/PR을 요구하지 않습니다.

근거: `ES/tmp/remote_work_pr49_review_20261020/audit/merge49.json`, `scope-review.json`; `remote_work_pr49_checks_20261020/audit/`의 user-management/fingerprint-relay/fingerprint-ui/apiclient/build 결과; `remote_work_pr49_independent47_20261020/audit/launcher-races/`; `remote_work_pr49_launcher_review_20261020/audit/launcher-boundary-final.txt`와 workspace preflight입니다.

<a id="pr48-review-20261020"></a>
## 최신 검토 — PR48 센서·OLED 공유 구성 병합, 2026-10-20

신규 [PR48](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/48) head `a4407f1`를 alpha `df2af6d`와 결합 검토해 추가 차단 결함 없이 `73b17e0`에 병합했습니다. 실제/검증 tree `a193994c2aa98bbb5a4e3d86bb082845b72c903f`가 일치합니다. 기존 단일 UART·센서 잠금·OLED를 빌려 명시 factory로 등록 runtime을 조립하며 **기본 실행은 등록 비활성**입니다. 전체Pi123개 중122통과/Windows POSIX권한1생략, 독립동시성3개 통과, Release0경고/0오류입니다. 45번 공지 삭제 정책과2번 결과 제출 연결·공통 코드는 보존합니다.

제출 head `a4407f1c79ce995abe3ee4d25c087fc94b267eab`, base `df2af6db0c0f8ca01410c5937b2cebc812e4ec74`, 실제 merge `73b17e02a6258625f1a7e914f43e0d0b45319c03`, 검증/병합 tree `a193994c2aa98bbb5a4e3d86bb082845b72c903f`를 고정 대조했습니다. Ready·무충돌·제출 head 고정 조건으로 병합했고 병합 직후 열린PR 목록은0건이었습니다.

### 변경과 직접 검증

- 변경은 정확히6파일입니다. 새 `fingerprint_enrollment_runtime.py`·기존 `pi_api.py`, 신규 runtime검사와 Markdown3개뿐입니다. 명시 keys/profile·비공개 ledger/cache를 받아 기존 FingerprintService의 sensor/codes/lock/OLED/clock/sleep을 공유합니다. 새 UART/키 생성·개인설정 자동 탐색·하드웨어 초기화는 없으며 기본 main/create_app은 등록 route를 설치하지 않습니다.
- 센서 잠금을 얻은 등록만 OLED 안내를 우선 점유하며 저장 후에도 “지문 저장됨·계정 연결 확인 중”으로 표시합니다. main 종료는 scan stopping→등록 취소/승인 대기 해제→조도 정리→공유 잠금 대기→단일 UART 종료 순서이고 중첩 finally로 후속 정리를 유지합니다. runtime.close는 빌린 scan/UART/OLED를 직접 닫지 않습니다.
- **전체 RaspberryPi 원본123개 중122통과/1생략,1.641초**입니다. `python -B -W error::ResourceWarning -m unittest discover -v -s Devices/RaspberryPi -p test_*.py`로 새14개를 포함해 실행했습니다. Windows에서 POSIX 소유자/파일 모드1개를 생략했으며123개 전체 성공이나 실제Pi 검증으로 표시하지 않습니다. requirements-test가 동일함을 대조한 기존 소유 auditvenv(Python3.12.14,Flask3.1.3,tzdata2026.5)를 재사용했습니다.
- **독립 동시성3개 통과**: 첫 등록이 잠금/OLED를 가진 동안 다음 등록sensor_busy와scan409·표시 무침범/취소 후 쓰기0, fake store 진행 중 close의 취소/잠금 대기·쓰기1회/확인 필요 상태·결과 미전달, scan 진행 중 결과 재조회 원문 일치/센서0·표시/잠금 무침범입니다. 실행에는 import된 기존runtime8개도 함께 수집돼 총11통과/2.486초이며 전체123에 중복 합산하지 않습니다.
- fresh process에서 하드웨어/Flask import·파일 쓰기·DB·network/subprocess를 차단하고 pi_api/runtime import가 성공했습니다. 별도 기본 라우트도 `/health`·`/motion`·`/scan`·정적 파일뿐이고 등록 route/새 thread가 없었습니다. Python AST3·문서 상대 링크56개(추가행2개 포함)·diff검사 및 결합 **Release 경고0/오류0**을 통과했습니다.

센서·승인·키는 공개 합성값/대역이고 ledger/cache는 임시 SQLite입니다. 실행 프로세스를 정리했고 검사 증거/격리checkout·venv는 보존했습니다. 실제 장치·운영키·공유MySQL/실계정/.env/사용자 서버는 사용하거나 변경하지 않았습니다. 2번Tasks·45공지와 공통C#/서버/DB 소스는 base 그대로입니다. 기존 C#중계114/창12·서버MySQL 회귀를 이번에 다시 실행한 수치로 쓰지 않습니다.

### 남은 연결

1번 회원창 등록 버튼/장치별 주소 주입, 승인된 운영 keys/profile·상태 저장소와 bootstrap/서비스 설치·키 배포/TLS는 후속입니다. 실제 UART/OLED 동시 동작·Pi123 동일파일 검증·SIGTERM/systemd/전원 장애·실제 신규 등록/삭제·기존 템플릿·회원 매핑/공유DB/두PC는 이번 검토로 완료 처리하지 않습니다. 기본 실행을 활성화하거나 실제Pi 서비스를 교체하지 않았습니다. 새 협의 회신MD/PR 없이 기존 LOG1/5 담당 경계를 유지하며 LOG/2번8문서와 원문/과거 검증을 보존합니다.

근거: `ES/tmp/remote_work_pr48_review_20261020/audit/merge48.json`, `remote_work_pr48_checks_20261020/audit/`의 unittest/build/default-and-environment 결과, `remote_work_pr48_independent_20261020/audit/test_runtime_races.py`, `remote_work_pr48_scope_review_20261020/audit/scope-and-workspace.json`입니다.

<a id="pr45-merged-delete-policy-20261020"></a>
## 최신 — 팀장 삭제 정책 확정·PR45 병합 완료, 2026-10-20

팀장이 본 채팅에서 **관리자가 공지를 삭제하면 해당 공지의 직원별 확인 기록도 함께 삭제**하도록 확정하고 문제없으면 병합하도록 지시했습니다. **삭제 감사 로그는 별도로 남깁니다.** [PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) head `c5dad3b`를 최신 alpha `2718601`와 결합해 추가 차단 문제 없음으로 확인했고, Draft를 해제하여 alpha `a554242`에 병합했습니다. 기존 보존 요구·로컬 선택/팀장 미확정·Draft/미병합은 아래의 과거 이력입니다. 공유DB 적용·팝업 옵션 활성화와 나머지 로그인 경계/주기 정책은 별도입니다.

팀장의 직접 지시: “그럼 공지 삭제 시 확인 기록도 같이 삭제하는 걸로 확정하고, 45번 문제없으면 병합 진행하자.” 직전 확인 답변의 감사 로그 별도 보존도 유지합니다. 현재 ORM/raw008의 `notice_id → notices.id ON DELETE CASCADE`와 일치하므로 삭제 로직·보존 필드·이관을 새로 구현하지 않았습니다. 사용자 삭제/감사 보존기간·운영 이관 등 다른 정책까지 확정한 것은 아닙니다.

| 최종 고정 대상 | 확인값 |
|---|---|
| 제출 head | `c5dad3bfef863ea798cf2dafe5b90ecb91ff5cef` |
| 최신 결합 base | `2718601ada9b92479d767e503622a63c810c1dd1` |
| 실제 병합 commit | `a554242fb7dc0a9a82fb22910219b16d5ff54990` |
| 검증/실제 병합 tree | `30435fe296e21ec794df7d33a4f3ef047c86d631`, 일치 |

병합 직전 alpha에2번 결과 제출 연결이 추가돼 이전 base `b03d83a`에서 즉시 병합하지 않고 최신271 결합본을 다시 검사했습니다. TasksControl/Designer·통합검사·TASK_QUERIES와 README의 오래된 검토 문구 제거를 보존했습니다. 최신 README는45 결합에서도 그대로였고 이번 상태 기록은 공지 절의 현재 정책만 추가합니다. 오래된 PR 상태 문단을 복원하지 않습니다.

### 직접 확인

- 최신271+45 결합본 Release **경고0·오류0**, Notices **35검사 통과**, 새 소유 MySQL8.2 물리 검사 **12통과·생략0,11.84초**입니다. 실제 기본 공지 삭제 **200→재삭제404, 확인 기록0행, `notice.deleted` 감사1행**을 팝업 옵션off에서도 확인했습니다. 명시 동일DB FK 허용/다른DB 거절·raw/ORM·물리 불일치 무수정·동시 초기화도 포함합니다.
- 이전0dfa 실행의 공식 Activity **297검사/12시나리오2회**, Notices35, popup26(물리12+SQLite/대역14)·독립FK1을 이번에 다시 모두 실행한 것으로 합산하지 않습니다. c5의 추가는 문서·SQL/시험 주석이며 Python AST/SQL 정의·C#146파일이 동일합니다. b03→271도 Attendance/MainForm/ApiClient/FeatureUiScope/Activity 검사가 동일해297을 반복하지 않았습니다.
- 현재 공통/기본 공지 초기화에서 popup 테이블을 자동 생성하지 않는 독립 기본off/별도metadata·전용준비 명령/008과 schema1 경계를 유지합니다. R45-01·02와 PID 읽기 경합은 해결 상태입니다. 공유DB/실계정/.env/실행 서버·실장비는 사용하거나 변경하지 않았고 소유MySQL/DB/runtime 정리를 확인했습니다.

검토/병합 승인과 운영 적용은 구분합니다. 팝업 정확한 로그인 수신 경계/cursor·부서 변경·주기/재표시·다중PC 등 나머지 협의와 실제 앱/공유DB의008 적용·팝업 활성화는 이 병합으로 자동 실행하지 않습니다. 전체 서버 회귀·실장비·공유DB·두PC/장시간·전체GUI E2E를 이번 검증으로 대신하지 않습니다.

근거: `ES/tmp/remote_work_pr45_gate271_20261020/audit/physical-gate-summary.json`, `physical-gate.txt`·빌드/공지 실행 결과와 `remote_work_pr45_recheck_20261020/audit/final-merge/merge45.json`, 기존 고정 head 회귀·기능 동일성 근거입니다. 기존 문서/과거 실패·LOG/2번8개 구조를 보존하며 새 협의 회신MD/PR을 요구하지 않습니다.

<a id="pr45-policy-followup-20261020"></a>
## PR45 최신 추가분 — 삭제 정책 문서 정정·Draft 유지, 2026-10-20

[PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) 검토 마무리 중 head `c5dad3b`이 추가됐습니다. Markdown3개·SQL/시험 주석만 바뀌었고 실행 로직·DDL은 이전 검증본 `0dfa517`과 동일합니다. **4번 문서는 로컬 선택을 “공지 삭제 시 확인 기록도 함께 삭제”로 정정**했으므로, 아래 “보존 요구와 CASCADE 불일치”는 이전 요구 시점 기록입니다. 이 보고를 팀장의 공통 삭제 정책·최종008/공유 적용 승인으로 확대하지 않습니다. 최신 head는 충돌 없이 결합되며 **Draft·미병합을 유지**합니다.

후속 제출 head `c5dad3bfef863ea798cf2dafe5b90ecb91ff5cef`과 당시 실제 alpha `73488a407c14260bd43b862fb1f9bc010db513bd`를 무충돌 결합했고 tree `3b3a9eaf81acf64c248ed0012f4ba6520a7502b6`/diff 검사를 확인했습니다. 이 alpha의 변경은 앞선45 검토 상태 README뿐이며44·46·47 제품은 유지됩니다.

`0dfa517→c5dad3b`는 정확히5파일25추가/4삭제입니다. Markdown3파일과 raw008 주석·migration시험 주석만 바뀌었습니다. Python AST·주석 제외SQL이 동일하고 C#146파일이 byte동일합니다. **직접 실행은0dfa517에서 공식 Activity297검사/12시나리오2회, Notices35, popup26(물리MySQL12+SQLite/대역14), 독립FK1, Release0/0**이며 최신 head에서는 기능 동일성과 결합을 검증했습니다. 주석 추가 때문에 전체 시험을 다시 실행한 것으로 표시하지 않습니다. PID helper의 파일없음/음수/비정상값도 독립 하네스에서 각각 약5.02/5.02/5.01초 제한 예외 종료를 확인했습니다.

최신4번 문서는 앞선 로컬 보존 요구가 철회되고 공지/직원별 확인 행을 함께 CASCADE 삭제하는 선택으로 바뀌었다고 보고합니다. 현재 ORM/raw008과 일치하며 감사 기록을 같이 삭제하지 않습니다. 따라서 보존 필드/이관 미구현을 현재 로컬 선택과의 코드 충돌로 계속 기재하지 않습니다. 다만 해당 문서도 로컬 선택과 팀장/1번의 최종 공통DDL·공유 적용 승인을 구분합니다. 이 검토에서 새로운 공통 정책 확정이나 Ready 전환·병합·공유008적용은 하지 않았습니다. 로그인 시작 경계/cursor·부서 변경/주기/다중PC 등 기존 협의도 남습니다. PR본문이 이전head/보존 요구를 표시하는 동안은 고정 head의 최신 문서와 구분합니다.

새 실행 결함은 발견하지 못했습니다. 위 정책 보고를 본 채팅 사용자의 추가 지시로 취급하지 않았고, 다른 채팅/팀원에 메시지를 보내지 않았습니다. 원문·직전 검토/이전 실패 근거·LOG/2번8문서 구조를 보존합니다. 실제 프로젝트에는 상태 README만 추가하며 검토자가 미병합45 제품 소스를 복사하지 않습니다. 실장비·공유DB/계정/.env는 사용/변경하지 않았습니다.

근거: `ES/tmp/remote_work_pr45_static_final_20261020/audit/comment-only-recheck.json`, `preservation.json`, `owned-review-summary.json`, `remote_work_pr45_pid_recheck_20261020/audit/recheck-summary.json` 및 각 고정 head diff입니다.

<a id="pr45-pid-recheck-20261020"></a>
## PR45 후속 수정 확인 — 충돌·PID 경합 해결 / Draft 유지, 2026-10-20

[PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) head `0dfa517`에서 최신 alpha `14c1c80` 반영과 병합 충돌 해소, 공식 Activity 회귀의 PID 파일 읽기 보완을 확인했습니다. 공식 원본 전체 **297검사/12시나리오를2회 통과**, 공지35검사·Release 경고0/오류0입니다. R45-01·02 해결도 유지됩니다. **공지 삭제 후 확인 기록 보존 구조는 여전히 미구현이므로 Draft·미병합·008공유 적용 보류**입니다. 아래 과거 “alpha 충돌/공식 Activity PID 경합 실패”는 해당 head 시점 이력이며 최신 해결 상태와 구분합니다.

제출 head `0dfa517811049d53b8aedbf97761205dd2d05afd`, 검토 base alpha `14c1c806c56ca0b2b701ad98ee427a55f46f71cd`, 결합 tree `2d81def261ffc075b588dcb739cf6a828004a4ac`입니다. 최신 alpha가 제출 head의 조상이며 무충돌 결합을 확인했습니다. 기존 PR44·46·47의 장치/Pi·Enrollment·Results·공통 ApiClient 소스는 alpha와 동일합니다. `.env.example`에는 기본off popup2줄만, registry에는 기존 notice 분기 안 popup3줄만 추가했고 enrollment 설정과 독립 Results 분기를 보존했습니다. 이전45 검토본과18개 제품/문서/시험 파일이 byte동일하며 migration README에는 upstream007 안내1줄이 반영됐습니다.

마지막 커밋은 `Tests/ActivityPresence.Tests/AuthenticationCloseTests.cs`1파일39추가/2삭제입니다. 파일 생성 직후 독점 writer가 닫히기 전 읽는 경합을 `ReadOwnedPid`로 보완했습니다. IOException·빈 값/잘못된 PID를 최대5초·15ms 간격으로 기다리고 실패를 명시하며, 종료 동작 자체의 재시도·종료 제한 완화·무조건 예외 무시는 추가하지 않았습니다. 동일 exclusive writer를 유지하다 PID를 기록/닫는 결정적 회귀3검사를 공식 원본에 추가했습니다. 검사상 가상 PID는 읽기 비교에만 쓰며 프로세스 종료 대상으로 사용하지 않습니다.

| 검토자 직접 실행 | 결과·구분 |
|---|---|
| 공식 원본 ActivityPresence 전체 | **297검사/12시나리오를2회 통과**. 같은297검사 반복이며594개 고유 검사로 합산하지 않음. 제품/공식 검사·진입점 무수정 |
| Notices.Tests 공식 원본 | **35검사 통과**, 실제 WinForms/합성HTTP이며 공유DB 시험은 아님 |
| popup opt-in·물리 migration 공식 원본 | **26통과·생략0,32.67초**, 소유 MySQL12+임시 SQLite/대역14 |
| 이전 독립 외부DB FK 회귀 | **1통과,2.04초**, 새 소유 MySQL에서 다른DB 부모 거절·무변경 확인 |
| Release / diff | **경고0/오류0**, diff --check 통과 |

R45-01 현재DB/FK 검사와 R45-02 정확한 pending GET off404 대역은 유지되며 새 차단 결함은 발견하지 못했습니다. Python popup 기능/검사는 이전 검토본과 동일하지만 최신44·46·47 결합 후 위 표적 검사를 재실행했습니다. 공통/init-db 모델/DDL 버전1과 기본off 경계를 유지합니다.

남은 보존 문제는 기존 `popup_models.py:15`와 raw008의 notice FK `ON DELETE CASCADE`입니다. 문서에 보고된 공지 삭제 후 확인 기록 보존 요구를 아직 충족하지 않아 작성자도 배포금지 비교안/Draft를 유지합니다. 이번 PID 수정과 충돌 해소는 보존 구조·이관/백필·로그인 시작 경계/cursor·부서 변경/주기/다중PC 정책 승인이나 Ready 전환을 뜻하지 않습니다. 실앱/공유DB의008적용·활성화와 코드 병합은 진행하지 않았습니다.

검토 환경: Windows/.NET SDK10.0.401·Desktop8.0.31/Python3.12.14. Activity는 기존 검토용 venv(OpenCV5/NumPy2.3.5/Pillow12.3)를 junction으로 재사용했으며 작성자의 pin 조합과 다릅니다. 합성 이미지·주입 카메라·소유 Python stdio·loopback HTTP/인증 대역 범위로, 실장비/실제 공통 서버·DB/두PC·전체MySQL 회귀·전체 GUI→HTTP→MySQL E2E는 이번에 검증하지 않았습니다. 새 소유 MySQL/DB/runtime 정리를 확인했고 실제 .env/계정·사용자 실행 서버·장치는 변경하지 않았습니다. 미병합45 제품 소스를 검토자가 로컬 프로젝트에 복사하지 않았습니다.

근거: `ES/tmp/remote_work_pr45_pid_recheck_20261020/audit/`, `remote_work_pr45_static_final_20261020/audit/`의 원본 실행 결과·격리 정리 JSON과 `remote_work_pr45_recheck_20261020/audit/updated-review/scope-review.json`, `pid.diff`입니다. 기존 원문과 과거 실패/참고 복사본 결과는 보존하고 새 협의 회신MD/PR은 요구하지 않습니다.

<a id="pr44-47-recheck-20261020"></a>
## 최신 재검토 — PR44·46·47 병합 / PR45 Draft 유지, 2026-10-20

[PR44](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/44) R44-01과 [PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) R45-01·02의 수정 업로드를 확인했습니다. **44번은 수정 재검증 후 병합, 45번은 기존 지적 해결·보존 구현 미완료로 Draft/미병합**입니다. 팀장의 이번 “46번은 수정됐으니 검토” 지시로 이전 보류를 해제하여 [PR46](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/46) 실제 제출·검토 코드를 검토했고, 신규 [PR47](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/47) 등록 중계도 검토했습니다. 44→47→46 순서로 alpha에 병합했으며 최종 코드 merge `1d2cb6b`/tree `0608974b29eb5996f67d34c14b63ae8fbf4fa51f`는 검증본과 일치합니다. 아래 이전 미해결·46보류·미병합은 작성 시점의 이력입니다.

| PR | 검토 제출 head | 실제 병합 / 판정 |
|---|---|---|
| 44 | `75a8259935490cd70ef598fea7c64b7dbac6016c` | `fa99e83b907eb17fc88e1eec793b10ac6eb7219d`, R44-01 해결·병합 |
| 45 | `16397286e2f34862bce855bbe48951d64f8513f1` | 기존 R45-01·02 해결, Draft·보존 구조 미완료로 미병합 |
| 47 | `a13a1b3eff529bf8bff1c25b72bdfa95cc85d79d` | `f6a789db75d626d435c30336a33305abd082bd65`, 44 서버와 결합 검토 후 병합 |
| 46 | `20f03641aa282440a938772573106c6404be7efc` | `1d2cb6b24ab6d27def4efe294359978b5b113fe5`, 44+47+46 최종 결합 검토 후 병합 |

44 병합 tree `dd9fc272c21b47ae80fab4163e8270f5d0760e5b`, 44+47 tree `cecb5fc83ab0a07278fe9c9bd654fcd2c0772684`, 최종 tree `0608974b29eb5996f67d34c14b63ae8fbf4fa51f`를 실제 원격 병합 후 각각 대조했습니다. 제출 head 고정 조건으로 병합했으며 각 단계의 테스트와 실제 tree가 일치했습니다. 44/45 단독 검토 base는 alpha `deedce02302f5d022e9814f6b1991e796bfdda78`입니다.

### 수정 확인과 직접 실행

- **44 / R44-01 해결:** 후보 조회에서 비활성 장치를 제외하고 장치가 없을 때도 대상 회원·관리자·접속 검사를 유지합니다. 비활성 장치 직접 발급·승인·완료의409는 유지합니다. 새 소유 MySQL 표적 **231통과·생략0,180.43초**, 이전 독립 재현 **1통과,2.85초**입니다. 회원 관리 WinForms **46검사/14시나리오**, 공개 등록 바이트 **16검사**, Release 경고0/오류0을 직접 확인했습니다. 과거 전체 서버/환경 보완 수치를 이번 표적 실행으로 바꾸거나 중복 합산하지 않습니다.
- **45 / R45-01·02 해결:** 실제 연결 DB와 FK 대상 schema 비교, 공식 인증 대역의 정확한 pending GET 기능off404를 확인했습니다. 원본 popup opt-in·migration **26통과·생략0,46.30초**(물리 MySQL12+SQLite/대역14), 이전 독립 FK **1통과,3.13초**, Notices **35검사**, 원본 제품/인증 대역을 유지한 표적 인증 종료 **47검사/1시나리오** 통과입니다. 원본 Activity 전체2회는 기존 PID 파일 공유 경합 `AuthenticationCloseTests.cs:239`에서 실패했습니다. PID 읽기에만 최대5초 재시도를 둔 audit 참고 복사본 **294/12통과**를 공식 원본 전체 통과로 대체하지 않습니다. 공지 삭제 후 확인 기록을 보존해야 하는 요구와 현재 CASCADE가 불일치하여 작성자가 Draft/배포 금지 비교안으로 남긴 상태를 유지합니다. 최신 alpha 반영·충돌 재검사와 보존 구현은 후속입니다.
- **47 / 등록 코어·온라인 승인·PC 중계:** Pi Python **108통과/Windows에서 POSIX 권한 검사1생략**, C# 실제 Python loopback 중계 **114검사**, 등록 WinForms **12검사** 통과입니다. 44+47의 실제 Flask/새 소유 MySQL→Pi adapter/fake sensor 독립 **3시나리오,8.56초**를 확인했습니다. 정상 완료/만료 후 결과 읽기·동일 매핑, modeling 후 취소 시 저장0, 만료 후 최초 완료 거절/매핑0을 검사했습니다. 기존11슬롯은 가상 센서에서 보존했고 실제 센서 검증으로 표시하지 않습니다. 44+47 Release0/0입니다.
- **46 / 제출·재제출·첨부·승인/반려·이력:** 새 소유 MySQL에서 원본 **185검사**, 실제 loopback HTTP/4개 WinForms **41검사**, raw006·권한 회수·판단 트랜잭션 rollback/replay·응답 크기 등 독립 **25검사**를 직접 통과했습니다. 실제 alpha44+47에 결합한 최종본에서도 독립 **25검사 재통과**, 새 프로세스 기본off·Release0/0·diff 검사를 확인했습니다. 두25회는 같은 검사의 반복 실행이며50개 고유 시험으로 합산하지 않습니다. C# 실행 스크립트의 고정 reference pack 경로만 audit 복사본에서 설치된8.0.31/10.0.12로 맞췄고 제품/검사 논리는 유지했습니다.

모든 DB 검증은 검토자 소유 MySQL8.2/RR과 임시 경로에서 실행했고 MySQL/추가 DB/runtime 정리를 확인했습니다. Windows/.NET SDK10.0.401·Desktop8.0.31/Python3.12.14 환경입니다. 47 최초 tzdata 누락 및 통합 검사의 요청별 request_id 전체 비교는 audit 환경/검사 준비를 보완한 뒤 위 최종 결과로 확인했으며 제품 결함으로 기록하지 않습니다.

### 병합 이후의 범위

44는 독립 기본off 등록 API/007·생성 회원ID 보존, 47은 Pi/PC 등록 흐름과 창까지입니다. 실제 회원 생성 후 등록 버튼·주소 주입/MainForm 연결, 실제 센서·키/TLS·전원 장애·공유007적용은 완료로 선언하지 않습니다. 46은 기본off006/로컬 개발 reviewer·extension gate 어댑터이며 최종 조직/연장 모델·웹 결과물·공유 파일 재사용·MainForm/2번 ResultsSaved 연결·보존/공유006적용은 후속입니다. 업무 관리자와 제출 검토자/배정 차수 JSON정수와 BIGINT 문자열 계약은 구분합니다.

공지 팝업45는 이번 코드 병합/로컬 반영에 포함하지 않습니다. 전체 서버 회귀·전체 수동GUI·실장비·실계정·공유 DB·두PC·장시간/복구 검증을 이번 수치로 대신하지 않습니다. 기존 .env/계정·사용자 실행 서버·장치 설정은 변경하지 않았습니다. 팀장은2번이며 기존 LOG/2번8문서·과거 원문·PR35 CLOSED 이력을 보존하고 추가 회신MD/PR은 요구하지 않습니다.

근거: 검토자 `ES/tmp/remote_work_pr44_recheck_20261020/audit/`, `remote_work_pr45_recheck_20261020/audit/`, `remote_work_pr46_review_20261020/audit/`, `remote_work_pr46_integration_20261020/audit/`, `remote_work_pr47_review_20261020/audit/`, `remote_work_pr44_pr47_integration_20261020/audit/`의 표적/독립 결과·정리 JSON·빌드·병합 정보입니다. 기존 [1번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/1번/2026-10-20_PR44_비활성장치_목록조회_수정요청.md)과 [4번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-20_PR45_FK대상DB_종료회귀_수정요청.md)의 상단 해결 상태를 따릅니다.

<a id="pr45-review-pr46-hold"></a>
## 최신 — PR45 Draft 수정 요청 / PR46 사용자 보류, 2026-10-20

[PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) 최신 head `8f9e2d2`는 **Draft·미병합**이며 R45-01 P2(다른 DB의 같은 이름 부모를 참조한 FK도 준비 검사 통과), R45-02 P2(MainForm 새 popup 요청으로 기존 인증 종료 회귀 실패)를 확인했습니다. 서버 표적118통과와 별도 FK 독립1실패, 공지C#35·Release0/0을 구분합니다. 기존 활동 회귀 원본은 실패했고, 새 경로404만 추가한 검토 복사본은294/12통과입니다. 제출 원본의 통과로 대체하지 않습니다.

**PR46은 사용자의 '46번은 보류' 지시에 따라 검토·병합 보류 / OPEN·미병합 유지**입니다. 문서 답변/임시 시연 정보·정책을 수용하거나 제품 코드로 반영하지 않았습니다. PR44의 R44-01 수정 요청·미병합도 유지합니다.

[4번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-20_PR45_FK대상DB_종료회귀_수정요청.md)에 정확한 최신 SHA/위치·재현·수정 방향을 기록했습니다. 서버 검증은751f624에서 시작했고 최신8f9e2d2와 Python이 동일함을 대조했습니다. 서버 표적118통과(물리9포함)·독립다른DB FK1실패, 최신 공지35/Release0·기존활동원본실패·정확한새경로404대역만 보완한 복사본294/12통과를 구분합니다. 전체 서버 회귀/새 MainForm 실제MySQL E2E를 재실행한 결과가 아닙니다.

R45-01: 물리 FK 검사에 referred_schema가 빠져 다른 DB 부모도 승인. R45-02: 직원 MainForm의 새 pending GET을 기존5번 인증종료 대역이 처리하지 못해 원본 회귀 중단. 후기 MainForm17줄을 포함한21파일을 검토했으며 초기 PR본문의20파일/MainForm미변경은 이전 제출 설명입니다. CASCADE의 확인 행 소실·문서상 보존 요구 충돌/정확한 로그인 경계 등은 알려진 미완료이고 Draft/공유008적용 보류를 유지합니다.

PR46은 사용자가 이 진행 중 '46번은 보류'라고 지정했습니다. 추가 검토/정책 수용/병합을 진행하지 않고 OPEN·mergedAt=null을 확인했습니다. 문서의 임시 시연 정보와 로컬 구현 보고를 이 검토의 완료 근거로 사용하지 않습니다. 3번 문서/제품·정책은 변경하지 않았습니다. PR44는 R44-01 미해결·미병합이며 이전 검토/환경보완 기록을 유지합니다.

소유MySQL/추가테스트DB/경로를 정리했고 사용자 공용DB·서버·계정/.env/장치는 변경하지 않았습니다. LOG/2번8개·기존 원문/다른 제작 채팅 기록도 보존합니다. 근거는 `ES/tmp/remote_work_pr45_review_20261020/audit/`와 위 수정 요청입니다.

<a id="pr44-review-20261020"></a>
## 최신 검토 — PR44 수정 필요·미병합, 2026-10-20

[PR44](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/44) head `ba6a37a`를 alpha `6cb4d5d`와 결합해 검토했습니다. **R44-01 P2: 정상/비활성 장치에 모두 명시 권한이 있으면 eligible-devices가409로 끝나 정상 장치도 선택할 수 없음**을 새 소유 MySQL에서 재현했습니다. 기존 전체 서버 **1차 704통과·3환경실패·12미설정생략,535.01초 → 보완 관련 23통과·실패/생략0,13.54초**와 별도 독립 회귀 **1실패,3.10초**를 구분합니다. C# 사용자 관리14시나리오/46검사·공개 벡터16검사·Release 경고0/오류0은 통과했습니다. 코드 병합/공유007적용·물리 등록 완료로 표시하지 않습니다. 아래 과거 PR40/41 승인과 신규44 판정은 별개입니다.

[1번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/1번/2026-10-20_PR44_비활성장치_목록조회_수정요청.md)에 정확한 제출 SHA·원인51~53행·최소 pytest 재현·수정/재검증 범위를 남겼습니다. 새 기능27파일은 서버 독립 기본off/별도metadata/007DDL·온라인 승인, 회원 생성ID/관리자 세션 보존입니다. 공통 로그인/HTTP·기존 업무/공지/채팅 제품 코드는 유지했습니다. 전체 pytest와 WinForms/바이트/Release 통과가 다중 장치 후보 목록의 누락 경계를 덮지는 못합니다.

독립 재현은 **정상 장치A+비활성 장치B에 같은 관리자의 grant가 있는 경우**입니다. 후보 GET이 A를 포함한200 대신 B의409로 중단됩니다. 현재 활성/권한/키/대상 검사는 유지하며 목록에서 비활성 장치를 제외하도록 요청합니다. 수정 코드 head 수신 후 재검토합니다. 코드 미병합이므로 alpha의007/실제 등록 API·회원창 변경 완료로 표시하지 않습니다.

직접 전체 서버 **1차 704통과·3환경실패·12미설정생략,535.01초 → 보완 관련 23통과·실패/생략0,13.54초**, 독립1실패3.10초, 회원창46/14·C#벡터16·Release0/0입니다. 전체 안에는 단위/프로토콜·대역과 실제 소유MySQL/HTTP·rawDDL/제약·dump/새DB복원이 함께 있으므로 전체 개수를 모두 실제HTTP/DB시험이라고 부르지 않습니다. 새 소유 DB3회 모두 종료/임시 경로 정리를 확인했고 공유DB/실계정/.env/사용자 서버·장치/템플릿은 그대로입니다.

기존 테스트/문서 원문과 LOG/2번8개 구조를 보존합니다. 이 검토는 신규 운영 정책·공유007적용 승인이나5번 Pi/PC 연결 완료가 아닙니다. 원본 제품/로컬 파일을 대신 수정하지 않았습니다. 근거는 `ES/tmp/remote_work_pr44_review_20261020/audit/`와 위 PR/1번 요청입니다.

<a id="pr43-doc-review"></a>
## 최신 — 1번 PR43 문서 검토·병합과4번 회신 대조, 2026-10-19

[PR43](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43) head `70c02577860c48b9196b86590270875ffdb7ac3b`,base LOG `f9203446a6111c2e7f1f3207e33d16d2e0f7f395`를 직접/독립 정적 검토했습니다. Markdown5개·208추가/삭제0,기존 원문/헤딩 보존,14답변·새 상대 링크15개/고정 코드 경로13개·명시 앵커·diff 검사를 확인했습니다. 차단 수정 없음으로 기존 자동병합 지시에 따라 LOG `ad75eed13b37e6cc9f985041646e26e32848cfc1`에 병합했고,사전/실제 tree는 모두 `51194b516be94fb006ffc8602c51756371d9e6f9`로 일치했습니다. 제품/DDL 변경이나 등록된 CI는 없습니다.

최신 alpha `41bbe82`의 auth/api·write_contract·업무 모델/시작 응답·chat paging·ActivityMonitorContext/IActivityMonitor/공유 view·UI Subscription과 대조했습니다. 역할/활성 변경의 접속 폐기와 부서 변경 차이,배정INTEGER/BIGINT문자열 구분·로그인/공통 쓰기 잠금 순서·제공된 감지 서비스와 실제 MainForm 미연결·보고된 사용자 요구/상세 협의 구분이 정확했습니다. PR40/41의 과거107/31·294/30·빌드 수치를 이번 실행으로 사용하지 않습니다.

[4번24개 회신의 취합](공통규약.md#member4-reply-technical-review): C08/X02 assignment_revision 문자열 표기를 기존 JSON정수로 정정하고,배정차수만으로 관리자변경/취소를 검출하지 못함·현재 없는 due_revision/새 알림 계약·로그인 counter의 전체 잠금 검토를 명시했습니다. 실제4번 원문은 보존하고 상단에 명확화 안내를 연결합니다. 알림 시각/주기·협업 열람/상태·파일/보존·공유 운영은 계속 협의사항이며1번이나 팀장의 일괄 정책 승인으로 표시하지 않습니다.

이번 실제 검증은 문서/정적 소스·키/링크/경로·게시범위·tree 비교입니다. 프로그램/빌드/MySQL·실제 본인확인·근태/MainForm·파일·공유서버/두PC/장시간 부하는 재실행하지 않았습니다. 새 회신 MD/PR을 만들거나3번 기능·다른 담당 제품을 변경하지 않습니다. LOG/2번8개 구조를 유지합니다.

<a id="pr40-pr41-pr42-review"></a>
## 최신 검토 — PR40·41 코드 / PR42 회신 병합, 2026-10-19

[PR40](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/40) 1번 공통 지원은 alpha `7073817`, [PR41](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/41) 5번 독립 감지 서비스/공유 view는 alpha `763f11e`에 검토 후 병합했습니다. 새 차단 수정 요청은 없습니다. 직접 검증은40번 Python107·C#31,41번 C#294/12시나리오·Python30, 결합 Release 경고0/오류0이며 실제 병합 tree와 일치합니다. [PR42](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/42) 4번24개 통합 회신은 기존2문서/원문·링크/앵커를 검토해 LOG `2e6a439`에 병합했습니다. 문서 검토 때문에 제품/DB 시험을 추가 실행한 것은 아닙니다.

40번은 독립 읽기 점검/바이트 초안/화면 요청 수명 도구이고41번은 소유자가 주입할 서비스 준비입니다. 실제 본인 확인 서버·근태 저장·출근~퇴근 MainForm 연결·실장비/공유DB/두PC는 아직 구현/검증 대상입니다. 회신 수신을 제안 정책의 확정으로 확대하지 않습니다. 아래의 이전 대기/미연결은 해당 시점 기록입니다.

| 대상 | 고정 제출 / 병합 / 검증 tree |
|---|---|
| PR40 | head `831e59d874773768f8c9f33692c8372653c6a815`, base alpha `c7fcca27ae51ea5706166e64b4d091ac6a360452`; merge `707381756af8df159fef28e018e466cc0b20358b`; tree `71782ce50670c2919952ef6137169437efe9c393` |
| PR41 | head `f4b20d0393e784fd38b18e985d509d7e8d50bffc`,40번과 결합 후 merge `763f11e6cb23e471d798926ae106d0bad575f5b6`; tree `b710340bf114620eec6e44d59575b1ac5bb1816b` |
| PR42 | head `b6989bc0f1396ae26dbbb60c69874e43b5b02a39`, base LOG `aab4edbc00b0b55b135587789554f1e138cb3afc`; merge `2e6a4395e9924ad496f3e4a4c82b172e5f5c5691`; tree `a85cd123459f1f66338573f81dd7998555689372` |

### 검토자 직접 실행

- 40번 Python: `test_device_protocol_draft.py`, `test_integration_readiness.py`, `test_task_query_readiness.py`, `test_auth.py`, `test_web.py` **107통과·실패/skip0,45.84초**. 새 소유 MySQL8.2/RR을 사용했으며 바이트53개 등 단위/대역도 포함되어107개 전체가 DB 시험은 아닙니다. 기본 DB 미접속, 읽기전용 SQL/누락 열·테이블 무수정, feature 등록과 민감 오류 비노출을 확인했습니다.
- 40번 C#: `dotnet run --project Tests/IntegrationSupport.Tests -c Release` **31검사 통과**. 공개 가상 벡터와 합성 HTTP/UI큐의 선택 변경·동일계정 재로그인·로그아웃·401·Dispose·1000회 세대 교대를 검사합니다. 실제 장치 서명/인증 API가 아닙니다.
- 41번 C#: `dotnet run --project Tests/ActivityPresence.Tests -c Release` **294검사/12시나리오 통과**. 화면 없는 감지·공유 view 닫기/재개·구세대/접속 콜백 억제·취소와 공유 Stop·이전 정리 중 재시작 거절·정리 제한 오류·기존R23-01~03/로그인 복귀 회귀를 포함합니다. 합성 영상/입력·소유 Python stdio·loopback HTTP/인증 대역입니다.
- 41번 Python: `unittest discover -s Tools/HandPresence -p 'test_*.py'` **30통과,2.215초**. 실제 카메라/Pi/모델 다운로드 없음.
- 결합본: `dotnet build RemoteWork.sln -c Release --nologo` **경고0·오류0**. 40·41 순차 병합 후 실제 Git tree가 각각 검증본과 같은지 확인했습니다.
- 42번 문서: 기존 Markdown2개·삭제0/기존 줄 보존,24개 답변 키·추가 상대 링크/앵커2개와 채팅 페이지/seq·문자수·수신 주기 코드를 대조했습니다. 실제 정책/새 코드 승인이나 제품 시험 수치로 합산하지 않습니다.

검토 환경은 Windows / .NET SDK10.0.401·Desktop8.0.31 / Python3.12.14입니다. 작성자10.0.302/8.0.28·Python3.13/3.14 보고와 구분하며 위 수치는 제출 원본을 직접 재실행한 결과입니다. 소유 DB/프로세스/datadir/임시 경로 정리를 확인했습니다. 기존 공유 DB/사용자 계정/.env/서버/장치 설정은 사용하거나 바꾸지 않았습니다.

### 코드·회신·후속 경계

40번11파일과41번7파일의 변경을 확인했습니다. 기존 공통 ApiClient/LoginForm/MainForm,2번 업무·3번 제출·4번 웹/채팅 소스와 서버 등록/모델/DDL은 변경하지 않습니다. 40번의 새 Infrastructure helper는 아직 기존 화면에 채택하지 않았습니다. 41번 기존 기본 생성자는 수동 점검 모드이며 창 닫기 시 정리하고, 공유 view만 외부 소유 서비스를 유지합니다. 최종 MainForm의 생성/주입·서버 출근 성공 ID·로그아웃/만료/정상 퇴근 정리는1번 후속 작성/대조 대상입니다.

1번14개 답변은 [기존1번 질문서](../1번/2026-10-19_공통규약_사전협의요청.md#member1-reply-pr40),5번21개 답변은 [기존5번 회신서](../5번/2026-10-19_C10_C11_회원장치_근태연결_협의회신_초안.md#member5-reply-pr41),4번24개 답변은 [기존4번 회신서](../4번/2026-10-19_C08_C09_채팅공지_협의회신.md#member4-unified-answer)에 취합/수신합니다. 5번은 정정 분담을 수락했으므로 과거 '5번 분담 확인 대기'는 최신 수신 전 이력입니다. 5번이 전달한 필수 지문 시점/탈퇴 삭제/인증1년 보관 요구는 공통 수명 순서와 세부 정책·운영 구현의 대조가 남습니다. 4번의 팝업/확인·협업·기한 알림의 주기/권한/DTO·새 모델은 제안/협의사항입니다. 3번의 실제 제출/검토·파일과2번 수정/배정/연장 협의는 별도이며 새 장치 조건을 업무 권한에 추가하지 않습니다.

미실행: 실제 지문/웹캠/Pi, 실제 본인 확인 키/매핑·challenge/완료 API, 실제 MainForm/출퇴근·연차 저장/정오 요청/보존 작업, 공유 MySQL·실계정·두PC·장시간 부하·전체 수동GUI. 이번 병합만으로 이 범위의 완료를 선언하지 않습니다.

근거: `ES/tmp/remote_work_pr40_41_review_20261019/audit/`의 `pr40-server-tests.txt`, `owned-review-summary.json`, `pr40-tested-tree.txt`, `doc42-verification.json`과 검토 실행 출력. 팀장 기존 지시에 따라 기존 PR/LOG/README만 갱신하며 협의 답변용 추가 PR/새 LOG 문서를 요구하지 않습니다. LOG/2번8개 구조와 과거 원문을 보존합니다.

<!-- team-collaboration-current:start -->
> **1~5번 협업 최신 안내 · 2026-10-19** — [통합 현재 현황/진행 순서](공통규약.md#team-collaboration-current)와 [2번 조치](공통규약.md#collab-member2)를 먼저 확인합니다. 원문과 과거 테스트/게시 수치는 작성 당시 기록입니다. 새 협의 결과·공유 적용이나 다른 담당 구현 완료를 이 문서 최신화로 대신 선언하지 않습니다. 미확정은 모두 **협의사항**입니다.
<!-- team-collaboration-current:end -->

# PR 검토 및 병합 기록

## 최신 독립 재검토 — R38-01 해결·PR38 병합, 2026-10-19

사용자의 “38번 재작업” 요청에서 최신 제출 head `8b57ed1cd74ffcc80a66e53784061874b86af149`에4번 수정이 이미 반영된 것을 확인하고, 기준 alpha `cd763c8392b5103acb5a86573b8045d36da44244`와 결합해 직접 재검증했습니다. **R38-01 해결·새 차단 문제 없음**으로 [PR38](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/38)을 alpha `8cfdfea21425c2cd43f05907c618098b34a56dd7`에 병합했습니다. 검증본/실제 병합 tree는 `bfc9afbeb0ad8da028b36e99f7d613bf6fe1586b`로 일치합니다.

수정본은 page·list_target을 실제 조회 폼에 포함하고, 대상 필터 변경은 서버에서1페이지로 초기화합니다. 정렬 링크도1페이지로 시작하며, 오래되거나 삭제된 선택은 다른 공지 본문으로 대체하지 않습니다. 마지막 빈 페이지와 빈 목록도 처리합니다. 수정은4번의 web.py/index.html/웹 회귀/WEB.md에 반영됐고, 검토자는 제출 제품 코드를 추가 수정하지 않았습니다.

| 이번 직접 검증 | 결과 |
|---|---|
| 원본 웹/공지/API 회귀 | 소유 MySQL8.2/RR에서 **75통과·실패/skip0,112.30초**. 페이지2/3의 첫·중간·마지막 선택6건, 필터/정렬·삭제/빈목록, 쿠키/API·CSRF·권한·rollback·동시삭제·콜드 등록 조합 |
| 이전 실패의 독립 재현 | 실제 렌더된 form 값을 그대로 제출하는 같은 검사 **1통과,2.31초**. 기대/실제 모두review-notice-20, page 보존True |
| 정적 범위 | PR14파일, Python AST5파일·상대 문서링크5개. 공통 auth/common/db/models/app factory·WinForms·2번 업무 소스 보존 |
| 실행 환경 정리 | 새 소유 DB/프로세스/datadir/임시 경로 정리 확인. 공유 DB/기존 .env/계정/서버 설정 사용 없음 |

수정 전 독립 재현은 review-notice-20 대신00을 표시했지만 같은 재현이 이번에는20의 본문과 제목을 표시합니다. 입력에는 `direction,list_target,page,selected,sort,target`이 실제 렌더돼 있으며 테스트가 누락 필드를 만들어 넣지 않습니다. 이전65통과·별도1실패, 작성자75/73·SQLite브라우저 보고를 이번 결과에 합산하지 않습니다.

고정 Python3.12.14와 기존 검토 venv로 Flask test client+실제 소유 MySQL을 사용했습니다. 이번에는 실제 브라우저→MySQL·키보드/JS 브라우저 자동화·공유 DB·두PC·부하·WinForms 빌드/전체GUI를 실행하지 않았습니다. 등록 콜드프로세스의 메모리DB 설정은 모델/경로 대조이며 별도 제품SQLite 회귀가 아닙니다. 웹 팝업/확인 저장·실제 첨부·마감 알림·C09 정책은 여전히 별도입니다.

이제 웹 공지 목록·상세·관리자 작성/삭제는 **기존 notice opt-in을 켠 개발 서버에서 사용 가능한 alpha 코드**입니다. 기본off·003/현재 DB·전역schema1·쿠키/Bearer 채널·전역CSRF/CSP·전체32KiB를 유지합니다. 닫힌PR35를 승인 근거로 삼지 않고 사용자의 재작업·기존 문제없으면 병합 지시에 따라 이번 제출 코드를 판정했습니다.

근거: `ES/tmp/remote_work_pr38_rework_20261019/audit/`의 `owned-review-summary.json`, `server-mysql.txt`, `pagination-regression.txt`, `pagination-repro.json`, `scope-verification.json`, `test_pagination_review.py`. 기존 실패 근거와 원문은 아래에 보존합니다. 다른 제작 채팅의 협업 문서/2번 코드와 LOG/2번8개도 유지합니다.


## 최신 PR 검토 — PR38 수정 요청·PR39 병합 확인, 2026-10-19

코드 [PR38](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/38)은 **R38-01 P2: 2페이지 선택→조회 시 page 누락으로 다른 상세 표시**를 실제 MySQL에서 재현해 미병합입니다. 기존65통과·별도 회귀1실패입니다. 문서 [PR39](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/39)는 검토 도중 LOG `754a6ea`에 병합된 상태를 확인했고 독립 문서 검증에서도 차단 문제를 찾지 못했습니다. 1번이 회원/장치 본인 확인 서버·DB/MainForm,5번이Pi/PC 중계·감지/근태를 작성한다는 역할 회신이며5번 확인·제품 구현은 대기입니다. [4번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-19_PR38_웹공지_페이지선택_수정요청.md)을 따릅니다. PR35는 CLOSED·미병합을 유지합니다.

| 검토 대상 | 고정 기준·실제 결과 |
|---|---|
| PR38 | head `0924db4abe4ea3ab9e7c593fbe0b6d0c1f4bdadb`, base alpha `56cda1fa15ea08a38142e99c524fa3706189c272`,14파일. 미병합·R38-01 수정 요청 |
| 기존 서버 회귀 | 소유 MySQL8.2/RR + Flask test client65통과·실패/skip0,91.81초. 웹/API·권한/CSRF·rollback·삭제경합/등록 조합 |
| 독립 페이지 재현 | 렌더된2페이지 form의 실제필드만 제출→review-notice-20 대신00 상세200.1실패,2.16초 |
| PR39 | head `a9387d9d221858191d7a3f9eb6bc7dcb7926fffb`, baseLOG `1ee575b42e872d9b79b09bce22b964e4914d8574`; 실제 병합 `754a6ea615b897ea26b283e67e43c84229c0048f` |
| PR39 독립 문서 검증 | Markdown6개·신규1·원문5보존, 추가 상대링크9개·MainForm5메서드/ApiClient3멤버 대조. 검토/병합 tree `056859502244764897cd0554abccee5d69ffd2cb` 일치 |

PR39는 이 채팅에서 병합을 실행했다고 기록하지 않습니다. 검토 중 이미 병합된 상태를 확인했으며 같은 head/병합 트리를 독립 검증했습니다. 1번 역할 수락·5번 분담 확인 대기·실제 제품 구현 전을 구분합니다. 이 문서에 별도 프로그램/DB/장비 시험을 수행한 것으로 기존65개나 이전 벡터 결과를 전용하지 않습니다.

PR38의 P2는 조회 form의 page 누락과 서버 기본1/첫행 대체가 결합된 문제입니다. 수정 방향·필터 초기화/선택 유지 경계·재현 코드/출력은 PR/4번에 게시합니다. 원본 제품 코드는 변경하지 않았습니다. 미병합 웹 기능을 현재 alpha에 연결됐다고 표시하지 않습니다. 소유 MySQL/DB/임시 경로만 정리했고 공유 설정·현재 사용자 데이터·실제 브라우저/두PC/부하·팝업/파일은 미검증입니다.

근거는 `ES/tmp/remote_work_pr38_review_20261019/audit/`의 `owned-review-summary.json`, `pagination-repro.json`, `doc39-verification.json`과 실행 로그입니다. 다른 제작 채팅의 수락/재작업 코드·잔여 협의 정리와 원문은 보존하며 LOG/2번8개를 유지합니다. 아래는 이전 회차 기록입니다.

## 최신 추가 문서 — PR39 분담 정정 수신·병합, 2026-10-19

[PR39](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/39) head `a9387d9d221858191d7a3f9eb6bc7dcb7926fffb`를 독립 검토했습니다.6개 Markdown(신규1·기존5), 삭제/제품/DDL0이며 기존 원문을 보존한 상단 정정입니다. PR36의 역할 질문과 대표 회신을 대조하고 고정 alpha `56cda1f`의 MainForm/Attendance 접점·공통 함수, head 기준6문서의 상대 파일 링크213개를 확인했습니다. 차단 수정 없음으로 기존 자동병합 지시에 따라 LOG `754a6ea615b897ea26b283e67e43c84229c0048f`에 병합했습니다. 사전/실제 결합 tree는 모두 `056859502244764897cd0554abccee5d69ffd2cb`입니다.

- **최신 분담:** 1번은 회원–장치 매핑·challenge/완료 검증·서버 모델/DDL/키·MainForm 연결,5번은 Pi 새 스캔/인증 결과·PC 중계·감지 서비스·근태 도메인을 작성합니다. 이전의 device/identity 서버 전체를5번 작성으로 읽히는 부분보다 이 정정이 우선합니다.
- **상태:** 1번 역할 수락·5번 분담 확인 대기·제품 구현 전. 새 경로/DTO·기술 상세/TTL·키 운영·근태/등록/보관 정책은 해당 담당 대조와 협의사항이며 공통 로그인/ApiClient 전체 변경 허용으로 확대하지 않습니다.
- **2번 반영:** [담당별 협의사항](공통규약.md#담당별-협의사항과-회신-방법)의 작성/회신 경계를 정정했습니다. 업무 조회·수락·재작업·제출에 장치 조건을 추가할 근거는 없으며2번 제품 수정은 없습니다. 콜백 내부의 현재 접속/generation/work_session/Form 재검사도1·5번 회신 항목입니다.
- **실행 경계:** 문서 정적 대조/링크·원문/범위·tree 확인만 이번 PR 검증입니다. 빌드·DB·실제 계정/키·장치·두PC 검사는 이 문서 PR 때문에 재실행하지 않았습니다. PR37 공개 벡터28 및2번 새 API의 실제 MySQL/HTTP 검증과 합산하지 않습니다. PR35 원문과 다른 채팅 범위의 제품 PR38은 검토하지 않았습니다.

## 추가 문서 독립 검토 — PR34·36 수신과 PR37 재확인, 2026-10-19

2번 구현 채팅에서 사용자 지시대로 **PR35 원문은 검토하지 않았습니다.** 다른 PR 채팅이 기록한35닫힘·37병합은 아래 원문을 보존합니다. PR34·36은 기존 오류 없으면 자동병합 지시에 따라 검토 후 처리했고, PR37은 이미 다른 채팅에서 병합됐으므로 중복 병합하지 않았습니다.

| 문서 PR | 검토/병합 결과 |
|---|---|
| [34](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/34) | head `1039dcda4a69c558c10b0a7e0458293105775a5c`, 문서10개·제품/DDL0. 수락/재작업의 기술 수용과 로컬 조회 환경 준비 절차를 현재 공통 소스/초안과 대조. 차단 수정 없음, merge `adbdc8ae605865ed57943601e0566892e09d7ccc` |
| [36](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/36) | head `092b1db8539c5e8d1dac59f5ea3ea29b02e0cd60`, 문서2개·제품/DDL0. 로그인 이후 본인 확인·장치 신뢰·근무 서비스 수명/정책 초안을 구분. 차단 수정 없음, merge `79808f897792a988ea20e0c0700f2030b8920547` |
| [37](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/37) | head `3a676f19317daf57a3053f715c4fd9aa7ead96c0`, 이미 병합된 기술 회신/공개 벡터 추가 독립 검토. 차단 결함 없음. 검토 원문8개의 상대 링크290개 존재 확인 |

34/36의 미리 계산한 결합 tree와 실제 병합 tree는 각각 `201ff30c0aaf0701e2ea9676c33ece3a232c83af`·`628f40b2690360209eadf9a06c1461936d41398d`로 일치합니다.34의 로컬 서버 준비는 공유 서버/계정 준비 완료가 아니며,36/37의 기술안은 공동 정책/실제 장치 구현이 아닙니다. 작성자가 보고한 MySQL/기존 C# 수치는 문서 검토에서 재실행하거나 합산하지 않았습니다.

37 공개 가상 자료는 별도 Python 표준 라이브러리 검사로 **28개 통과**했습니다. 요청400/결과438바이트, UTF8↔canonical base64url·전체 HMAC-SHA256, kind/key_id/payload/키 변조 거절·비정규 인코딩6종, 결과 ticket_digest와 요청 MAC 입력SHA256 연결 및 RFC5869 A.1 HKDF 기대값을 독립 재현했습니다. 기존 C#10검사·제품/DB/실키/장치/두PC는 재실행하지 않았습니다. 로컬 검사기는 `ES/tmp/remote_work_pr37_independent_20261019/verify_vectors.py`이며 제품/문서 원본은 바꾸지 않았습니다.

**회신에 반영할 보완:** C10/C11의 UI 갱신은 Post 이전뿐 아니라 **큐에 넣은 콜백이 실제 실행될 때도 현재 접속·monitor generation·work_session_id·Form 생존을 다시 확인**하는지1·5번이 대조하도록 협의사항에 넣습니다. 미구현 기술안의 명확화이며 현재2번 제품 결함은 아닙니다. 업무 조회/수락/재작업에 장치 선행 조건을 추가하지 않습니다. WEB-N4는 닫힌35의 과거 기술 의견이며 유효 웹 구현 요청/공통 파일 권한으로 사용하지 않습니다.

## 최신 PR 검토 — PR35 닫힘·문서 PR37 병합, 2026-10-19

- **PR35** `[4번][LOG] 웹 공지사항 연결 규약 확인·협의 요청`: 사용자가 “35번은 Close로 변경 잘못된 PR”이라고 지정하여 CLOSED·mergedAt=null을 확인했습니다. 브랜치 삭제나 해당 문서 병합은 하지 않았습니다.
- **PR37** `[1번][LOG] 웹 공지·회원/장치·근무수명 협의 기술 회신`: head `3a676f19317daf57a3053f715c4fd9aa7ead96c0`를 LOG `79808f897792a988ea20e0c0700f2030b8920547`와 결합해 검토했습니다. 새 차단 수정 요청 없이 LOG `c2e8d95e0ff9cbb826c195e845d9da516611b48a`에 병합했습니다. 검토/실제 병합 tree는 `9df928e0f3c185579297aa612631bfb26a18b3d4`로 일치합니다.

| 직접 검증 | 결과 |
|---|---|
| 게시 범위/원문 | Markdown10개, 신규3개·기존7개 원문 보존. 제품 코드/DDL 변경 없음 |
| 추가 상대 링크 | 18개 대상 파일 존재 확인 |
| 공개 request 벡터 | UTF8→base64url 원문·HMAC-SHA256 일치, MAC 입력400바이트 |
| 공개 result 벡터 | UTF8→base64url 원문·HMAC-SHA256 일치, MAC 입력438바이트 |
| 양방향 연결 | request MAC 입력SHA256과 result ticket_digest 일치 |
| 담당 문서 구조 | LOG/2번8개 유지 |

공지 opt-in/현재 역할·채널/현재 읽기·요청32KiB·CSRF/CSP·MainForm 메뉴 Dispose 등의 현재 소스와 기술 회신을 대조했습니다. 장치 MAC/수명안은 협의용 초안이며 새 인증·근태 구현이 아닙니다. 작성자 MySQL31·Python20/C#10·빌드 보고는 이번 독립 실행으로 기록하거나 합산하지 않습니다. 문서 변경이므로 제품 빌드·서버/DB·물리 장치·두PC 검증은 재실행하지 않았습니다.

PR37의 WEB-N4는 PR35 고정 원문을 읽은 1번의 당시 의견입니다. **PR35가 잘못된 PR로 닫혔으므로 그 요청이나 웹 공통 파일 허용 범위를 팀장 승인·유효 작업 지시로 처리하지 않습니다.** 후속 상태를 웹 회신·협의 목차/담당 README에 명시하고 원문을 보존합니다. C10/C11/D5도 기술 방향·상대 회신·공동 정책·최종 모델·코드/공유 적용을 구분합니다.

재현 자료는 `ES/tmp/remote_work_pr37_review_20261019/audit/document-verification.json`과 `verify_documents.py`입니다. 다른 제작 채팅의 2번 수락/재작업 구현·담당별 규약 작업은 보존합니다. 아래는 이전 회차의 기록입니다.

## 최신 독립 재검토 — R23-01~03 해결·PR23 병합, 2026-10-19

사용자의 “PR23 수정 올라왔는지 확인하고 재검토해줘. 문제없으면 병합 진행하자” 지시에 따라 새 head `7af259d8d7020f62eb5457ddd28a3d34de7a28ba`를 기준 alpha `a8d368ef06d284e0c554746d696e9ce4202304e2`와 결합하여 직접 검증했습니다. **R23-01~03 해결 확인, 새 수정 요청 없음**으로 코드 [PR23](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/23)을 alpha `5d9602d19ae16e8b6373ac9ac1bc71bff9500cac`에 병합했습니다.

R23-03 수정은 테스트 `AuthenticationCloseTests.cs`와 README에 한정됩니다. 같은 UI 스레드/메인 소유 안내에 WM_CLOSE를 보내고 IsWindow=false를 기다린 뒤 원래 로그인 복귀·세션 해제 검사를 수행합니다. 이번에는 **제출 원본을 수정하지 않고** 이전 실패 환경에서 통과했습니다. 정상 검사에서 테스트가 부모를 직접 닫아 결과를 만들지 않습니다. 기존 제품 종료 수정·카메라/공통 로그인 코드는 유지됐습니다.

| 직접 실행 | 결과·범위 |
|---|---|
| 원본 C# Release | **264검증/11시나리오 통과**. 실제 안내 종료4조합·로그인/메인/활동 창·loopback 합성401·일반/부모/즉시/지연/강제 정리 |
| Python 원본 회귀 | **30개 통과**. 합성 영상·주입 카메라/모델·Tk/stdio |
| 독립 R23-01 | 수신100→추론완료110에도 STALE·age10·JPEG없음, 대역 카메라/검출기 해제 |
| 제품 Release | **경고0·오류0** |
| 결합/병합 트리 | 검증한 결합본과 실제 병합의 tree가 모두 `3ed3f75dc9402349e435559ea4a68cc98c238da8`로 일치 |

검토 환경: SDK10.0.401·설치된 Desktop8.0.31·고정 Python3.12.14/OpenCV5.0.0/NumPy2.3.5/Pillow12.3.0. 이전 원본 실패·검토 복사본260 통과·작성자8.0.28/3.13 결과와 구분하며 합산하지 않습니다. 이 회차의264는 수정된 제출 원본 결과입니다. CI 등록 결과는 없고 독립 로컬 검증입니다.

관련 문서 [PR32](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/32)(head `12e20db504fc041736fd611959d1e36537d1196e`)는 LOG `54aeae5f93e099969b3ba0f788cd518a94f772ae`, [PR33](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/33)(head `2bb8f080f809973ebe912d623066c90d6ec8fea7`)은 PR `e4955b7a06b14a6c33ebcc8ca2e986c993a76d4e`로 수신·병합했습니다. 기존 작성자 회신·실패 근거는 아래에 보존합니다.

현재 활동 확인은 사용자가 직접 여는 모달 기능입니다. **물리 카메라·Pi·장비30초/정확도/가림/장시간·공통 서버/DB·다른PC/배포는 이번 미실행**입니다. 출근~퇴근 백그라운드 감지·지문/회원·출퇴근/연차 저장과 C10/C11/D5 합의는 별도이며 코드 병합으로 완료 처리하지 않습니다. 2번 업무 조회/수락/제출에 장치 조건을 추가하지 않았습니다.

재현 자료는 `ES/tmp/remote_work_pr23_r23_03_20261019/audit/`의 `dotnet-tests.txt`, `python-tests.txt`, `independent-freshness.txt`, `release-build.txt`에 보관했습니다. 다른 채팅이 진행 중인 2번 조회 준비/서버 점검 작업과 기존 문서는 보존하며, LOG/2번은8개를 유지합니다. 아래 미병합/재검토 대기·R23-03 실패 표현은 각 과거 회차의 기록입니다.

## 최신 독립 재검토 — 코드23 검사 보완·문서30/31 병합, 2026-10-19

코드 head `8191b431ca62dc90a9370ca778a9b9ca19ab843d`와 alpha `abd219bf58bf1f4ea0ee25dfe1f270f6ff31bfc2`의 결합본을 검토했습니다. 검토 도중 #30은 `c90e7ba`, #31은 `50dd6f7`로 갱신되어 추가된 게시 상태/오타 변경도 확인했습니다. 코드 head는 그대로였습니다.

| PR | 이번 결과 |
|---|---|
| #23 활동 확인 | **미병합**. R23-01 지연 방어·R23-02 종료 동작 확인. 원본 C# 검사에 R23-03 안내 닫기 안정성 P2 수정 요청 |
| #30 LOG 회신 | head `c90e7ba14aa905ae1fb042c5689790c8b3aa5a22` → LOG `a1e59739aec3a4ba195510218cc6f7f586064c7a` 병합 |
| #31 PR 회신 | head `50dd6f706d7922b18edfcbedbab1801f4209123e` → PR `e3ff855d55eb352fe2b2c9631b06d5e625e14f80` 병합 |

| 직접 실행 | 결과 |
|---|---|
| Python 기존 회귀 | 30개 통과. 물리 카메라/모델 대신 대역/합성 영상 |
| 독립 R23-01 | 수신100→추론완료110에도 STALE·age10·JPEG없음, 소유 대역 해제 |
| C# 제출 원본 | `real login UI/401/immediate/late notice: product login screen restored`에서 실패 |
| 진단 복사본 | 원본과 동일 실패. native 안내가 남고 main allowClose=False / 로그인 숨김 확인 |
| 안내 닫기 조정 복사본 | 소유 안내에 WM_CLOSE 사용 시260검증/11시나리오 통과. 원본 통과로 표시하지 않음 |
| 제품 Release | 경고0·오류0 |

R23-03은 제품의 인증/종료 오류와 구분되는 회귀 검사 수정 요청입니다. `PostMessage(WM_COMMAND, IDOK)` 성공만으로 안내가 닫혔다고 보장되지 않았고, 제품의 만료 콜백이 안내 뒤 Close에 도달하지 않았습니다. 소유 안내 닫기를 안정화하고 실제 종료를 기다리는 원본 검사로 수정한 뒤 재검토합니다. 세부 재현·원본/진단 결과·고정 코드 링크는 [PR/5번](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/5번/2026-10-19_PR23_과거프레임_지연검사_수정요청.md)에 기록했습니다.

환경: SDK10.0.401·설치 Desktop8.0.31·고정 Python3.12.14/OpenCV5.0.0/NumPy2.3.5/Pillow12.3.0. 작성자8.0.28/3.13 성공 보고와 구분합니다. 실제 카메라·Pi·공통 서버/DB·다른PC·장시간·근태 저장은 미실행입니다. CI 결과가 없으며 서로 다른 실행 수치를 더하지 않습니다. 재현 자료는 `ES/tmp/remote_work_pr23_review_20261019_github/audit/`에 보관했습니다. 제품/제출 테스트·공유 .env/DB·DOCX는 변경하지 않았고 LOG/2번8개를 유지합니다. 아래는 이전 검토 기록입니다.

## 최신 PR 재검토 및 병합 — 2026-10-19

검토 시작의 alpha `a4424dc`, LOG `61f11b5`, PR `2531b79`와 제출 head를 고정해 결합본을 검증했습니다. 문제없는 Ready PR은 기존 사용자 지시에 따라 병합했습니다. 새 결함이 재현된 코드 #23은 병합하지 않았습니다. 아래 이전 회차의 Draft·R23-01 미해결 표시는 당시 기록입니다.

추가 접수 문서 **#29**도 검토해 LOG `d002ec6a98ec49a66d8633d5c255ce03b68c7ef9`로 병합했습니다. 정확한 head `4df9c7d140139cf501bd4bb208c24c393dcd0f06`, 최신 LOG 결합 tree `9410fde`에서 기존3문서·나머지59파일 보존, head/결합본 각각 상대링크48·앵커9 오류0,005 SQL/안내 blob 일치·공지 현재 코드와 정책 대조를 확인했습니다. C08은4번 수신/수용 확인, C09는미합의 제안 수신입니다. 이번 문서 검토로 제품 빌드·DB·HTTP·장비 시험을 재실행하지 않았습니다. README2곳의 끝 빈줄만 후속 정리했습니다.

| PR | 검토 head | 현재 결과 |
|---|---|---|
| #25 DB 검증 | `b122c33e09765c771c1019d6ad73ff566b126f8c` | alpha `a2e8499bde4a615f9a2900083b72448f2011d641` 병합. 새 MySQL DDL/등록/복원 검증 |
| #21 채팅 | `599a55f0eeef41bc060eecfbbf6b3a9963623f37` | alpha `ff908ab61bb6d38a7d4377f8bfea50aaf37d2e06` 병합. R21-01~04 수정 확인·자동 수신 검증 |
| #23 활동 확인 | `dd3b3d6d8b8d59d84217190ff4367e2202d9114b` | **수정 필요·미병합**. R23-01 해결 확인, 새 R23-02 부모 종료 취소 P2 재현 |
| #26 기술 회신 | `9d8ce7c088393ef3f45e6aa04457eab4286e55d4` | LOG `4ccebfe419b9ef84c29448532a85d0797032f5ea` 병합. 1번 기술 수용/제안 수신이며 공유 적용 승인 아님 |
| #27 활동 문서 | `782449d905e0352cec596995f754ab32501ecb9d` | LOG `a3f4aac15edad68ed6d05ed127370c874c6cb6bb` 병합. 작성자 보고 수신·코드23 승인 아님 |
| #28 수정 회신 | `e000d2adc484b85b7c0e9507c4eb5ffe0c237fca` | PR `f5fa07e5996f3f7e1ed84d8ff33ee18b1ac8007b` 병합. 기존 R23-01 요청과 작성자 후속 보존 |
| #29 C08/C09 회신 | `4df9c7d140139cf501bd4bb208c24c393dcd0f06` | LOG `d002ec6a98ec49a66d8633d5c255ce03b68c7ef9` 병합. C08 수용·C09 제안 수신 |

| 이번 직접 실행 | 결과·검증 범위 |
|---|---|
| #25 새 MySQL 8.2 | **24통과**: 기존 등록18·raw002 등록3·raw005 DDL/실제 dump→새 DB 복원3. 실패/skip 없음 |
| #21 C#·cmd | 자동 수신32·저장40·초대47·실제 cmd 환경 격리9 통과. C#은 WinForms/HTTP 대역 |
| #21 새 MySQL | 서버20통과(실제 MySQL19+DDL 컴파일1), SQLite/cold mock10 선택 제외. raw005는 채팅3테이블·schema1 유지 |
| #21 실제 통신 | 두 WinForms→loopback TCP HTTP→소유 MySQL **11통과**. 물리 두 PC가 아님 |
| #23 회귀·이전 수정 | Python30·C#156검증/8시나리오 통과. 독립 지연 재현은 STALE·경과10초·JPEG없음으로 R23-01 해결 확인 |
| #23 신규 종료 재현 | 지연 Stop 대역에서 부모 Close 취소 후 자식만 종료, 부모 미종료/표시 유지 재현. 인증 만료 진입 경로는 MainForm 코드와 대조했으며 실제 인증 요청은 보내지 않음 |
| 문서 #26/#27/#28 | 기존 수정문서7/1/1개 및 나머지48/54/11파일 보존. 각 head/결합본의 상대 링크256/9/0·앵커96/2/0 오류 없음 |
| 병합 alpha 회귀 | `ff908ab`에서 TaskStartPreparation149·TaskCreation79 통과(WinForms/통제 HTTP), 최종 Release 경고0·오류0 |

R23-02: ActivityPresenceForm의 종료 취소가 카메라 정리 전에 발생합니다. 인증 만료 처리의 자식 Close→안내→메인 Close 중 안내를 먼저 닫으면 부모 종료도 취소됩니다. 자식 정리 후에는 메인을 다시 닫지 않아 expired/중지된 확인 타이머 상태로 남습니다. 서버 권한 우회를 재현한 것은 아닙니다. [5번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/5번/2026-10-19_PR23_과거프레임_지연검사_수정요청.md)에 재현·최소 수정 방향·재검증 조건을 이어 적습니다.

서버 검토는 고정 Python3.12.14, #23은 기존 OpenCV5.0.0/NumPy2.3.5/Pillow12.3.0을 사용했습니다. 작성자 Python3.13/3.14 환경과 구분합니다. 소유 MySQL/HTTP·DB·임시 자료 정리를 확인했고 공유 .env/DB·실물 장비는 사용하지 않았습니다. 물리 두 PC·공유/운영 DB 적용·장시간 부하·실제 카메라/Pi·모델 정확도·수동 전체 GUI·3번 제출/판단/파일 통합은 미실행입니다. CI 등록 결과가 없으므로 로컬 통과를 CI 통과로 표시하지 않습니다. 서로 다른 실행/작성자 보고 수치를 합산하지 않습니다.

로컬 재현 자료는 `ES/tmp/remote_work_pr_review_20261019_latest/`의 pr21/owned-review-summary.json, pr23/audit/OwnerClose와 independent_freshness.txt에 보관합니다. PR25의 실제 실행은 검토 도구 출력으로 확인했습니다. README·LOG·PR 최신화 외 제품 코드를 추가 수정하지 않았고 LOG/2번은8개를 유지합니다.

실제 `ES/2차 팀 프로젝트`는 이전 alpha238파일의 일치·새 경로 충돌 없음을 먼저 확인하고 변경37파일을 백업/반영했습니다. alpha 문서 최신화 `3d4464b`의 추적265파일 일치·로컬 DOCX7파일 SHA256 보존·해당 작업공간 Release 경고0/오류0을 확인했습니다. 검토용 Git 폴더와 실제 작업공간을 구분하며 코드23은 포함하지 않았습니다. 상세 동기화 결과는 위 로컬 검토 폴더의 `workspace-sync-b20451ac-4175-4fa5-b54e-fb0320795860/summary.json`입니다.

## 이전 독립 재검토 — 2026-10-19

PR21 정확한 head `52cc0864eb5c84899367be82a750ab713624a86b`: R21-01/03은 공통 Session·Unicode scalar 수정, R21-02는 기본 off 새 프로세스 등록 수정으로 확인했습니다. [추가 R21-04](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-19_PR21_채팅저장_수정요청.md)를 재현했고 Draft·현재 alpha와 README 충돌·1번 공동 수용 대기를 유지합니다. PR23 head `159264429d68434ee391b05e7f01a04c72d006b1`은 [R23-01](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/5번/2026-10-19_PR23_과거프레임_지연검사_수정요청.md)을 재현해 미병합입니다.

| 실제 실행 | 결과·범위 |
|---|---|
| PR21 C# | Release 경고0/오류0, ChatStorage40·ChatParticipants47 통과. HTTP 대역/WinForms |
| PR21 독립 이전/이후 | 이전 `832478e` 0/4 → 새 head4/4. 정상 정보 갱신·같은 계정 재로그인·emoji1,001조회/2,000전송 |
| PR21 서버 | 새 소유 MySQL에 rawDDL3테이블 적용·공통version1보존, 원본 테스트18통과·SQLite관련10선택제외 |
| PR21 등록 경계 | MySQL mock 새 프로세스9개 통과. 실제 DB/SQLite 아님 |
| PR21 추가 결함 | 무작업 Flask 대역의 batch에서 호출자 flag0 → 종료 후1 재현. 실제 DB 작업 없음 |
| PR23 | 기존 대역/Tk16개·AST4파일 통과. 별도 지연 대역 재현에서10초 전 영상이 ‘활동 중’으로 표시됨 |

PR23 검토 환경은 Python3.12.14/OpenCV5.0.0/numpy2.3.5/Pillow12.3.0이며 작성자3.13/Pillow13 전체 환경 재현은 아닙니다. 물리 카메라는 명시적으로 차단했고 실제 모델·사용자 영상은 사용하지 않았습니다. PR21 실제 C#→TCP→MySQL15개는 작성자 보고이며 검토자 독립 실행으로 합산하지 않습니다. 공유DB·두PC·운영/장시간·수동전체GUI는 이번에 검증하지 않았습니다. 새 소유 MySQL·datadir·프로세스 정리를 확인했습니다.

최초 서버 검증 실행기의 SQL 주석 분리 오류는 실행기를 고친 뒤 새 DB에서 재실행했으며 제품 결함으로 기록하지 않았습니다. 환경 조회 중 WindowsApps의 Python 설치 관리자 명령이 관리자 자체를26.4로 자동 갱신했습니다. 이후 해당 별칭을 사용하지 않았고 고정된 bundled Python과 전용 검토 venv를 사용했습니다. Python 인터프리터/프로젝트 기존 venv를 갱신한 것으로 기록하지 않습니다.

PR24는 문서3파일·링크·작성자 보고/실물 미검증·공통 미합의 표시를 코드23과 대조했습니다. rootREADME 충돌은 최신2번/3번 기록과5번 새 안내를 모두 보존해 해결했습니다. 두 parent를 가진 merge commit `afd990b3125e0049403a38f4c4cc9f0d16966f07`을 LOG에 반영한 뒤 GitHub의 MERGED 상태를 확인했습니다. 코드23은 병합하지 않았습니다. 검토 문서 수는 LOG/2번8개를 유지합니다.

실행/재현 근거는 로컬 `ES/tmp/remote_work_pr21_23_24_review_20261019/`의 `csharp-review-results.json`, `server_review/`, `audit/pr23_stale_frame_repro.json`에 보관합니다. 새 수정 head와 미해결 재현 결과를 전달받으면 다시 검토합니다.

최신화: 2026-10-19 · 검토: 팀장·2번

## 현재 상태

| PR | 상태 | 근거·후속 |
|---|---|---|
| #1~#18 | 앞선 코드/문서 병합·후속 처리 완료 기록 | 최초 오류·수정·정확한 head·독립 검증은 아래 당시 원문. 코드 병합을 정식 DDL/공유 적용 완료로 확대하지 않음 |
| #19 장치 코드 | alpha `33afbf6` 병합 | head `9f30f19`, 독립 API23·Windows14 테스트 통과. 실제 장비 재배포·공통 신원/DB/근태 미검증 |
| #20 장치 문서 | LOG `da69128` 병합 | 독립 시연·실제/미실행·D5 합의 구분 |
| #21 채팅 코드 | **병합** alpha `ff908ab`, head `599a55f` | R21-01~04 해결·1번 기술 수용·Ready와 자동 수신/실제 HTTP 검증 확인 |
| #22 채팅 협의 문서 | LOG `0339b29` 병합 | 문서 수신이며 API/DDL 공동 수용·코드 승인 아님 |
| #23 활동 확인 코드 | **수정 필요·미병합**, head `dd3b3d6` | R23-01 해결, 신규 R23-02 인증 만료 시 부모 종료 취소 P2 재현 |
| #24 손 검출 문서 | LOG `afd990b` 병합 | head `fd59f27`. README 충돌에서 두 기록을 보존, 문서 범위만 병합·코드23 승인 아님 |

PR #21의 대표 [수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-19_PR21_채팅저장_수정요청.md), [#19/#20 독립 검토 기록](2026-10-19_PR19_PR20_장치시연_검토_병합.md)을 유지합니다. 검토 당시 실제 테스트와 작성자 보고·미실행을 구분합니다. 등록된 GitHub CI가 없는 회차의 로컬 실행을 CI 통과로 표시하지 않습니다.

## 검토 원칙

정확한 새 head를 확인하고 변경 범위·현재 alpha 결합·실제 테스트·미합의와 Ready 상태를 검토합니다. 문제가 없으면 기존 사용자 지시에 따라 병합하며, Draft·미확정 권한·재현된 오류가 남은 코드를 자동 병합하지 않습니다. 팀원의 포크 동기화만으로 수정/수용 회신이 온 것으로 표시하지 않습니다.

## 과거 기록의 원문

아래 R번호는 통합 전 각 문서와 일대일로 연결됩니다. 과거 PR 상태와 당시 검증 수치는 해당 시점 기록입니다.

### R003

**PR #3 병합과 담당별 수정 요청 분리** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_PR3_%EB%B3%91%ED%95%A9_%EB%B0%8F_%EC%88%98%EC%A0%95%EC%9A%94%EC%B2%AD_%EB%B6%84%EB%A6%AC.md) · 원본 `2026-10-16_PR3_병합_및_수정요청_분리.md` · SHA256 `b22c55c0781a`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.

### R004

**PR #4 전달 문서 병합과 상태 갱신** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_PR4_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9_%EC%83%81%ED%83%9C%EA%B0%B1%EC%8B%A0.md) · 원본 `2026-10-16_PR4_문서병합_상태갱신.md` · SHA256 `8c1c18c0b1c7`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.

### R005

**PR 검토 — 병합 후 1번 담당 전달 사항** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_PR%EA%B2%80%ED%86%A0_%EB%B3%91%ED%95%A9%ED%9B%84_1%EB%B2%88%EC%A0%84%EB%8B%AC%EC%82%AC%ED%95%AD.md) · 원본 `2026-10-16_PR검토_병합후_1번전달사항.md` · SHA256 `d590191c9959`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.

### R009

**1번 W1·N4 회신 수신과 PR #12 문서 병합** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88_W1_N4_%ED%9A%8C%EC%8B%A0%EC%88%98%EC%8B%A0_PR12_%EB%B3%91%ED%95%A9.md) · 원본 `2026-10-17_1번_W1_N4_회신수신_PR12_병합.md` · SHA256 `f6602759b9fb`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.

### R018

**4번 → 2번: PR #7 R1/R2 수정 회신** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_4%EB%B2%88_PR7_%EC%88%98%EC%A0%95%ED%9A%8C%EC%8B%A0.md) · 원본 `2026-10-17_4번_PR7_수정회신.md` · SHA256 `29e33a005331`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.

### R019

**PR #15·#16 공통 쓰기 지원 독립 검토와 병합** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR15_PR16_%EA%B3%B5%ED%86%B5%EC%A7%80%EC%9B%90_%EA%B2%80%ED%86%A0_%EB%B3%91%ED%95%A9.md) · 원본 `2026-10-17_PR15_PR16_공통지원_검토_병합.md` · SHA256 `c8994a08bfd8`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.

### R020

**PR #1·#2 재검토와 PR #5·#6 병합** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR1_PR2_%EC%9E%AC%EA%B2%80%ED%86%A0_%EB%B0%8F_PR5_PR6_%EB%B3%91%ED%95%A9.md) · 원본 `2026-10-17_PR1_PR2_재검토_및_PR5_PR6_병합.md` · SHA256 `4404d7f1b43d`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.

### R021

**PR #7 Ready 재검토와 PR #13·#14 병합** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR7_Ready_%EC%9E%AC%EA%B2%80%ED%86%A0_PR13_PR14_%EB%B3%91%ED%95%A9.md) · 원본 `2026-10-17_PR7_Ready_재검토_PR13_PR14_병합.md` · SHA256 `e477d0a0286f`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.

### R022

**PR #7 검토와 PR #8·#9 문서 병합** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR7_%EA%B2%80%ED%86%A0_%EB%B0%8F_PR8_PR9_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9.md) · 원본 `2026-10-17_PR7_검토_및_PR8_PR9_문서병합.md` · SHA256 `21f0d8de5aa2`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.

### R023

**PR #7 수정 재검토와 PR #10·#11 문서 병합** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR7_%EC%88%98%EC%A0%95%EC%9E%AC%EA%B2%80%ED%86%A0_PR10_PR11_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9.md) · 원본 `2026-10-17_PR7_수정재검토_PR10_PR11_문서병합.md` · SHA256 `e7c07a543834`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.

### R040

**PR #21 채팅 검토·PR #22 협의 문서 병합** · [작성 당시 원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-19_PR21_%EC%B1%84%ED%8C%85%EA%B2%80%ED%86%A0_PR22_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9.md) · 원본 `2026-10-19_PR21_채팅검토_PR22_문서병합.md` · SHA256 `f6fff1facc21`. 당시 결정·테스트·미실행 범위는 원문 기준이며 합산하지 않습니다.
