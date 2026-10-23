<a id="device-local-validation-20261023"></a>
## 최신 — 로컬 서버 LAN·실물 웹캠 검증, 기존 지문 이전 준비 필요

로컬 서버 health와 **실제 Pi→PC 요청 HTTP 200**을 확인했습니다. 실물 Logi C310에서 Python bridge **161프레임·손 95·움직임 87·오류 0**, 실제 C# 연결 **49/50프레임의 두 번 시작·중지·재시작**을 통과했습니다. 한글 경로 모델 로딩 오류 보완 뒤 Python **34개 회귀**와 실제 SDK 검사도 통과했습니다.

[PR92](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/92)를 alpha **fd1fb5d9**에 병합했고 검증 tree 일치 및 로컬 844파일의 누락 0·내용 차이 0을 확인했습니다. 현재 지문 화면은 WinForms에 있고 웹 로그인/공지에는 없으며, 로컬 장치 관련 4개 설정은 OFF입니다. 소스 누락과 실행 환경 준비를 구분합니다.

[직접 검증·변경·최초 실패·미실행 범위](업무_구현검증.md#device-local-validation-20261023)를 확인합니다. Pi 장비 API는 아직 연결 거부 상태이며 로컬에 원 사용자/지문 저장소가 없어 **지문·조도·근태 통합은 미완료**입니다. [1번의 일관된 DB/서버 설정 이전과 5번의 현재 회차 전환·복구 요청](공통규약.md#device-local-migration-20261023)을 기존 취합에 추가했습니다. 새 회신 전용 MD·PR은 필요 없으며 아래 기록과 앵커는 보존합니다.

<a id="validation-harness-fixes-20261023"></a>
## 최신 — 원본 검증 8종 보완·PR91 병합 완료

[PR91](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/91)의 **773e76e9 → alpha 2613e88a** 병합과 tree 일치를 확인했습니다. 직전 전체 검증에서 보완이 필요했던 **V01~V08의 원본 테스트·공식 실행기를 모두 수정·통과**했습니다. 변경은 테스트·실행기·README 20파일이며 제품 코드·운영 서버/DB·키는 변경하지 않았습니다. 물리 DDL guard도 그대로 유지했습니다.

업무 조회 MySQL은 직원 71·관리자 107단언과 실행기 302검사, 지문 서버는 21개, 공지·채팅 첨부 브라우저는 14시나리오, 업무 웹 잔여 회귀는 **357개와 DDL 불변 검사**를 통과했습니다. 단위·포함 관계가 달라 합산하지 않습니다. 이번 단계에서 전체 C# 51프로젝트·서버 2,076개를 다시 실행한 것은 아닙니다.

[변경 20파일·번호별 보완·직접 검사·초기 실패 이력·미실행 범위](업무_구현검증.md#validation-harness-fixes-20261023)를 확인합니다. 사용자 로컬 20파일도 백업 후 반영했고 전체 844파일의 누락·내용 차이 0과 DOCX 7개 보존을 확인했습니다. 실제 장비·공유 배포·두 PC·수동 전체 GUI 검증은 별도입니다. 아래의 ‘8종 보완 필요’는 이전 검증 당시의 기록입니다.

<a id="full-project-validation-20261023"></a>
## 최신 — 프로젝트 전체 검증 결과

최신 alpha **6ab7a43b**와 같은 소스에서 전체 검증을 진행했습니다. C#51프로젝트 Release 빌드, 서버80모듈의 준비 교정 후2,075통과/1생략, 통합·기본 비활성 실행21종 중19통과와 채팅 브라우저18시나리오 등을 확인했습니다. **원본 테스트/실행기8종 보완이 남아 있어 전체 무오류 판정은 아닙니다.** 최초 실패와 외부 진단 통과를 분리했습니다.

[전체 실행 결과·V01~V08·미실행 범위](업무_구현검증.md#full-project-validation-20261023)를 확인합니다. 제품·원본 테스트·실제 서버/DB는 변경하지 않았고, 사용자844소스도 줄바꿈을 제외한 내용 일치를 확인했습니다. 실제 장비·공유 배포·두PC·수동 전체GUI는 별도 검증이 필요합니다. 아래는 이전 이력입니다.

<a id="pr90-current-20261023"></a>
## 최신 — 잔여 구현·확정 정책 반영, PR90 병합

[PR90](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/90)의 **eac40a55 → alpha 6ab7a43b** 병합/tree 일치를 확인했습니다. 공지 원 UUID/W1 복구, KST11~12 오후 업무 전 지문 확인·일반 근무 조회 버튼 제거, 연차 현재 구현 기준 확정을 반영했습니다. 기존 취소·연장은 실제2·3번 소유 환경 통합258검사, 연차는50검사를 통과했습니다.

[공통규약의 최신 기준·담당별 실제 적용 조건](공통규약.md#remaining-implementation-policy-20261023) · [변경 파일·검사·미실행/동기화](업무_구현검증.md#remaining-implementation-tests-20261023)를 확인합니다. 사용자 소스76개 갱신·46개 추가와 백업, DOCX7개·개인 설정 보존을 완료했습니다. 실제5000/공유 서버 재시작·DB/017·016·운영ACK·장치·개인별 지급은 변경하지 않았습니다. 아래 과거 미구현/미정 표현은 당시 기록입니다.

<a id="pr89-current-20261023"></a>
## 최신 — PR89 R89-01 수정·재검토·병합 완료

팀장 요청으로 PR89의 시작 인계 재사용 결함을 **58bc1ab21a4d8d68dfeb079e0462e84388d79fbf**에 수정하고, 검토 요청을 해결한 뒤 **alpha 97136195d37ce70161bd1c76931f85557739b9d6**에 병합했습니다. 실제 systemd의 MainPID·InvocationID·cgroup·상태를 현재 프로세스와 대조해 옛 환경/인계 자료를 가진 새 프로세스를 거절합니다. 연차 UI 테스트 DPI도 제품 설정과 맞췄습니다.

Pi302개 중298통과·Linux4생략, Leave242·Workday109·회원 연결341/복구95/신규58/관리14시나리오46, Release 두 빌드 경고0·오류0을 직접 확인했습니다. 병합 tree는 검증한 head와 동일합니다. [변경 파일·명령·회귀·실행 한계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr89-fixed-merged-20261023)·[담당별 후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr89-boundaries-20261023). 실제 Pi/systemd/root 전환·공유DB/계정·두PC는 미실행입니다. 별도 KST11~12 지문 정책의1·5번 연결은 이번 PR에 포함하지 않았습니다. 아래는 이전 기록입니다.

<a id="lunch-auth-current-20261023"></a>
## 최신 — 오후 업무 전 지문 확인 시간·근무 조회 버튼 정리

팀장 결정은 **한국 시간11:00 이상12:00 미만에 현재 근무의 본인 확인 성공 하루1회**, 평상시 `근무 다시 확인` 버튼 제거입니다. 자동 근무 상태 조회와 연결 오류/시간 내 저장된 원 결과 복구는 보존합니다. 과거 “정오12시 이후” 기준은 [현재 확정 규약·1번/5번 요청](공통규약.md#member2-lunch-auth-window-20261023)으로 대체합니다.

- **1번:** 서버 시간/당일·중복/원 성공결과와 공통 reader 검증.
- **5번:** 버튼·안내·자동 조회/복구·로그인/근무 수명과 같은 시간 기준.
- **현재 상태:** 규약/담당 요청 반영.1·5번 제품 코드·운영 서버/DB/키·flags는 이번에 수정하지 않았습니다. 미인증·예외 근무/지각·강제 차단은 협의사항이며 새 회신 전용 MD/PR 없이 기존 취합에 한 번 회신합니다.

<a id="pr86-88-current-20261023"></a>
## 최신 — PR86·87·88 검토와 현재 병합 상태

|PR|고정 head|현재 판정|
|---|---|---|
|[86](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/86#issuecomment-6359071539)|`3caa6c033b3b45f309dc2d663b3de23d84f48933`|팀장 정책 확정·병합 `cfc2851f`|
|[87](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/87#issuecomment-6359071873)|`0ecf6807f143987c2ae456a9fc48559993737ccd`|팀장 정책 확정·병합 `389f5ac5`|
|[88](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/88#issuecomment-6359006697)|`f365953df50edb33360ebd976461fa6ab02a64f2`|병합 e2ee34bf|

86은 퇴근 후 당일 일반 비밀번호 접속과 근태 종료 유지,87은 지문 템플릿을 보존한 관리자 계정 연결 이전,88은 재확인한 기존 회원의 명시적 지문 신규 등록 UI입니다. 본 채팅에서 팀장이 **86은 제안대로 확정**, **87은 관리자 유지보수 용도로 한정해 확정**하고 두 PR의 병합을 직접 지시했습니다. 86은 **cfc2851f**,87은 **389f5ac5**로 병합 완료했습니다. 87의 범위는 현재 활성 관리자·해당 장치 등록 권한을 확인하는 기존 서버 로컬 CLI의 명시 유지보수입니다. 실제 계정/슬롯 이전·운영 DDL 적용·서버/Pi 재기동은 이번 작업 범위가 아닙니다.

직전 실행 근거인 독립 MySQL86의74PASS/1skip,87의239PASS/4skip(새transfer20포함),별도추가6,세PR결합32와88실제폼/통제HTTP·Pi대역341/95/58/14시나리오46·Release0경고0오류를 확인했습니다. 최종 tree는 기존 결합32PASS 검증본과 동일하며 이번 검사 재실행은0회입니다. 각 밴드의 포함/반복/생략은 합산하지 않습니다. [정확한 검증·한계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr86-88-reviewed-20261023)·[정책/담당 후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr86-88-policy-20261023). 실제서버/공유DB·env/키/슬롯·센서·운영flags/ACK·두PC는 이번 검토에서 변경/검증하지 않았습니다. 아래는 이전 기록입니다.

<a id="pr85-current-20261023"></a>
## 최신 — PR85 내부망 HTTP 업무·결과물 첨부 검증 연결·병합

[PR85 최종 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/85#issuecomment-6356794850)의 고정 **b46f3679→alpha 412e2089**를 검토 후 병합했습니다. 기존 공통 `RemoteWorkFileHash.sha256`을 업무·결과물 소비 화면에서 같은 origin의 동적 import로 연결해, `crypto.subtle`이 없는 내부망 HTTP에서도 전체 크기·SHA 검증을 수행합니다. 공통 함수·API/DTO/DDL/권한·원 UUID·명시 재시도 계약은 유지합니다.

직접 Node 검사141/83/32와 별도 결과 소비 VM8, 실제 headless Edge13/pageErrors0을 통과했습니다. 브라우저는 소유 loopback HTTP·비 localhost 테스트 도메인에서 secureContext=false/subtleAvailable=false와 제품 JS/CSP/동적 import·업무 업로드/검증 다운로드·모듈404 차단을 확인한 명시 HTTP/데이터 fixture입니다. [정확한 SHA·밴드·한계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr85-reviewed-merged-20261023)·[협업 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr85-lan-hash-20261023). 실계정/공유DB·실제5000 전송·네이티브 파일선택창·두PC 검증은 별도입니다. 아래 기록은 당시 이력입니다.

<a id="member2-pr84-current-20261023"></a>
## 2번 최신 — PR84 검토·병합 / D3 팀장 확정·전달 완료

[PR84 단일 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/84#issuecomment-6355596252)에 D3 정책 확정·@1/3/4 전달과 기술 검토·배포 후속을 기록했습니다. **e498ad2b→ed9b0e81** 병합 tree/부모와 검증본 일치를 확인했습니다. [새 직접 검사·실패/정리 구분](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr84-reviewed-merged-20261023)·[확정 정책/담당 후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr84-d3-adopted-20261023)를 기존 취합에 추가합니다.

기존8문서·원문·앵커와79/80/83 기록은 그대로 보존합니다. 이번에2번 writer/journal·업무 화면·사용자 프로젝트나 공유 환경을 수정하지 않았습니다. 아래 D3 미정/전달 대기는 이전 이력이며 최신 팀장 확정·전달 완료로 갱신합니다. 공유 배포·실계정 삭제 성공, 공지 생성 W1·연차 D1/D2는 별도입니다.

<a id="member2-pr80-83-current-20261023"></a>
## 2번 최신 — PR80 수정·PR83 신규 검토 병합

[PR80](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/80#issuecomment-6354991039) **70d3d924→1b83e4f3**, [PR83](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/83#issuecomment-6355309762) **048572cb→badffbdf** 병합 완료입니다.80R80-01해소/실제파일·명령경로와새Pi관측부를확인했고83현재첨부IDreader의같은인증TX·현재권한/locking read를확인했습니다. [직접검사·반복/실패구분·tree일치](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr80-83-reviewed-20261023)·[담당경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr80-83-boundaries-20261023)를기존취합에기록합니다.

79의별도직접수정8730b9ea→dc823221/50PASS기록과기존8문서·원문·앵커를보존합니다. 이번검토는2번writer/journal/화면·사용자프로젝트를수정/동기화하지않았고 실제서버/DB/정책·키/장치도변경하지않았습니다. 새공개회신MD/브랜치/PR은없습니다. 아래는이전기록입니다.

## 최신 — PR79 두 오류 수정·댓글·병합 완료, 2026-10-23

현재담당 회수 뒤 목록노출 R79-01과 비활성직원 때문에 전체목록403인 R79-02를 **8730b9ea**로 수정했습니다. [댓글·재검증·실제병합](PR_검토기록.md#pr79-fixed-merged-20261023) 후 alpha **dc823221**에 반영했고 검증tree와 같습니다. 실제MySQL50(기존39포함)/원UI115·두Release0/0을 확인했습니다. [5파일변경·미실행·사용자798파일동기화/DOCX7보존](업무_구현검증.md#member2-pr79-fix-check-20261023), [남은정책/담당경계](공통규약.md#member2-pr79-merged-boundary-20261023)를 기존8문서에 취합합니다. 운영쓰기/D1·D2는 미확정이며 아래79미병합은 과거기록입니다.

<a id="member2-pr78-82-current-20261023"></a>
## 2번 최신 — PR78~82 전체 검토·3건 병합 / 2건 수정 요청

[78](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/78#issuecomment-6337428379) R78-01 해소와 협업 수신 최신본 **f4ab296c→ea45d437**, [81](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/81#issuecomment-6354927202) 공통해시 **ee366d2d→560bba27**, [82](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/82#issuecomment-6354944328) private 서버실행/관측 **21f3ca22→99fe414f** 병합 완료입니다. [79](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/79#issuecomment-6354990801) 현재권한 P1·비활성목록 P2, [80](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/80#issuecomment-6354991039) 연차전용 startup 설정 P2로 미병합입니다. [정확한 실행/재현](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr78-82-reviewed-20261023)·[담당 후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr78-82-boundaries-20261023)를 기존 취합에 갱신합니다.

81은2·3번 화면 앞의 공통SHA 함수 공급으로,2번 업무/3번 결과물의 최소소비·취소/현재세션/원file·key/body/전체bytes 검증 연결은 각 소유자의 다음 작업입니다. 이번 검토는2번 writer/journal/업무화면을 바꾸지 않았습니다. 기존2번8문서와 모든 원문/앵커를 보존하며 새 공개회신MD/브랜치/PR은 만들지 않습니다. 실제서버/사용자 프로젝트 동기화·DB/설정 변경도 수행하지 않았습니다. 아래는 이전 이력입니다.

<a id="member2-pr7778-current-20261022"></a>
## PR77 수정 병합·PR78 보완 대기 — 2번 기존 기록 보존

[77](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/77#issuecomment-6337145116) **ef84aedd→38233949**의 R77-01 수정·독립 재검토·Draft→Open→Merge를 수신했습니다. [78](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/78#issuecomment-6337428379) **e0bfbb4a**는 R78-01/P2 채팅 부분 실패 시 원선택 전체 보존 보완 전까지 미병합입니다. [정확한 SHA/검사·작성자 보고 구분](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr7778-reviewed-20261022)·[후속 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr7778-boundaries-20261022)를 따릅니다.

동시 발행된 ef84 수정 구현/검증 기록과 이전2번 writer·마감/PR58 기록은 원문 그대로 보존했습니다. 기존2번8문서·앵커 유지, 새공개MD/브랜치/PR0이며 사용자 프로젝트 재동기화·공유DB/설정 변경은 수행하지 않았습니다.

## 최신 — PR77 R77-01 직접 수정 완료·재검토용 반영, 2026-10-22

사용자가 요청한 CHECK문자열 정규화 오류를 [ef84aed](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/ef84aeddaa7e070ef88a9216c13dd0fc68eaac16)로 기존 PR77 브랜치에 푸시했습니다. [원지적/수정·표현식29/실제MySQL49/원HTTP108](PR_검토기록.md#pr77-r77-01-fixed-20261022)과 [변경파일·미실행](업무_구현검증.md#member2-pr77-fix-evidence-20261022)을 취합합니다. Draft/미병합·운영/사용자프로젝트 미변경을 유지하며 다른 제품·SQL선언·정책은 변경하지 않았습니다. 기존8문서/앵커를 보존합니다.

<a id="member2-pr7576-pr58-current-20261022"></a>
## PR75·76 병합·PR58 회신 취합 — 2번 기존 범위 보존

[75](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/75#issuecomment-6336468832) **e8945bba**→[76](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/76#issuecomment-6336589781) **36f1e55d**를 검토·병합했고 두 실제 tree가 검증 결합본과 같습니다. 기존 실제2번writer 원본은 변함없으며 actual32/188.13초를 직접 재검증했습니다. 다른 직접 검사와 [중앙 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr7576-pr58-review-20261022)에서 구분합니다.

[기존2번 journal·이전 검사/sync 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr72-journal-connection-20261022)은 원문으로 보존하고 이번에 사용자 프로젝트를 재동기화했다고 표시하지 않습니다. [PR58 연차 추가 요청·담당별 후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr7576-pr58-boundaries-20261022)을 함께 취합하며 정책 새 확정/운영DB·dump복원·서버/flags·계정/장비 변경은 없습니다. 기존2번8문서/앵커를 유지하고 새공개MD/브랜치/PR을 만들지 않았습니다.

<a id="member2-pr74-status-20261022"></a>
## PR74 병합 — 5번 기본 공통 연결 수신·2번 기존 범위 보존

[PR74](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/74#issuecomment-6336089532) 최종4452996의 실제 기본 공통 퇴근 factory·reader 소비를 검토해 **652b21a20b08fd7c4e3d77332a7d219366dac779**에 병합했습니다. 검증/실제 tree **d527263428820b2b39acef5e42053331b94b5ae4**가 같습니다. 최신51(기존26 포함)/Owner11/Release0/0·실제 원서버/Pi HTTP/MySQL14는 [검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr74-reviewed-merged-20261022)으로 취합하고 초기 실행 수치와 합산하지 않습니다.

2번 업무에 새 근태 조건을 추가하지 않았습니다. [별도 실제 journal 연결·32/기존7·21/기존 sync 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr72-journal-connection-20261022)과 기존2번8문서/앵커를 보존하며 이번74검사나 제품 동기화로 재표시하지 않습니다. [현재 공통/5번 제공과 운영 후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr74-default-consumption-20261022)을 따릅니다. 실제5000/.env·공유DB/DDL·flags·실장비·사용자 프로젝트는 이번 검토에서 미변경, 새 회신용 공개MD/브랜치/PR은 추가하지 않았습니다.

<a id="member2-pr73-status-20261022"></a>
## PR73 병합과 병행 2번 기록 보존 — 2026-10-22

공통 퇴근 drain·원 결과 소유/MainForm 명시 접점 [PR73](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/73#issuecomment-6335730169)을 사용자 지시에 따라 Draft→Open→**f542902e3edfa551e33eb97ed81f4ef12eff27c3**에 병합했습니다. 최신7692ce4 포함 tree **0a6ee3ad17fb5ed1610088eb065c94957f791248**를 검토·병합본과 대조했습니다. [실제 검사/대역·선행 실행](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr73-reviewed-merged-20261022)과 [1·5번 연결 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr73-handoff-20261022)로 취합합니다.

이번 PR73은2번 업무에 새 근태 조건을 추가하지 않습니다. 별도 병행 기록의 [2번 생성·기한 수정·연장 승인 실제 journal 연결](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr72-journal-connection-20261022)·32/기존7·21 검사·사용자 프로젝트 동기화 보고는 원문 그대로 보존하며 PR73의 새 독립 검사로 합산하지 않습니다. 기존2번8문서/앵커를 유지했고 새 회신용 공개MD/브랜치/PR은 만들지 않았습니다. 4번 binding 소비·baseline/중간상태/공유 적용은 각 담당의 기존 후속 범위입니다.

## 최신 — PR72 병합본·2번 실제 기한 사건 연결, 2026-10-22

PR70·71·72의 [실제 병합 상태](PR_검토기록.md#pr70-72-merged-owner2-connected-20261022)를 확인하고2번 생성·실제 기한 수정·연장 승인에 같은TX 사건 기록을 연결했습니다. **새 실제 MySQL32통과·기존writer7/HTTP21 재통과·Release경고0/오류0**이며, 기본OFF/결과읽기전용·실패rollback/성공replay를 확인했습니다. [변경·직접 검사·미실행](업무_구현검증.md#member2-pr72-journal-check-20261022)과 [담당별 다음 연결/협의사항](공통규약.md#member2-pr72-journal-connection-20261022)을 구분합니다. 다른 담당 제품은 수정하지 않았으며 기존8문서/앵커를 유지합니다. 아래 PR70 Draft/미병합은 과거 회차 기록입니다.

alpha [7692ce4](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/7692ce41cf36e89e6f5881b61efcc7fcb05fe5e5)를 사용자 프로젝트에 동기화했고 추적728파일 일치·DOCX7파일 보존을 확인했습니다. 병행 검토 채팅의 LOG5ecdf79 기록도 보존했습니다.

<a id="member2-pr707172-status-20261022"></a>
## PR70·71·72 병합과 2번 연결 경계 — 2026-10-22

사용자 지시에 따라70·71 Draft를 해제하고70 **cca5b621**→71 **30fcb30d**→72 **7135a293**를 병합했습니다. 실제 각 merge tree와 검토 결합본이 일치하며 최종 Release0/0·WorkdayActivity26/WorkdayState39를 확인했습니다. [검토·기존 증거 재사용/직접 검사](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr707172-merged-20261022)·[담당별 후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr707172-merged-20261022)으로 취합합니다.

2번 현재 facts/변경 경계 제공과1번 실제 journal/binding/HTTP 공급은 코드로 수신했습니다. 원 생성·기한수정·승인연장의 실제 W1 mutate에 기록 함수를 연결하는 작업,4번 새 binding source·baseline/중간상태·정식 DDL/공유 적용은 후속입니다. 기존2번8문서 구조와 아래 원문을 유지하며 새 회신용 MD/PR은 만들지 않았습니다. 실제5000/.env·DB/운영 flags·장비 변경은 없습니다.

## 최신 — PR70 수정 재검토·이전 P2 해결, 2026-10-22

고정2a50535의 [이전 P2 해결·새 HTTP 검토](PR_검토기록.md#pr70-rechecked-20261022)와 [담당별 제공/잔여·내부망 접속 기준](공통규약.md#member2-pr70-recheck-status-20261022)을 갱신했습니다. 기존 독립 재현과 원23/7·HTTP21·OFF12 및 Release0/0을 직접 확인했으며 Draft/미병합·공유 미적용입니다. [검증 범위](업무_구현검증.md#member2-pr70-recheck-evidence-20261022)를 구분하고 기존8문서를 유지합니다. 아래 수정 필요 표시는920145 당시 이력입니다.

## 최신 — 웹 첨부 복원·1/4번 회신·PR70 수정 필요, 2026-10-22

[파일명·임시 삭제 복원과 수신 상태](공통규약.md#member2-web-redisplay-and-pr70-20261022)를 갱신했습니다. 4번 원키 재시도는 고정564b352로 해결을 독립 확인했고, PR70 최신920145는 원 검사22/7 통과와 별개로 **RR 보관 읽기1실패**를 재현했습니다. [수정 요청](PR_검토기록.md#pr70-reviewed-20261022) 전2번 사건 연결/병합은 하지 않습니다. [변경·검증·미실행](업무_구현검증.md#member2-web-redisplay-check-20261022)을 구분하며 기존8문서를 유지합니다.

## 최신 — 2번 독립 복구·성공 화면 보완, 2026-10-22

1·4번 회신 대기 중 취소·연장 원사유/날짜, 재배정·관리자 변경 원래 대상, 웹 성공 업무 번호를 표시하도록 보완했습니다. [범위와 남은 연결](공통규약.md#member2-independent-recovery-ui-20261022)·[직접 검사/미실행/게시](업무_구현검증.md#member2-independent-recovery-check-20261022)를 확인합니다. 다른 담당 제품·API/DB·운영 ACK는 변경하지 않았고 기존8문서를 유지합니다.

## 최신 — 4번 추가 회신 수신·2번 상태/배정 근거, 2026-10-22

14:46 KST에 [3번 PR52](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6287667210)·[4번 PR54](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305519634)의 기존 댓글로 회신했습니다. T3 소비 완료와 코드·검증 자료, 4번 재시도 수정·기술 대조를 구분했습니다. 행동별 시각 보완은 alpha641bc71에서 기존10건 재통과했으며 새로운 제품 동작·운영 ACK는 추가하지 않았습니다.

[기존 PR54 취합 절](공통규약.md#member2-deadline-boundary-reply-20261022)을4번14:26 회신까지 갱신했습니다. 현재facts 제공/소비완료와 실제 사건·초기자료·중간상태 협의사항을 구분하고 [이번 근거 검사](업무_구현검증.md#member2-deadline-state-evidence-check-20261022) 10건 통과와 4번 초안의 원래 키·본문 재시도 수정 항목을 기록했습니다. 기존8문서를 유지합니다.

<a id="pr54-followup-and-pr69-status-20261022"></a>
## 최신 — PR54 1번 회신 대조·2번 경계 검사 / PR69 병합, 2026-10-22

PR54의 [1번 14:06 갱신 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305220140)을 수신했습니다. [4번](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6288083731)은 이 절 작성 당시11:30 회신 기준이었으며, 이후14:26 추가 회신은 위 최신 취합에 반영했습니다.2번은 [기한 변경 작성부/같은TX 결합 위치](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/94a40353a1e6cf2cdff24106a07b34d1ad46b88e/docs/TASK_QUERIES.md#deadline-mutation-boundary)를 대조하고 실제 소유MySQL **17PASS**를 추가했습니다. 새 사건 저장·API·DDL은 설치하지 않았으며 [남은 1/2/4 계약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-deadline-boundary-reply-20261022)과 [검사 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#member2-deadline-boundary-check-20261022)를 구분해 [기존 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305519634)에 회신합니다.

[PR69](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/69)는 고정80af53f를 검토해 alpha **78581f21a61bd14de97a719136c2a5f067d4e1f7**에 병합했습니다. 로그인UI77·독립27·공통ApiClient144/29시나리오·연결88/pending43/연차57, 실제 서버/Pi HTTP·소유MySQL·C# **13PASS**, Release0오류/0경고입니다. [정확한 범위/병합tree/미실행](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr69-reviewed-merged-20261022)을 따릅니다. 지문OFF·공유환경/실물·정오/퇴근/연차 실제 저장은 별도입니다.

## 최신 — 일반수정 첨부와 3번 전달 자료, 2026-10-22

[3번 전달 코드·검증 위치](공통규약.md#member3-connection-code-evidence-20261022)에 내업무→결과물, 업무 첨부, 이번WinForms일반수정 연결을 모았습니다. [이번 변경·검사](업무_구현검증.md#member2-management-attachment-ui-check-20261022)는 과거 웹/제출 검사와 구분합니다. 기존8문서만 갱신합니다.

## 최신 — PR68 업무 웹·첨부 실제 소비, 2026-10-22

[현재 연결/남은 배포 조건](공통규약.md#member2-t3-consumers-20261022): managed read·원래 업무 복귀, 웹 업로드/다운로드/임시 삭제와 등록·일반수정 첨부 최종 목록을 연결했습니다. 다른 담당 코드·운영 DB/ACK는 수정하지 않았습니다. [변경 파일·직접 검사·미실행](업무_구현검증.md#member2-t3-consumers-check-20261022)을 확인하며 기존8문서/회신 한 곳 원칙을 유지합니다.

<a id="member2-own-deadline-facts-status-20261022"></a>
## PR54 요청 대응 — 2번 현재 본인 업무 읽기 제공, 2026-10-22

2번은 [전체 현재 사실·단건 재검사 내부 함수](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/dc525d08312eb6d31c4c9e56aa6367a71103dfb9/docs/TASK_QUERIES.md#own-deadline-current-facts)를 alpha **dc525d08312eb6d31c4c9e56aa6367a71103dfb9**로 제공했습니다. 관리자도 본인 배정만,5상태·취소·기한·배정/업무 버전을 같은 인증 TX에서 읽습니다. 실제 소유 MySQL **52통과·실패/오류/skip0**, 읽기 전후 전체 DB 무변경·오래된 RR/ORM 캐시 뒤 현재 사실/인증 재검사를 확인했습니다. [제공 범위·1/2/4 합의 구분](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-own-deadline-facts-20261022)·[검사 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#member2-own-deadline-facts-check-20261022)·[기존 PR54 통합 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305519634)에 취합합니다. 실제 알림 API/source·서버UTC·기한 사건/초기자료 전환·확인 저장은 후속입니다. 아래 과거의 최소 reader 미제공 표시는 당시 이력입니다.

<a id="pr68-merged-20261022"></a>
## PR68 최종 재검토 — R68-01 해결·병합 완료, 2026-10-22

수정 head **194c472f61c6573f237f8e9d26067345bfeaa9aa**에서 A의 원래 업무·동작에 묶인 성공 안내와 B의 미확정 상태가 구분됨을 확인했습니다. 공식 JS **83PASS**, 독립 해결 회귀 **20PASS**이며 같은 검사는 이전 fec2364 원본에서 실패합니다. JS 구문·diff check도 통과했습니다. 추가 병합 차단 문제가 없어 [PR68](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68)을 alpha **ff32ba225f9079332e83d568fc545acb595b623f**에 병합했습니다. 실제 tree **b813e2d6e0a27199ad51cf6fcb7efed99b11852f**를 확인했습니다.

서버·템플릿·C# tree가 이전 검증본과 동일해 기존 연결150·공용251·lifecycle258·서버191·웹183·C#43/독립198·Release0오류/0경고 근거를 유지합니다. 이 수치는 이번 JS 수정 재실행 수치로 합산하지 않습니다. [최종 검토 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68#issuecomment-6331304609)을 따르며, 아래 fec2364의 R68-01 보류/미병합은 당시 이력입니다. 2번 실제 웹 전송·일반수정 첨부 writer 소비와 공유 환경 검증은 후속입니다.

## 최신 — 업무 웹 화면·파일 선택 준비, 2026-10-22

[현재 규약](공통규약.md#member2-web-file-preparation-20261022): 등록·일반수정의 로컬 파일 선택/정리·선택 중 확인 차단과 좁은 화면 배치/오류 안내를 보완했습니다. 실제 업로드·첨부 저장·서버 삭제는 미연결입니다. 3번 PR68은 별도 검토 채팅에서 병합됐으며 2번 실제 연결·검증이 남았습니다. [직접 검사 86/16·브라우저 3개 폭·미실행 범위](업무_구현검증.md#member2-web-preparation-check-20261022)를 구분하며 기존 8문서/회신 한 곳 원칙을 유지합니다.

alpha `e9ee978` 게시 후 실제 로컬 프로젝트의 추적 파일 638개 일치·DOCX 7개 보존과 Release 경고 0/오류 0을 확인했습니다.

<a id="pr68-review-status-20261022"></a>

> **최종 상태: PR68은 R68-01 해결 후 ff32ba2에 병합됐습니다.** [최종 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68#issuecomment-6331304609)을 따르며 아래 보류 표시는 이전 head 당시 기록입니다.

## PR68 신규 검토 — T3-1~5 제공부 수신·R68-01 수정 대기, 2026-10-22

3번의 [PR68](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68) head **fec2364b8caf9ba8c4ac43a0052f02f3af479223**가 추가됐습니다. 현재 첨부 ID reader·감사 표시·읽기 전용 결과물/서명 복귀·cookie 공용 첨부·C# 내부 base64 전송 보완을 검토했습니다. **R68-01/P2: A 저장 성공 뒤 B 응답 유실에도 B 화면에 원 작업을 구분하지 않은 성공 안내가 남는 현상**을 독립 재현해 [수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68#issuecomment-6331304609)했고 현재 미병합입니다. A 성공은 원래 업무/동작을 명시해 보존하고 B의 미확정 결과와 구분해야 합니다.

새 연결150·공용 첨부251·취소/연장258·서버191·웹183·실제 C#43, 별도 C# 경계198·공식 JS61검사는 통과했습니다. 독립 JS10확인은 현상 검출7+읽기 전용 경합 안전성3이며 해결 통과로 표시하지 않습니다. Release0오류/0경고·각 소유 DB/프로세스/runtime 정리를 확인했습니다. [상세 검토·정확한 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr68-review-20261022)를 따릅니다.

**T3 제공부는 코드 게시·검토/수정 대기**이며2번 실제 웹 링크/첨부 UI·일반수정의 전후 집합/감사/버전/receipt 소비는 후속입니다. TASK_ATTACHMENT_REPLACEMENT_NOT_READY를 해제하지 않았습니다. 실제5000/.env·공유 DB/DDL·운영 키/계정·장비·제품 배포는 변경하지 않았습니다. 아래 이전 상태/미제공 표시는 각 작성 당시 이력입니다.

## 최신 — 등록 파일 선택·삭제 완료와3번 새 패치 수신, 2026-10-22

[현재 공통규약·잔여 작업표](공통규약.md#member2-file-ui-and-t3-reply-20261022)를 먼저 읽습니다. WinForms 등록 파일 선택·명시업로드·PR64 DELETE 소비를 연결했고 제어440·화면95·등록133·실제HTTP33/DB9검사를 통과했습니다. [변경/직접검사/미실행](업무_구현검증.md#member2-file-ui-check-20261022)을 구분합니다.

3번 T3-1~5는 PR52 기존 댓글에 제품5파일 패치가 제공됐습니다. 정책 재합의가 아니라 **새 코드 PR 게시·검토·병합과2번 소비 검증 대기**이며 alpha에 이미 반영된 것으로 읽지 않습니다. 일반수정 첨부503·공유 운영ACK 경계는 유지합니다. 업무 웹 기본조회/쓰기 화면은 구현됐고 남은 read/복귀·웹첨부는 제공부 반영 뒤 연결합니다. 기존8문서/앵커와 회신 한 곳 원칙을 유지하며 아래 내용은 각 회차의 이력입니다.

alpha [f534b68](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/f534b68933c35d9c48a613c2269eebab4d8411ab)에2번24파일을 푸시했습니다. 별도 PR 채팅에서 병합한 PR62/66은 원본 그대로 보존했고 최종 결합 Release도 경고0·오류0입니다. 실제 `ES/2차 팀 프로젝트`는 충돌 사전검사와 변경 전 백업 후 상류를 포함한53파일 반영·추적627파일 일치·DOCX7파일 해시 보존·로컬 Release경고0/오류0을 확인했습니다. 문서91개/링크·앵커1284개 오류0·LOG/2번8문서를 유지합니다. 운영 서버/설정·DB·ACK는 바꾸지 않았습니다.

## 최신 — C# 등록 첨부 전송·재개 / 회신 갱신, 2026-10-22

[최신 회신·담당별 다음 연결](공통규약.md#member2-upload-followup-20261022)을 먼저 읽습니다.1번은 실제 DB006/009/013 적용을 보고했고 파일권한/관계·설정/ACK·두PC는 후속입니다. 게시 전 PR63/64/65 병합을 확인했고 PR64의 captured DELETE·MainForm 명시 탐색 조립은 제공 완료입니다. 최신 alpha18a4855 위에서 같은 검사를 재통과했습니다.1·4번은 확인 성공과 목록 갱신 실패 분리·전체 snapshot 재검사 방향을 수용했으며 실제 source/확인 저장은 후속입니다.3번 T3-1~5 공개 구현은 아직 미제공이며 정책 수용을 다시 묻지 않습니다.

2번 C# 첨부 준비·전송·같은ID 재시도 서비스를 추가했습니다. **제어 HTTP344개·실제 C# HTTP26개·DB9개 통과**, Release경고0·오류0입니다. [변경 파일·검사·미실행](업무_구현검증.md#member2-upload-check-20261022)을 구분합니다. 파일 선택/임시삭제 UI는 아직 미연결이며 다른 담당 제품은 수정하지 않았습니다. 아래 절은 각 당시 이력을 유지합니다.

alpha [9c5c07c](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/9c5c07c9aa6757c83a6d52a0c01cf7bcc3b85fc6) 푸시와 로컬 반영을 완료했습니다. 상류 병합을 포함한54파일 반영·추적602파일 일치·DOCX7파일 보존·실제 로컬 Release경고0/오류0을 확인했습니다. 운영 서버/설정·DB·ACK는 변경하지 않았습니다.

## 최신 — 3번 네 제안 수용·기술 연결 회신

[팀장 수용 결정과 T3-1~5 기술 요청](공통규약.md#member3-proposals-accepted-20261021)을 먼저 읽습니다. 등록 첨부는 기존 공개 helper로 연결하고, 일반수정 첨부의 전후 비교·감사와 read/복귀/웹 adapter는 담당 공개 접점 제공 후 이어갑니다. 기존8문서와 한 곳 회신 원칙을 유지합니다. **2번 등록 API/C# DTO의 선택 첨부 연결을 구현**하고 소유 MySQL/Flask274개·C#제어133개·checkout Release0/0을 확인했습니다. [검사·변경 파일·미실행](업무_구현검증.md#member2-attachment-accepted-check-20261021)을 확인합니다.

alpha [8444983](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/8444983fa20bd318a1ae201144cb71121fe83ae6)에2번16파일과 README를 푸시했습니다. 로컬 `ES/2차 팀 프로젝트`는 충돌 사전 검사와 변경 전 백업 후16파일을 반영해 추적564파일 일치·DOCX7파일 해시 보존을 확인했습니다. 검증 checkout과 실제 로컬 Release는 각각 경고0·오류0입니다. 문서89개/링크·앵커1247개 오류0·LOG/2번8문서 유지입니다. 서버 실행/운영 DB 적용·ACK 설치는 수행하지 않았습니다.

<a id="member3-proposals-current-20261021"></a>
## 추가 수신 — 3번 합의안과 DELETE 수용 회신

3번18:09 KST 통합 회신과18:15 KST PR64 댓글을 확인했습니다. [관리 읽기/명시 복귀/첨부 저장/웹 첨부의 네 합의안](공통규약.md#member3-proposals-received-20261021)은 **수신 뒤 팀장 수용 완료**입니다. [최신 수용 회신·남은 기술 조건](공통규약.md#member3-proposals-accepted-20261021)을 따릅니다. 현재 미지원 URL/필드를 구현 완료로 표시하거나 임의 적용하지 않습니다. 공통 DELETE의 제공 방향은3번도 수용했으며 PR64 미병합/실제 소비·공유DB 후속과 구분합니다. 이번 추가 확인은 코드/댓글의 정적 대조이며 새 기능 시험이나 제품 수정은 없습니다.

<a id="member2-reply14-current-20261021"></a>
## 현재 — 1·4번 새 회신과 2번 내 업무 탐색 제공

1번17:58 KST·4번17:34 KST의 기존 댓글 갱신을 확인했습니다. [제공/미병합/협의사항·담당별 후속](공통규약.md#member2-reply14-navigation-20261021)을 기준으로 합니다. 1번 captured DELETE는 미병합 PR64의 제공물이고, 4번은 제한된 협업 reader의 범위를 수용했습니다. 알림 최소 facts/사건·초기자료/확인 저장·MainForm 실제 조립과 공유006/009/013·ACK는 후속입니다. 당시 진행 중이던3번의 이후 합의안은 위 추가 수신 절을 따릅니다.

2번은 로그인에 고정된 `CaptureOwnTaskNavigator()` 콜백을 제공합니다. 새 내 업무 목록에서 요청 ID만 선택하고 상세를 조회하며, 대상 없음/구접속/폐기/늦은 응답을 성공으로 표시하지 않습니다. 다른 담당 코드·운영 설정·DB·ACK는 수정하지 않습니다. [변경 파일·직접 검사·미실행](업무_구현검증.md#member2-navigation-check-20261021)을 확인합니다. 기존8문서/앵커와 단일 회신 원칙을 유지합니다.

alpha `5b77a60` 게시·원격 대조와 실제 로컬7파일 반영을 완료했습니다. 탐색130·기존HTTP132·화면172·첨부295검사를 각각 통과했고 checkout/로컬 Release경고0·오류0입니다. 추적562파일 일치·DOCX7파일 해시를 보존했습니다. 실제 알림/MainForm/공유 DB와 불명 쓰기 모달 상호작용은 이번 검사 범위가 아닙니다.

<a id="member2-independent-ui-current-20261021"></a>
## 최신 — 회신 대기 중 2번 화면 지연·긴 텍스트 보완, 2026-10-21

기존1·3·4번 회신을 기다리는 동안 새 정책 없이 처리할 수 있는 화면 문제 두 가지를 수정했습니다. 업무 상세 성공 뒤 첨부 지연 때문에 기존 행동이 잠기던 문제와 웹의 긴 제목·관리자명 가로 넘침을 보완했습니다. [변경·재현·직접 검사·미실행](업무_구현검증.md#member2-independent-ui-check-20261021)을 확인합니다.

alpha [63d4062](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/63d4062155537d9e55de1eb54819cfef2c9465aa)에8파일을 게시했습니다. 로컬 `ES/2차 팀 프로젝트`는 기존 파일 백업 뒤 최신 상류를 포함한21파일을 반영했고 추적561파일 불일치0·DOCX7개 해시 보존·로컬 Release경고0/오류0을 확인했습니다.

다른 담당 제품과 API/DB/운영 ACK는 변경하지 않았으며, 별도 채팅에서 병합한 PR61 원본을 보존합니다. 새 협업 요청은 없고 기존 [담당별 취합](공통규약.md#member2-remainder-consumers-20261021)을 유지합니다. 1번은 공통/공유 환경·DELETE,3번은 결과물 진입/복귀·첨부 계약/웹 어댑터,4번은 제한된 연속성 소비·알림 기술 대조를 기존 회신 한 곳에 취합합니다.5번의 새 회신은 필요하지 않습니다.

<a id="member2-remainder-current-20261021"></a>
## 최신 — 3번 회신 소비와 2번 독립 잔여 구현, 2026-10-21

[최신 취합·담당별 남은 연결](공통규약.md#member2-remainder-consumers-20261021)을 먼저 확인합니다. 3번의 관리 읽기 진입·성공 후 복귀·첨부 웹 어댑터는 제안/미제공이며, 이후 팀장 결정으로 등록·수정 `attachment_ids` 의미도 [수용 확정](공통규약.md#member3-proposals-accepted-20261021)됐습니다. 미제공 접점은 기술 후속으로 남습니다.

2번은 기존 계약으로 취소·연장5행동 웹, WinForms 실제 첨부 목록/다운로드, 취소 TX의 업무 첨부 참조 해제, 기존 협업 bridge용 보수적인 이력 reader를 구현했습니다. 공용 DB/운영 ACK·실계정·두 PC를 완료로 표시하지 않습니다. [변경 파일과 직접 검사·미실행](업무_구현검증.md#member2-remainder-check-20261021)을 구분합니다. 기존8문서 구조와 과거 앵커를 보존하며 회신용 새 파일/PR은 만들지 않습니다. 아래의 이전 ‘현재’ 절은 각 당시 이력입니다.

<a id="member2-task-web-management-current-20261021"></a>
## 현재 — 업무 웹 수정·재배정·관리자 변경과 PR52 소비

기존 [팀장 확정 A~C 규약](공통규약.md#member2-management-policy-approved-20261020)에 따라 업무 웹의 수정·재배정·관리자 변경을 기존 API/W1 실행과 연결했습니다. 처음 연 업무의 기준을 유지하고 후보 검색·내용 확인 뒤 명시 실행합니다. 관리자 본인 제외의 정상 성공과 응답 유실 뒤 권한 상실의 결과 불명을 구분합니다. [현재 계약·3번 후속 협의](공통규약.md#member2-task-web-management-20261021), [변경·실행/미실행 검증](업무_구현검증.md#member2-task-web-management-check-20261021)을 따릅니다.

PR52 병합 `cbda025`의 기존 결과물 진입을 소비해 본인 업무에서 `/results?task_id=업무번호&view=own`으로 이동하고, 업무 상세의 현재 GET 재조회를 제공합니다. 업무 관리자 지정과 결과물 검토자는 별개입니다. PR60의 실제 번호 파일 `013_shared_attachments.mysql.sql`과 PR58·57 병합 `ba9e312`도 상류 그대로 받았습니다. 실제 공용 DB 적용·첨부 소비자 TX/파일 UI·통합 ACK는 별도이며 취소·연장 쓰기 차단을 해제하지 않습니다. 다른 담당 제품을 2번이 직접 수정하지 않았습니다.

기존 8문서·앵커와 당시 검증 이력을 보존합니다. 새 회신만을 위한 MD·브랜치·PR은 요구하지 않으며 추가 기술 협의는 기존 공통규약의 F3-05에 모읍니다. 아래 이전 절의 미병합·013 번호 대기·웹 미연결은 당시 상태입니다.

alpha [388d50b](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/388d50b68ef114b40a5df8b424f91ab3103a6093)에2번16파일과 README를 푸시했습니다. 새 관리/3번 소비80검사 통과·기존 관리API/웹202개 통과를 구분해 기록했고 Release경고0/오류0입니다. 로컬 `ES/2차 팀 프로젝트`는 백업 후 최신 상류를 포함한79파일을 반영해 추적539파일 불일치0·DOCX7개 해시 보존·실제 로컬 Release0/0을 확인했습니다. 공유 서버 재시작·DB 적용·ACK 설치를 파일 동기화와 혼동하지 않습니다.

<a id="member2-task-web-writes-current-20261021"></a>
## 현재 — 업무 웹 등록·수락·재작업 시작과 추가 회신 재확인

기존 1번 공통/W1·등록/시작 기술 수용과 3번 명시 재작업 회신에 맞춰, 2번 소유 웹의 등록·수락·재작업 시작 확인/실행/결과 조회를 연결했습니다. API와 같은 실행 함수를 재사용하고 정확한 로그인 접속·대상/본문/키를 서명 확인폼에 고정합니다. 2번 코드 커밋은 `5f9be7d`이며, 이후 1번 [PR59](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/59)의 공통 업무 메뉴 연결을 정상 병합한 결합 커밋은 `92ab386`입니다. 공통 파일은 2번이 대신 수정하지 않았습니다. [규약·수신 재확인](공통규약.md#member2-task-web-writes-20261021), [변경/직접 검사/미실행](업무_구현검증.md#member2-task-web-writes-check-20261021)을 따릅니다.

원격 LOG의 새 커밋만으로 회신 여부를 판단하지 않고 기존 PR 댓글의 갱신도 다시 확인했습니다. [1번의 공용 첨부 DB 검토·013 예약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6305999537), [3번 cc84c1c의 R52-03 수정 보고](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6309410815), [1번 협업 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308644957), [4번 8613c94 수정/범위 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6309130119)을 수신했습니다. 이 담당자 보고를 이번 2번 직접 실행 결과나 미병합 PR52/57의 승인·실제 공유 적용으로 표시하지 않습니다. 2번의 실제 업무 전체 버전 이력 reader와 공용 파일/웹 이동·갱신 소비, 1번 공통/공유 환경 연결은 후속입니다.

취소·연장 실제 쓰기503/통합ACK 미설치는 유지합니다. 미확정 내용은 협의사항으로 남기며 5번 장치나 새 협업 조건을 업무 시작의 선행조건으로 추가하지 않습니다. 기존 8문서/앵커와 아래 당시 이력을 보존하고 회신만을 위한 새 MD·브랜치·추가 PR은 요구하지 않습니다.

이번 웹 연결은 alpha [96734bc](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/96734bcb29eead01369584c0968fa07361f7f7bc)에 푸시했습니다. PR59 메뉴 병합을 보존했고 결합 후 웹81검사·별도 전체196/순수172검사·Release0/0을 확인했습니다. 로컬 `ES/2차 팀 프로젝트`는 충돌 사전 확인과 백업 후20파일을 반영해 추적496파일 불일치0·DOCX7개 해시 보존·실제 로컬 Release경고0/오류0입니다. 설정/실행 중 공유 서버 재시작이나 DB 적용을 파일 동기화와 혼동하지 않습니다.

게시 중 받은 LOG `fecce0f`의 PR59 병합·PR57/58 검토 기록도 정상 병합해 보존합니다. PR57의 기존5건 수정 확인과 별개로 새 **R57-04 알림 중복**이 기록돼 Draft·미병합을 유지합니다. 이 절의4번 자체 수정 보고를 전체 승인으로 표시하지 않으며 [해당 검토 기록](PR_검토기록.md#pr57-59-review-20261021)을 우선 대조합니다. 아래32fc711의 수정대기 기록과 이후cc84c1c의 수정 보고 수신은 서로 다른 고정 후보의 이력입니다.

<a id="member2-task-web-context-current-20261021"></a>
## 현재 — 업무 웹 상세 취소/연장 대기 정보

3번 회신 없이 진행 가능한2번은 기존 lifecycle 옵션에서 취소 사유/UTC 일시와 현재 연장 대기의 기존/희망 기한·사유를 웹 상세에 연결했습니다. 현재 잠근 업무/배정/사용자·취소/대기를 함께 표시해 인증 조회의 이전 MySQL snapshot이 섞이지 않게 합니다.2번 취소 guard도 현재 마커를 검사하도록 보완합니다. [현재 규약·3번 기존 접점](공통규약.md#member2-task-web-context-20261021),[변경/실행/미실행](업무_구현검증.md#member2-task-web-context-check-20261021)을 확인합니다.

웹 쓰기·공통 메뉴·API/DB·ACK·다른 담당 제품은 변경하지 않았습니다. 기존5상태·기한/권한과 실제 취소/연장 변경503을 유지하며3번 실제 소비/공유 검증은 별도입니다. 기존8문서/앵커를 보존합니다.

alpha `44c85f3` 반영과 소유 MySQL85검사 통과·실제 로컬 Release0/0을 확인했습니다. 로컬492추적 파일 일치·DOCX7개를 보존했고 기존 PR57 검토 기록도 유지합니다.

<a id="member2-task-web-current-20261021"></a>
## 현재 — 3번 실제 차단 연결 전달 / 2번 업무 웹 읽기

팀장 요청에 따라3번 Codex용 문안을 채팅에 정리하고 [기존 F3-01/02의 취소/연장 대기·TX/성공 복구·실제 검증 경계](공통규약.md#member2-task-web-and-member3-handoff-20261021)를 보완했습니다. 실제3번 제품/ACK·공유DB는 바꾸지 않았으며 취소·연장 변경503을 유지합니다.

독립 가능한2번은 공통 cookie 로그인과 기존 조회 opt-in에서 `/tasks` 목록/상세·내/관리·전체/5상태·취소 표시/복귀를 구현했습니다. 새 소유 MySQL **31검사 통과**,합성 브라우저 로그인/필터/상세/복귀를 확인했습니다. [파일·실행/미실행·정리 기록](업무_구현검증.md#member2-task-web-check-20261021)을 따릅니다. 현재 공통 메뉴 `/features/tasks`는 준비 화면이며1번 메뉴 연결·웹 쓰기/결과물 접점·공유 계정/두PC는 후속입니다. 기존8문서/앵커와 과거 검증을 보존합니다.

같은 날 이어서 **웹 마감일 연장 신청함 읽기**를 제공합니다. 기존 lifecycle 옵션과 API 조회의 현재 배정/권한을 재사용하며 신청5처리 상태·날짜/사유·업무 상세 링크만 표시합니다. 신청·승인·반려·철회 쓰기와 통합ACK는 추가하지 않고 실제3번 차단 연결을 기다립니다. [동일 통합 기록](업무_구현검증.md#member2-task-web-check-20261021)에 후속 검증을 구분합니다.

최신 alpha `8ed25c6`에는 상류 PR55/56 병합과 이번 신청함을 함께 보존했습니다. 결합 소스의 고유62검사 재통과·Release0/0,로컬490파일 일치·DOCX7개 보존을 확인했습니다. 실제3번 소비·공유DB/두PC·웹 쓰기/공통 메뉴 연결은 완료로 바꾸지 않습니다.

<a id="member2-lifecycle-integration-current-20261021"></a>
## 현재 — 기록 정합성·정식009 기반 취소/연장 통합 준비

최신 alpha/LOG와 대조해 **PR51 실제009·공통 성공 결과 읽기 제공 완료**,3번 실제 취소/pending 소비 미연결을 구분했습니다. 현재 실행 안내의 '009 예약만/실제SQL 없음',옛 callback 수명과 협업 정책 미정 표현을 갱신했습니다.10/21 A1~A6·B1~B7 진행 기준 채택과 과거 검증/앵커는 유지합니다.

2번 소유 ORM을009 엔진/문자셋/정렬과 맞추고 검증 실행기를 raw009로 전환했습니다. 다른 MySQL 기본값·한글/이모지와 실제 results 등록 시 미연결503/저장 보존 검사를 추가했습니다. **실제 변경503과 기본off를 유지하며 다른 담당 제품·공유DB·통합ACK는 변경하지 않았습니다.** [담당별 현재 연결/기존 요청](공통규약.md#member2-lifecycle-integration-progress-20261021),[변경 파일·실행/미실행](업무_구현검증.md#member2-lifecycle-integration-check-20261021)을 따릅니다. 직전 지문/근태·연차·별도 검토 요청 추천안을 채택한 기록은 아닙니다.

<a id="member2-read-session-current-20261020"></a>
## 현재 — PR52 수정 필요 / 회신 기준 2번 읽기 접점 보완

[PR52 전체 검토](PR_검토기록.md#pr52-review-20261020)에서 **R52-01 P1: 과거 결과물 표시와 최신 제출물 승인·반려 대상의 불일치**를 지연 대역으로 재현했습니다. 공식 서버185·웹177·C#HTTP41·Release0/0은 통과했으나 해당 JS 경합을 포괄하지 않습니다. **OPEN·미병합**이며[3번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/3번/2026-10-20_PR52_검토회차_불일치_수정요청.md)에 재검증 기준을 남겼습니다.1번 PR51은 이번 검토 제외입니다.

병행한2번은 [접속 고정 읽기 어댑터·명시 대역의 취소 필터](공통규약.md#member2-read-session-delivery-20261020)를 구현했습니다. 새 정책/DTO/알림·협업 기능 없이 기존 GET을 재사용하며 새92+기존304검사·Release0/0이 통과했습니다. [변경 파일·직접 검사·미실행](업무_구현검증.md#member2-read-session-check-20261020)을 따릅니다. 실제4번 소비/공유 검증과1·3번 lifecycle 연결은 남고 취소·연장503은 유지합니다. LOG/2번8파일 구조·기존 앵커를 보존했으며 회신용 새 파일/PR은 요구하지 않습니다.

<a id="member4-followup-current-20261020"></a>
## 현재 — 4번 F4-01~03 후속 통합 회신 수신

[기존 PR42의 새 댓글과 취합 결과](공통규약.md#member4-followup-received-20261020)를 확인했습니다. 서버 기한/현재 권한·후보 무효화·기존 타입과 협업 권한 자동확대 금지를 유지하고,알림 운영·수명/중복과 협업 최소공개를 구체 제안했습니다. **알림09시/조회60초/backoff·보충·재표시/다중PC·협업 범위/전이는 아직 협의사항**입니다.

4번의 새 알림/협업 서비스·API·DB는 미구현·운영 검증 미실행입니다.2번은 기존 읽기 경로/DTO의 제공 범위와 보장 한계를 대조했고,새 권한·필드·기능을 추가하지 않았습니다. [확인/미실행](업무_구현검증.md#member4-followup-check-20261020)을 따릅니다. 기존3번 수신과 취소/연장503은 유지합니다. 회신용 새 파일/PR은 요구하지 않습니다.

<a id="member3-followup-current-20261020"></a>
## 현재 — 3번 PR52 후속 회신 수신 / 실제 취소·연장 소비 미연결

1번을 제외하고 [3번 PR52 통합 회신과 소스](공통규약.md#member3-followup-received-20261020)를 대조했습니다. 결과물 웹·관리자 이력 웹과 기존 재작업 입력 접점은 **코드 제출·OPEN/미병합**입니다. 취소/pending helper의 실제 소비·첨부/제출 경합,새8행동 이력 표시·공용 첨부는 후속이며 별도 수동/자동 검토 요청은 협의사항을 유지합니다. **취소/연장503을 해제하지 않았습니다.**

이번 확인은 문서/소스 정적 대조이며3번 보고의185/41/177검사를 재실행하거나 전체 PR을 승인한 것이 아닙니다. [직접 확인/미실행 기록](업무_구현검증.md#member3-followup-check-20261020)을 따릅니다. 기존2번 제품 코드·alpha와 타 담당 파일은 변경하지 않았습니다.

<a id="member2-followup-current-20261020"></a>
## 현재 — 화면 점검·문서 정리와 담당별 후속 요청

등록·수락·재작업·취소 복구·연장 신청/승인·목록/5필터·최소 크기·빈 목록/오류를 실제 폼과 **명시 메모리 대역으로 직접 조작**했습니다. 각 메뉴는 독립 상태이며 실제 서버 연속 흐름/권한 검증은 아닙니다. 행동별 성공 안내와2번 문서7개를 정리하고119클라이언트 회귀·Release경고0/오류0을 확인했습니다. [변경 파일·실행/미실행](업무_구현검증.md#member2-manual-review-20261020)을 따릅니다.

[1번 공통/DDL·공유/웹,3번 취소·pending/이력/첨부/웹,4번 알림·협업 후속 요청](공통규약.md#member2-followup-requests-20261020)을 담당별로 취합했습니다.5번의 새 선행 조건은 없습니다. A~E는 확정이며 실제1·3번 소비 연결·공유 환경은 미완료이므로 취소/연장 쓰기503을 유지합니다. 아래 날짜별 ‘최신/미정/미구현’은 해당 회차 기록입니다. 회신용 새 파일/추가PR은 요구하지 않습니다.

<a id="member2-lifecycle-current-20261020"></a>
## 최신 — 취소·마감 연장 2번 구현 완료 / 실제 소비 연결 전503

취소·연장 신청/철회·승인/반려의2번 모델/저장·현재 조회/요청함·Designer 폼과 취소 포함 필터를 구현했습니다. **1·3번 연결 확인 전에는 일반 실행의 실제 변경을503으로 막습니다.** [변경 파일·실제 검사/미실행](업무_구현검증.md#member2-lifecycle-implementation-20261020)을 확인합니다. 서버 고유57·실제HTTP391/독립DB15·새 클라이언트119/기존570·Release0/0을 확인했으며, TESTING 대역과 실제3번 소비·공유 환경은 구분합니다.

다음 연결은 [1번 공통/DDL·등록](공통규약.md#member2-lifecycle-member1-connection), [3번 취소/대기·첨부 각 단계](공통규약.md#member2-lifecycle-member3-connection)입니다. A~E 정책은 이미 확정됐습니다. 아래 구현 중/옛 모델 미준비 설명은 이전 기록이며 다른 담당 제품 코드를 수정하거나 추가 회신 파일/PR을 요구하지 않았습니다.

## 최신 진행 — 취소·마감 연장 2번 구현 / 1·3번 기술 연결

[1번 공통/DDL·등록](공통규약.md#member2-lifecycle-member1-connection)과 [3번 제출·첨부/취소·연장 대기](공통규약.md#member2-lifecycle-member3-connection)에 필요한 내용을 기존 중앙 규약에 나눠 정리했습니다.2번 소유 모델/저장·조회/요청함·폼을 구현하며 정책 재승인은 요구하지 않습니다. 상대 제품 코드는 수정하지 않고, 실제 소비 연결 확인 전 쓰기는503으로 차단합니다. 명시 테스트 대역과 실제3번 통합·공유 적용은 구분하며 검사 결과는 구현·검증 문서에 갱신합니다.

<a id="member2-management-development-current-20261020"></a>
## 최신 — 수정·재배정·관리자 변경 개발 연결, 2026-10-20

[A~E 정책은 팀장 확정](공통규약.md#member2-management-policy-approved-20261020)이며 A/B/C는 서버3경로와 도구상자 관리 폼·성공 재조회/불명 결과 복구를 구현했습니다. D 취소/E 연장은 **정책 확정·기술 연결 전**이고, 연장 로컬 입력은 확정 상태/KST 날짜를 검사합니다. 다른 담당 제품 코드·공통 schema/DDL은 변경하지 않았습니다.

[변경 파일·직접 실행/미실행·남은 접점](업무_구현검증.md#member2-management-implementation-20261020): 서버121, 실제HTTP/폼59+독립DB28, 관리 제어84/연장 로컬32와 기존 회귀 통과입니다. 기본 off·공유 환경 미검증을 유지합니다. 아래 착수/옛 정책 미정은 과거 이력입니다.

<a id="member2-management-current-20261020"></a>
## 최신 — 팀장 A~E 정책 확정·2번 구현 착수, 2026-10-20

일반 수정·재배정·관리자 변경·취소·연장은 **팀장이 전체 추천안으로 확정**했습니다. [현재 정책/상태표·담당별 기술 접점](공통규약.md#member2-management-policy-approved-20261020)을 우선합니다. 아래 과거 ‘3번과 협의 후 결정/정책 미정’은 이력입니다. 일반 수정/재배정은 진행전, 관리자 변경/취소는 진행전·진행중·반려완료, 연장 신청은 진행중·반려완료가 기준입니다.

2번은 기존 공통 저장 도구로 A/B/C 서버·실행 폼부터 구현합니다. D/E는 **정책 확정 / 모델·공통 등록·3번 제출 차단의 기술 연결 필요**이며, 신청·승인/취소 완료로 표시하지 않습니다. 다른 담당 제품 코드는 수정하지 않습니다. 결과는 [실제 검사와 미실행](업무_구현검증.md)에 갱신합니다. 기존 8개 문서와 원문·앵커를 유지하며 추가 회신 파일/PR을 요구하지 않습니다.

<a id="member2-results-entry-current-20261020"></a>
## 최신 — 3번 회신 수신 후 업무 화면의 결과 제출 연결, 2026-10-20

3번46개 답변을 [기존 질문표에 취합](../3번/2026-10-19_팀원들과_합의해야_하는_기준.md#member3-reply-received-20261020)했고 누락/추가/중복0을 확인했습니다. 내 진행중 업무에서 기존3번 제출 폼으로 연결하고, 실제 성공 뒤 현재 상태 필터로 목록·상세를 재조회합니다. 진행전/반려완료는 먼저 수락/재작업 시작이 필요합니다. 검토 요청은 별도 협의사항이고 자동 제출/중복 상태 저장은 없습니다.

[회신별 적용·남은 협의](공통규약.md#member3-reply-member2-connection-20261020), [변경 파일·직접 실행/미실행](업무_구현검증.md#member2-results-entry-validation-20261020)을 따릅니다. C02 수정/재배정/관리자 변경/취소·C07 실제 연장 정책은 회신에서도 미확정이므로 협의사항을 유지합니다. 3번/공통 제품 코드는 수정하지 않았습니다.

## 최신 재검토 — PR44·46·47 병합 / PR45 Draft 유지, 2026-10-20

[PR44](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/44) R44-01과 [PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) R45-01·02의 수정 업로드를 확인했습니다. **44번은 수정 재검증 후 병합, 45번은 기존 지적 해결·보존 구현 미완료로 Draft/미병합**입니다. 팀장의 이번 “46번은 수정됐으니 검토” 지시로 이전 보류를 해제하여 [PR46](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/46) 실제 제출·검토 코드를 검토했고, 신규 [PR47](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/47) 등록 중계도 검토했습니다. 44→47→46 순서로 alpha에 병합했으며 최종 코드 merge `1d2cb6b`/tree `0608974b29eb5996f67d34c14b63ae8fbf4fa51f`는 검증본과 일치합니다. 아래 이전 미해결·46보류·미병합은 작성 시점의 이력입니다.

[직접 검증과 담당별 남은 연결](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr44-47-recheck-20261020)를 따릅니다. 제출 후2번 목록 갱신/MainForm 연결과 수정·배정·연장 저장의 기존 협의 경계는 유지합니다.

## 최신 — PR45 Draft 수정 요청 / PR46 사용자 보류, 2026-10-20

[PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) 최신 head `8f9e2d2`는 **Draft·미병합**이며 R45-01 P2(다른 DB의 같은 이름 부모를 참조한 FK도 준비 검사 통과), R45-02 P2(MainForm 새 popup 요청으로 기존 인증 종료 회귀 실패)를 확인했습니다. 서버 표적118통과와 별도 FK 독립1실패, 공지C#35·Release0/0을 구분합니다. 기존 활동 회귀 원본은 실패했고, 새 경로404만 추가한 검토 복사본은294/12통과입니다. 제출 원본의 통과로 대체하지 않습니다.

**PR46은 사용자의 '46번은 보류' 지시에 따라 검토·병합 보류 / OPEN·미병합 유지**입니다. 문서 답변/임시 시연 정보·정책을 수용하거나 제품 코드로 반영하지 않았습니다. PR44의 R44-01 수정 요청·미병합도 유지합니다.

[상세 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr45-review-pr46-hold)를 따릅니다.

## 최신 검토 — PR44 수정 필요·미병합, 2026-10-20

[PR44](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/44) head `ba6a37a`를 alpha `6cb4d5d`와 결합해 검토했습니다. **R44-01 P2: 정상/비활성 장치에 모두 명시 권한이 있으면 eligible-devices가409로 끝나 정상 장치도 선택할 수 없음**을 새 소유 MySQL에서 재현했습니다. 기존 전체 서버 **1차 704통과·3환경실패·12미설정생략,535.01초 → 보완 관련 23통과·실패/생략0,13.54초**와 별도 독립 회귀 **1실패,3.10초**를 구분합니다. C# 사용자 관리14시나리오/46검사·공개 벡터16검사·Release 경고0/오류0은 통과했습니다. 코드 병합/공유007적용·물리 등록 완료로 표시하지 않습니다. 아래 과거 PR40/41 승인과 신규44 판정은 별개입니다.

[상세 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr44-review-20261020)를 따릅니다.

<a id="member2-query-session-current"></a>
## 최신 작업 — 수신된 1·4·5번 규약의 2번 적용, 2026-10-19

3번 추가 합의가 필요한 기능을 제외하고 업무 조회·준비의 로그인 수명을 맞췄습니다. 같은 캡처 세션으로 목록/상세·준비·등록 workflow를 사용하고, 로그아웃·인증 만료·계정 교체 때 이전 자료를 지웁니다. 새 로그인은 명시 목록 재조회로 연결하고 준비 정보도 함께 갱신합니다. [회신별 적용/협의사항](공통규약.md#member2-replies-implementation)과 [변경 파일·실제 검사·미실행](업무_구현검증.md#member2-query-session-validation)을 확인합니다.

1·4·5번 통합 회신은 수신 완료이며 새 최초 회신을 요구하지 않습니다. 수정/재배정·마감 연장·제출/파일/이벤트,협업 조회/알림 정책·장치/근태 통합은 각 담당과의 협의사항입니다. 기존8개 문서에 취합하며 다른 담당 제품 코드는 수정하지 않았습니다.

## 최신 — 4번 회신 대조·1번 PR43 수신, 2026-10-19

4번24개 통합 답변과1번 후속14항목을 대조했고 문서PR43은 차단 문제 없이 LOG `ad75eed`에 병합했습니다. [검토·기존 타입 정정/남은 정책](공통규약.md#member4-reply-technical-review)을 확인합니다. assignment_revision은 기존 JSON정수이며 due_revision/협업 권한·공지/기한 알림 수치·운영 정책은 협의사항입니다.5번PR41과21답변은 이미 수신됐고 실제 서버/근태/MainForm 연결은 후속입니다. 이번 작업은 문서/정적 대조이며 제품 테스트를 새로 실행하지 않았습니다. 추가 회신 PR/새MD를 요구하지 않습니다.

> **2026-10-19 · 1번의 전체 잔여 협업 통합 회신안** — [14개 세부키 답변](../1번/2026-10-19_공통규약_사전협의요청.md#member1-reply-20261019). 공통 인증/통신·DB/운영·웹/파일·장치/MainForm 접점의 유지 기준·현재 제안·제공물·담당/결정 조건을 한 번에 정리했다. PR40/41 병합·1/4/5번 통합회신 수신을 반영하고,4번X02 배정revision 타입·5번 실제감지서비스/보고된지문요구의 연결 경계를 대조했다.  공유 환경·3번 최종 모델·4/5번 후속 정책/실제 통합은 협의사항이다. 사용자 요청에 따른 LOG 검토 PR이며 상대 수신/정책 합의·구현/병합 완료를 대신 선언하지 않는다. 기존 질문/팀장 규약 원문은 아래에 보존한다.

## 최신 검토 — PR40·41 코드 / PR42 회신 병합, 2026-10-19

[PR40](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/40) 1번 공통 지원은 alpha `7073817`, [PR41](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/41) 5번 독립 감지 서비스/공유 view는 alpha `763f11e`에 검토 후 병합했습니다. 새 차단 수정 요청은 없습니다. 직접 검증은40번 Python107·C#31,41번 C#294/12시나리오·Python30, 결합 Release 경고0/오류0이며 실제 병합 tree와 일치합니다. [PR42](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/42) 4번24개 통합 회신은 기존2문서/원문·링크/앵커를 검토해 LOG `2e6a439`에 병합했습니다. 문서 검토 때문에 제품/DB 시험을 추가 실행한 것은 아닙니다.

40번은 독립 읽기 점검/바이트 초안/화면 요청 수명 도구이고41번은 소유자가 주입할 서비스 준비입니다. 실제 본인 확인 서버·근태 저장·출근~퇴근 MainForm 연결·실장비/공유DB/두PC는 아직 구현/검증 대상입니다. 회신 수신을 제안 정책의 확정으로 확대하지 않습니다. 아래의 이전 대기/미연결은 해당 시점 기록입니다.

[검토·실행/미실행 상세](PR_검토기록.md#pr40-pr41-pr42-review)를 기준으로 확인합니다.

<!-- once-reply-navigation:start -->
## 전체 잔여 협업 — 2번 1회 통합 회신

[팀장 전체 잔여 대조표](../2번/공통규약.md#whole-remainder-once-reply)와 [2번 통합 질문서](2026-10-17_2번_영역수정_협업규약.md#member2-once-reply)의 전 항목을 확인합니다. 현재 수용된 규약은 전제로 두고 남은 정책·API/DB/UI·이벤트/알림·웹·운영/시험 접점을 한 묶음으로 답합니다. **텍스트 답변 1회를 팀장에게 전달 → 팀장이 기존 LOG에 취합**하며 협의 회신용 새 MD·브랜치·추가 PR은 필요 없습니다. 아직 결정하지 못한 내용도 현재 제안·이유·관련 담당/조건을 포함해 같은 답변에 씁니다.
<!-- once-reply-navigation:end -->

<!-- team-collaboration-current:start -->
<a id="member2-current"></a>

## 2번 현재 협업 현황 — 2026-10-19

현재2번은 목록·상세·신규 등록·수락·재작업의 기본off 개발 연결까지 구현했고, 남은 업무 수정/담당자·관리자 변경과 마감 연장은 **3번과 협의 후 결정**입니다. 원본LOG에 이 정책의 새 확정 회신은 없습니다. [전원 현황과 다음 순서](공통규약.md#team-collaboration-current), [2번 제공 접점/할 일](공통규약.md#collab-member2), [공통 회신 양식](공통규약.md#collab-reply)을 기준으로 이어갑니다.

우선3번의 상태별 수정/재배정·연장 회신표와1번의 공유 환경 준비 계획을 병행해서 받습니다. 실제3번 API/모델 게시 후2번 성공 재조회·과거 자료 보존/경합을 대조하고, 합의된 범위부터 수정→담당자/관리자 변경→연장을 연결합니다. 정책/코드가 준비되면 실제 공유 역할로 전체 업무 흐름을 검증합니다. 제출/검토·파일·이력은3번 담당입니다.

1번 공통,3번 결과물,4번 공지/채팅,5번 장치/근태의 대표 README와 협업 문서도 같은 수신·협의·구현 경계로 최신화했습니다. 원문/앵커·LOG/2번8개 구조를 보존하며 아래 과거 ‘미연결/대기’는 작성 당시 기록입니다. 문서 변경만으로 새 정책·실제 통합 완료를 선언하지 않습니다.
<!-- team-collaboration-current:end -->

## 최신 — PR38 R38-01 해결·병합, 2026-10-19

[PR38](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/38)의4번 수정본8b57ed1을 직접 재검증해 **R38-01 해결·새 차단 문제 없음**으로 alpha `8cfdfea`에 병합했습니다. 소유 MySQL75통과와 이전 실패 재현1통과, 검증/병합 tree 일치를 확인했습니다. 웹 공지는 기존notice opt-in 개발 서버에서 목록·상세·관리자 작성/삭제가 연결됩니다. 팝업/확인·실제 첨부·마감 알림·공유 적용은 별도이며 닫힌PR35는 승인 근거가 아닙니다. 아래 미병합/실패는 당시 기록입니다.

## 최신 PR 검토 — PR38 수정 요청·PR39 병합 확인, 2026-10-19

코드 [PR38](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/38)은 **R38-01 P2: 2페이지 선택→조회 시 page 누락으로 다른 상세 표시**를 실제 MySQL에서 재현해 미병합입니다. 기존65통과·별도 회귀1실패입니다. 문서 [PR39](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/39)는 검토 도중 LOG `754a6ea`에 병합된 상태를 확인했고 독립 문서 검증에서도 차단 문제를 찾지 못했습니다. 1번이 회원/장치 본인 확인 서버·DB/MainForm,5번이Pi/PC 중계·감지/근태를 작성한다는 역할 회신이며5번 확인·제품 구현은 대기입니다. [4번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-19_PR38_웹공지_페이지선택_수정요청.md)을 따릅니다. PR35는 CLOSED·미병합을 유지합니다.

> **2026-10-19 · 팀장 최신 처리: 잘못된 PR35 닫힘 / 문서 PR37 수신**
> 사용자 지시로 [PR35](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/35)는 **CLOSED·미병합** 처리했습니다. #35의 기존 요청을 승인된 정책·공통 파일 수정·실행 근거로 사용하지 않습니다. [PR37](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/37)은 1번의 기술 의견/협의 자료로 수신·병합했으며, WEB-N4 내용은 닫힌 요청의 작성 당시 검토 의견으로 보존합니다. 웹 작업의 유효 요청·범위는 별도로 확인해야 합니다. 회원/장치·근무 수명안과 남은 정책도 공동 합의·제품 구현 완료로 확대하지 않습니다.

> **2026-10-19 · 1번 수신 요청 후속 회신**
> [3번 로컬 실행 회신](../1번/2026-10-19_3번_로컬MySQL_조회환경_준비회신.md) · [2번 시작 API 회신](../1번/2026-10-19_수락_재작업_API_기술수용회신.md) · [4번 C08/C09 회신](../1번/2026-10-19_C08수신_C09공지_기술검토회신.md) — KST 수락/재작업 날짜 결정을 수신했고 경로/action·5필드·오류·등록 경계를1번 기술 범위에서 수용했습니다. 실제2번 API/3번 접점·공유 적용은 별도입니다.

> **2026-10-19 · 1번 공통규약 사전 협의 요청** — [요청서](../1번/2026-10-19_공통규약_사전협의요청.md)
> 2번 확인: C01 공유 적용, C02 상태별 수정/재배정, C03 검토자, C04·C06 제출/이력 접점, C07 마감/연장. 최신 등록 화면 연결을 반영했습니다. **회신 대기이며 새 규약 확정·구현 완료가 아닙니다.**

# 2번 업무·상태·마감 — 현재 안내

## 최신 연결과 담당별 회신 — 2026-10-19

1번 시작 API 기술 수용을 수신한 뒤 2번의 수락·재작업 **개발 서버와 명시 실행 UI를 연결**했습니다. 조회/등록과 함께 기본 off이며 실제 공유 서버·직원/관리자 계정은 준비 전입니다. [변경 파일·실제 검증·미실행](업무_구현검증.md)을 먼저 확인합니다. 아래 순수 전이 계획·대상 확인만 있던 시점의 기록은 당시 근거로 보존합니다.

전달할 남은 내용은 한 문서의 [1번](공통규약.md#collab-member1) · [3번](공통규약.md#collab-member3) · [4번](공통규약.md#collab-member4) · [5번](공통규약.md#collab-member5)으로 나눴고 [공통 회신 양식](공통규약.md#collab-reply)을 붙였습니다. 기존 C/W1/W3/N4/D5 번호를 유지하며 이미 수용된 시작 경로/action·5필드 계약은 다시 묻지 않습니다. 업무 수정·담당자 변경·마감 연장은3번 협의 후 결정이고, 모든 미확정은 협의사항입니다.

문서 PR37·39는 기술 회신과 분담 정정을 수신한 상태입니다. 1번이 서버 검증/DB·키·MainForm 연결, 5번이 Pi/PC 중계·감지 서비스·근태 화면을 작성하는 방향이며5번 확인·실제 구현은 별도입니다. 닫힌 #35의 WEB-N4는 과거 기술 의견이고 유효 구현 요청이 아닙니다. 타 담당 코드·공통 파일 수정 승인으로 사용하지 않습니다. LOG/2번은8개 문서를 유지합니다.

## 잔여 전체 진행 — 2026-10-19

2번 잔여를 [하나의 순서/협의사항 목록](공통규약.md#잔여-전체-작업의-진행-기준)으로 정리했습니다. 수락·재작업의 확정 상태/KST·버전 충돌/증가·최초 수락 기록 보존을 순수 불변 계획으로 준비했고 실제 저장은 연결하지 않았습니다. 새96·기존39/27 서버 검사와 WinForms149·Release 경고/오류0, 기본/최소 폼 캡처를 확인했습니다. [변경·검증·미실행](업무_구현검증.md#최신--잔여-순차-정리와-서버-내부-전이-계획-2026-10-19)을 따릅니다. 업무 수정·담당자 변경과 마감 연장은 팀장 답변대로 **3번과 협의 후 결정**입니다. 모든 미확정 내용은 협의사항으로 남기며 다른 담당의 코드와 미정 계약을 대신 구현하지 않았습니다.2번 전체 기능 완료로 표시하지 않습니다.

## 최신 2번 회신 — 3번 실행 환경 요청, 2026-10-19

팀장 확인으로 **공유 검증 서버와 직원·관리자 계정은 아직 준비 전**입니다. [3번에게 전달할 항목별 회신·다음 담당/순서](공통규약.md#3번-실행-환경-요청-회신)를 기존 문서에 통합했습니다. 조회/등록 코드와 실제 공유 환경·ReviewsControl 연결을 구분합니다.2번은 기존 설정으로 업무 조회 선행 조건만 읽는 점검 명령을 추가했고 DDL·seed·계정 생성·서버 시작은 하지 않습니다. [실제 검사·미실행](업무_구현검증.md#최신--3번-실행-환경-요청-회신과-읽기-전용-준비-점검-2026-10-19)을 확인합니다. LOG/2번8개 구조와 다른 채팅의 PR 검토 범위는 유지합니다.

## 최신 독립 재검토 — PR23·32·33 병합, 2026-10-19

활동 상태 코드 [PR23](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/23)은 head7af259d의 R23-01~03 해결을 확인해 alpha `5d9602d`로 병합했습니다. 문서 #32는 LOG `54aeae5`, #33은 PR `e4955b7`로 병합했습니다. 수정된 원본 C#264검증/11시나리오·Python30개·독립 지연 검사·Release 경고/오류0, 검증본/병합 tree 일치를 직접 확인했습니다. [현재 검토·실행/미실행](PR_검토기록.md)과 [5번 해결 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/5번/2026-10-19_PR23_과거프레임_지연검사_수정요청.md)을 따릅니다. 실제 장비/공유 DB·회원/근태 저장·출근~퇴근 백그라운드 감지와 C10/C11/D5 합의는 별도입니다. 아래 미병합/재검토 대기는 과거 회차이며, 다른 채팅의 2번 진행 작업과 LOG/2번8개를 유지합니다.

## 최신 2번 후속 — 입력 준비 수명·시작 날짜, 2026-10-19

등록/수정·연장 준비창의 이전 로그인 확인값 노출을 보완했습니다. 접속 변경·취소·종료 직후 확인값을 반환하지 않고 이전 입력·버튼을 정리합니다. 새43검사/8시나리오와 기존171/87/79/149 회귀, Release 경고0·오류0을 확인했습니다. 실제 업무 HTTP/DB 쓰기를 추가한 변경은 아닙니다.

팀장은 수락·재작업 시작을 **KST 시작일00:00부터 마감일이 끝날 때까지** 허용하는 기준을 직접 확정했습니다. 서버 순수 날짜 판정39검사는 통과했으며 실제 저장 API는 아직 없습니다. [공통규약의 구체 확인안](공통규약.md#w1-0304w3-01020406--다음-저장-연결의-구체-확인안)에서1번의 경로/action·결과·오류 확인과3번의 보존/조회 접점을 나눴습니다. 제출·연장 날짜는 별도 합의입니다. PR 검토는 다른 채팅에서 진행하며 이번 작업은2번 구현/협업 문서만 다룹니다. [실행·미실행 상세](업무_구현검증.md)를 확인합니다.

## 최신 독립 재검토 — 2026-10-19

문서 #30·#31을 각각 LOG·PR에 수신·병합했습니다. 코드 #23 head8191b43은 R23-01과 R23-02 동작을 확인했지만 원본 C# 회귀의 만료 안내 닫기가 실패해 **R23-03 검사 안정성 수정 요청·미병합**입니다. 안내 닫기를 조정한 검토용 복사본260/11 통과, 원본 실패, Python30 통과·Release 경고/오류0을 구분합니다. [검토 기록](PR_검토기록.md)과 [5번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/5번/2026-10-19_PR23_과거프레임_지연검사_수정요청.md)을 따릅니다. 실제 장비/공유 DB·근태 통합은 미완료입니다. 아래 이전 검토·작성자 게시 상태는 당시 기록으로 보존합니다.

최신화: 2026-10-19 · 담당/팀장: 사용자 본인

## 먼저 볼 문서

| 문서 | 내용 |
|---|---|
| [공통규약·협의 회신](공통규약.md) | 확정 기준·개발 계약, 담당별 남은 협의사항·수신 상태와 단일 회신 양식 |
| [업무 구현·검증](업무_구현검증.md) | 실제 조회·신규 등록·수락/재작업 개발 연결, 회차별 테스트와 미실행 |
| [PR 검토 기록](PR_검토기록.md) | 병합/미병합·수정 요청·작성자 보고/독립 검증 구분 |
| [2번 영역 협업 기준](2026-10-17_2번_영역수정_협업규약.md) | 다른 담당이 2번 파일·조회/화면 접점을 변경할 때 확인할 경계 |
| [지문 용도 기준](2026-10-17_지문센서_원격근무_본인인증_용도변경.md) | 로그인 후 본인 확인·재석 보조·업무 인증 조건의 경계 |
| [3번 재작업 확정 회신](2026-10-19_3번_재작업상태_확정_및_연결회신.md) | 반려완료→재작업 시작→진행중→재제출 성공→제출완료와 담당 분리 |
| [PR19·20 원 검토 기록](2026-10-19_PR19_PR20_장치시연_검토_병합.md) | 외부 전달 링크를 유지하는 당시 독립 검토 원문 |

## 현재 상태

- 기본 인증·HTTP·DB·오류·권한/버전·요청 결과 지원은 원본에 있습니다. 기존 지원을 재사용하며 공통 기능을 대신 바꾸지 않습니다.
- 2번은 실제 목록·상세 조회와 입력/후보 준비, 등록자 필수 관리자를 포함한 개발용 신규 등록 화면/API를 연결했습니다. 입력 확인 후 명시적 등록, 불명확 결과 확인·같은 키 재전송, 성공 후 범위/필터 유지 재조회를 검증했습니다. 서버 기본 off·공유 미적용입니다. 신규 등록 초기값/원자 저장·최소 결과·새 개발 DB DDL은1번 기술 수용을 받았습니다.
- 수락/재작업 대상은 기존 GET으로 현재 본인 배정·상태·버전을 확인하고 명시적 CheckedTarget을 보관합니다. 대상 확인은 읽기이며 이후 사용자의 명시 실행에서만 키/POST를 사용합니다. 서버가 현재 본인·상태·배정/업무 버전·KST·이상 자료를 다시 검사합니다. 준비값을 실행 허용이나 저장 완료로 표시하지 않습니다.
- 수락·재작업의 KST는 팀장 확정, W1-03/04 경로/action·최소 응답·오류·등록 경계는1번 기술 수용 수신·2번 개발 연결입니다. 3번 실제 제출/판단·회차/과거 파일 보존·최신 재조회·동시성 통합은 미실행이며 기존 W3 번호로 이어갑니다. 제출·연장 날짜는 협의사항입니다.
- 3번 재작업 흐름 수신을 유지합니다. 현재 병합/미병합·검토 상태는 [PR 기록](PR_검토기록.md)을 따르며 과거 R23-02/03 미병합 기록을 최신 상태로 읽지 않습니다.
- 5번 활동 시연과 PR37·39의 회원/장치·수명 기술안을 수신했습니다. 실제 회원 신뢰·근태 저장·근무 수명 통합과 장비 정확도 검증은 별도이며2번 업무에 장치 조건을 추가하지 않습니다.
- 공통규약의 현재 기준과 제안·담당자 합의·기술 검토·실제 적용을 구분합니다. 공통 버전 검사 도구가 있다는 사실이 정식 DDL 적용을 뜻하지 않습니다.

## 문서 정리 기준

기존 42개 Markdown을 **8개**로 정리했습니다. 같은 목적의 요청·회신과 구현/검증·PR 기록은 주제별 문서에 통합했습니다. 외부에서 사용 중인 협업·지문·재작업·장치 검토 대표 경로 4개는 유지합니다. 기존 날짜형 문서를 새로 계속 늘리지 않고 주제 문서에 현재 상태와 회차를 추가합니다.

과거 원문은 통합 전 Git 커밋의 고정 링크와 SHA256으로 보존했습니다. 아래 이동표로 옛 파일을 찾을 수 있습니다. 과거의 미연결/대기·PR 상태·검증 숫자를 현재 완료 상태로 읽거나 다른 실행과 합산하지 않습니다.

## 문서 이동표

<details>
<summary>기존 42개 파일의 새 위치·작성 당시 원문 보기</summary>

| 기존 파일 | 현재 위치 | 보존 원문 |
|---|---|---|
| `2026-10-16_1번_공통규약_회신정리.md` | [이동](공통규약.md#r001) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_1%EB%B2%88_%EA%B3%B5%ED%86%B5%EA%B7%9C%EC%95%BD_%ED%9A%8C%EC%8B%A0%EC%A0%95%EB%A6%AC.md) |
| `2026-10-16_1번_담당_공통규약_확인요청.md` | [이동](공통규약.md#r002) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_1%EB%B2%88_%EB%8B%B4%EB%8B%B9_%EA%B3%B5%ED%86%B5%EA%B7%9C%EC%95%BD_%ED%99%95%EC%9D%B8%EC%9A%94%EC%B2%AD.md) |
| `2026-10-16_PR3_병합_및_수정요청_분리.md` | [이동](PR_검토기록.md#r003) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_PR3_%EB%B3%91%ED%95%A9_%EB%B0%8F_%EC%88%98%EC%A0%95%EC%9A%94%EC%B2%AD_%EB%B6%84%EB%A6%AC.md) |
| `2026-10-16_PR4_문서병합_상태갱신.md` | [이동](PR_검토기록.md#r004) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_PR4_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9_%EC%83%81%ED%83%9C%EA%B0%B1%EC%8B%A0.md) |
| `2026-10-16_PR검토_병합후_1번전달사항.md` | [이동](PR_검토기록.md#r005) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_PR%EA%B2%80%ED%86%A0_%EB%B3%91%ED%95%A9%ED%9B%84_1%EB%B2%88%EC%A0%84%EB%8B%AC%EC%82%AC%ED%95%AD.md) |
| `2026-10-16_업무목록_상세_조회연결_준비.md` | [이동](업무_구현검증.md#r006) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_%EC%97%85%EB%AC%B4%EB%AA%A9%EB%A1%9D_%EC%83%81%EC%84%B8_%EC%A1%B0%ED%9A%8C%EC%97%B0%EA%B2%B0_%EC%A4%80%EB%B9%84.md) |
| `2026-10-16_업무조회_인터페이스_DTO_전달.md` | [이동](업무_구현검증.md#r007) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-16_%EC%97%85%EB%AC%B4%EC%A1%B0%ED%9A%8C_%EC%9D%B8%ED%84%B0%ED%8E%98%EC%9D%B4%EC%8A%A4_DTO_%EC%A0%84%EB%8B%AC.md) |
| `2026-10-17_002업무DDL_초안_검증.md` | [이동](업무_구현검증.md#r008) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_002%EC%97%85%EB%AC%B4DDL_%EC%B4%88%EC%95%88_%EA%B2%80%EC%A6%9D.md) |
| `2026-10-17_1번_W1_N4_회신수신_PR12_병합.md` | [이동](PR_검토기록.md#r009) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88_W1_N4_%ED%9A%8C%EC%8B%A0%EC%88%98%EC%8B%A0_PR12_%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_1번_쓰기공통지원_구현전달.md` | [이동](공통규약.md#r010) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88_%EC%93%B0%EA%B8%B0%EA%B3%B5%ED%86%B5%EC%A7%80%EC%9B%90_%EA%B5%AC%ED%98%84%EC%A0%84%EB%8B%AC.md) |
| `2026-10-17_1번_업무쓰기_공통규약_확인요청.md` | [이동](공통규약.md#r011) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88_%EC%97%85%EB%AC%B4%EC%93%B0%EA%B8%B0_%EA%B3%B5%ED%86%B5%EA%B7%9C%EC%95%BD_%ED%99%95%EC%9D%B8%EC%9A%94%EC%B2%AD.md) |
| `2026-10-17_1번_업무조회_API_DB_합의요청.md` | [이동](공통규약.md#r012) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88_%EC%97%85%EB%AC%B4%EC%A1%B0%ED%9A%8C_API_DB_%ED%95%A9%EC%9D%98%EC%9A%94%EC%B2%AD.md) |
| `2026-10-17_1번회신_수신확인_및_조회규약반영.md` | [이동](공통규약.md#r013) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_1%EB%B2%88%ED%9A%8C%EC%8B%A0_%EC%88%98%EC%8B%A0%ED%99%95%EC%9D%B8_%EB%B0%8F_%EC%A1%B0%ED%9A%8C%EA%B7%9C%EC%95%BD%EB%B0%98%EC%98%81.md) |
| `2026-10-17_2번_영역수정_협업규약.md` | [이동](2026-10-17_2번_영역수정_협업규약.md) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_2%EB%B2%88_%EC%98%81%EC%97%AD%EC%88%98%EC%A0%95_%ED%98%91%EC%97%85%EA%B7%9C%EC%95%BD.md) |
| `2026-10-17_3번_W3_회신수신_및_조회연결안내.md` | [이동](공통규약.md#r015) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_3%EB%B2%88_W3_%ED%9A%8C%EC%8B%A0%EC%88%98%EC%8B%A0_%EB%B0%8F_%EC%A1%B0%ED%9A%8C%EC%97%B0%EA%B2%B0%EC%95%88%EB%82%B4.md) |
| `2026-10-17_3번_수락상태_마감_연결규약_확인요청.md` | [이동](공통규약.md#r016) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_3%EB%B2%88_%EC%88%98%EB%9D%BD%EC%83%81%ED%83%9C_%EB%A7%88%EA%B0%90_%EC%97%B0%EA%B2%B0%EA%B7%9C%EC%95%BD_%ED%99%95%EC%9D%B8%EC%9A%94%EC%B2%AD.md) |
| `2026-10-17_3번_업무상태_제출검토_연결합의요청.md` | [이동](공통규약.md#r017) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_3%EB%B2%88_%EC%97%85%EB%AC%B4%EC%83%81%ED%83%9C_%EC%A0%9C%EC%B6%9C%EA%B2%80%ED%86%A0_%EC%97%B0%EA%B2%B0%ED%95%A9%EC%9D%98%EC%9A%94%EC%B2%AD.md) |
| `2026-10-17_4번_PR7_수정회신.md` | [이동](PR_검토기록.md#r018) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_4%EB%B2%88_PR7_%EC%88%98%EC%A0%95%ED%9A%8C%EC%8B%A0.md) |
| `2026-10-17_PR15_PR16_공통지원_검토_병합.md` | [이동](PR_검토기록.md#r019) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR15_PR16_%EA%B3%B5%ED%86%B5%EC%A7%80%EC%9B%90_%EA%B2%80%ED%86%A0_%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_PR1_PR2_재검토_및_PR5_PR6_병합.md` | [이동](PR_검토기록.md#r020) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR1_PR2_%EC%9E%AC%EA%B2%80%ED%86%A0_%EB%B0%8F_PR5_PR6_%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_PR7_Ready_재검토_PR13_PR14_병합.md` | [이동](PR_검토기록.md#r021) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR7_Ready_%EC%9E%AC%EA%B2%80%ED%86%A0_PR13_PR14_%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_PR7_검토_및_PR8_PR9_문서병합.md` | [이동](PR_검토기록.md#r022) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR7_%EA%B2%80%ED%86%A0_%EB%B0%8F_PR8_PR9_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_PR7_수정재검토_PR10_PR11_문서병합.md` | [이동](PR_검토기록.md#r023) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_PR7_%EC%88%98%EC%A0%95%EC%9E%AC%EA%B2%80%ED%86%A0_PR10_PR11_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9.md) |
| `2026-10-17_W1-05_상태CHECK_수정_MySQL_검증.md` | [이동](업무_구현검증.md#r024) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_W1-05_%EC%83%81%ED%83%9CCHECK_%EC%88%98%EC%A0%95_MySQL_%EA%B2%80%EC%A6%9D.md) |
| `2026-10-17_W1_후보_쓰기준비_화면연결.md` | [이동](업무_구현검증.md#r025) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_W1_%ED%9B%84%EB%B3%B4_%EC%93%B0%EA%B8%B0%EC%A4%80%EB%B9%84_%ED%99%94%EB%A9%B4%EC%97%B0%EA%B2%B0.md) |
| `2026-10-17_W1_후속_행동별API_사용자선택_제안.md` | [이동](공통규약.md#r026) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_W1_%ED%9B%84%EC%86%8D_%ED%96%89%EB%8F%99%EB%B3%84API_%EC%82%AC%EC%9A%A9%EC%9E%90%EC%84%A0%ED%83%9D_%EC%A0%9C%EC%95%88.md) |
| `2026-10-17_업무등록_수락_마감_입력준비.md` | [이동](업무_구현검증.md#r027) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%97%85%EB%AC%B4%EB%93%B1%EB%A1%9D_%EC%88%98%EB%9D%BD_%EB%A7%88%EA%B0%90_%EC%9E%85%EB%A0%A5%EC%A4%80%EB%B9%84.md) |
| `2026-10-17_업무목록_상세_실제조회_개발초안.md` | [이동](업무_구현검증.md#r028) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%97%85%EB%AC%B4%EB%AA%A9%EB%A1%9D_%EC%83%81%EC%84%B8_%EC%8B%A4%EC%A0%9C%EC%A1%B0%ED%9A%8C_%EA%B0%9C%EB%B0%9C%EC%B4%88%EC%95%88.md) |
| `2026-10-17_업무조회_API_DB_합의초안.md` | [이동](업무_구현검증.md#r029) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%97%85%EB%AC%B4%EC%A1%B0%ED%9A%8C_API_DB_%ED%95%A9%EC%9D%98%EC%B4%88%EC%95%88.md) |
| `2026-10-17_업무조회_MySQL_실제HTTP_실행기.md` | [이동](업무_구현검증.md#r030) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%97%85%EB%AC%B4%EC%A1%B0%ED%9A%8C_MySQL_%EC%8B%A4%EC%A0%9CHTTP_%EC%8B%A4%ED%96%89%EA%B8%B0.md) |
| `2026-10-17_업무조회_격리HTTP_통합검증_실행기.md` | [이동](업무_구현검증.md#r031) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%97%85%EB%AC%B4%EC%A1%B0%ED%9A%8C_%EA%B2%A9%EB%A6%ACHTTP_%ED%86%B5%ED%95%A9%EA%B2%80%EC%A6%9D_%EC%8B%A4%ED%96%89%EA%B8%B0.md) |
| `2026-10-17_입력확인_스냅샷_실제HTTP_준비창검증.md` | [이동](업무_구현검증.md#r032) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%9E%85%EB%A0%A5%ED%99%95%EC%9D%B8_%EC%8A%A4%EB%83%85%EC%83%B7_%EC%8B%A4%EC%A0%9CHTTP_%EC%A4%80%EB%B9%84%EC%B0%BD%EA%B2%80%EC%A6%9D.md) |
| `2026-10-17_조회_상태필터_응답검증_보강.md` | [이동](업무_구현검증.md#r033) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%A1%B0%ED%9A%8C_%EC%83%81%ED%83%9C%ED%95%84%ED%84%B0_%EC%9D%91%EB%8B%B5%EA%B2%80%EC%A6%9D_%EB%B3%B4%EA%B0%95.md) |
| `2026-10-17_조회_선택유지_상세재시도_취소처리.md` | [이동](업무_구현검증.md#r034) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%A1%B0%ED%9A%8C_%EC%84%A0%ED%83%9D%EC%9C%A0%EC%A7%80_%EC%83%81%EC%84%B8%EC%9E%AC%EC%8B%9C%EB%8F%84_%EC%B7%A8%EC%86%8C%EC%B2%98%EB%A6%AC.md) |
| `2026-10-17_지문센서_원격근무_본인인증_용도변경.md` | [이동](2026-10-17_지문센서_원격근무_본인인증_용도변경.md) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%EC%A7%80%EB%AC%B8%EC%84%BC%EC%84%9C_%EC%9B%90%EA%B2%A9%EA%B7%BC%EB%AC%B4_%EB%B3%B8%EC%9D%B8%EC%9D%B8%EC%A6%9D_%EC%9A%A9%EB%8F%84%EB%B3%80%EA%B2%BD.md) |
| `2026-10-17_합의전_진행가능범위_점검.md` | [이동](업무_구현검증.md#r036) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%ED%95%A9%EC%9D%98%EC%A0%84_%EC%A7%84%ED%96%89%EA%B0%80%EB%8A%A5%EB%B2%94%EC%9C%84_%EC%A0%90%EA%B2%80.md) |
| `2026-10-17_회신재확인_목록상세_변경안내.md` | [이동](업무_구현검증.md#r037) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-17_%ED%9A%8C%EC%8B%A0%EC%9E%AC%ED%99%95%EC%9D%B8_%EB%AA%A9%EB%A1%9D%EC%83%81%EC%84%B8_%EB%B3%80%EA%B2%BD%EC%95%88%EB%82%B4.md) |
| `2026-10-19_3번_재작업상태_확정_및_연결회신.md` | [이동](2026-10-19_3번_재작업상태_확정_및_연결회신.md) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-19_3%EB%B2%88_%EC%9E%AC%EC%9E%91%EC%97%85%EC%83%81%ED%83%9C_%ED%99%95%EC%A0%95_%EB%B0%8F_%EC%97%B0%EA%B2%B0%ED%9A%8C%EC%8B%A0.md) |
| `2026-10-19_PR19_PR20_장치시연_검토_병합.md` | [이동](2026-10-19_PR19_PR20_장치시연_검토_병합.md) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-19_PR19_PR20_%EC%9E%A5%EC%B9%98%EC%8B%9C%EC%97%B0_%EA%B2%80%ED%86%A0_%EB%B3%91%ED%95%A9.md) |
| `2026-10-19_PR21_채팅검토_PR22_문서병합.md` | [이동](PR_검토기록.md#r040) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/2026-10-19_PR21_%EC%B1%84%ED%8C%85%EA%B2%80%ED%86%A0_PR22_%EB%AC%B8%EC%84%9C%EB%B3%91%ED%95%A9.md) |
| `README.md` | [이동](README.md) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/README.md) |
| `기록목록.md` | [이동](README.md#문서-이동표) | [원문](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/251a8520d7daa6dbcc6c07e99d6a108748b1e0b5/2%EB%B2%88/%EA%B8%B0%EB%A1%9D%EB%AA%A9%EB%A1%9D.md) |

</details>

[전체 LOG](../README.md) · [alpha 코드](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/alpha)

## 1번 기술 회신 도착 — 2026-10-19

[공통규약 후속](공통규약.md#1번-복귀-기술-회신--2026-10-19) · [1번 판정·검증](../1번/2026-10-19_협업요청_기술검토_회신.md). 신규업무등록초기값/DB·채팅API/DDL·3번제출/파일/이력·5번회원장치접점에답변했습니다. 1번기술수용과공동회신/제품연결·공유적용은구분합니다.

현재 결과는 [PR 검토 기록](PR_검토기록.md)을 우선합니다. 아래는 문서 작성 당시 게시 안내입니다.

게시 확인: [DB SQL·검증 PR #25](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/25) · [협업 LOG PR #26](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/26) 생성 완료. 포크 alpha/LOG → 원본 alpha/LOG 검토 대기. 이번 보완의 원본 병합·각 담당 수신/구현·공유 적용은 아직 완료로 표시하지 않는다.
