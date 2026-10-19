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
