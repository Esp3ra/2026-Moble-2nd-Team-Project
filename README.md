## 최신 — PR61 수정 3건 해결·병합 완료, 2026-10-21

[PR61 최종 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/61#issuecomment-6311013832)에서 R61-01/02/03을 모두 해결 확인하고 사용자/팀장의 지시에 따라 Draft→Ready 후 **ad46517eb8666ef4fea7d1ef779ab256f2433c10**으로 alpha에 병합했습니다. 최신739c510+head3ae607ee의 공식 마감105·기존 공지35·독립16 및 backoff/폐기 polling 대조·Release0/0을 직접 확인했습니다. 실제 merge tree **e8e4a43cb91ba24bfe2ac7be75142866c069edb3**는 검토본과 전체 diff0입니다.

[직접 검사·2번 최신 원본·후속 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr61-merged-20261021)를 따릅니다. 독립 UI/source port 제공 범위의 병합이며 실제 HTTP/확인 MySQL·DDL·90일 이력·MainForm/탐색·공유 환경 연결은 후속입니다. 아래 PR61 수정 대기/Draft·미병합은 최초 검토 당시 기록입니다.

## 최신 — PR61 검토: 수정 3건, Draft 유지, 2026-10-21

[PR61 통합 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/61#issuecomment-6311013832)에 **R61-01 업무 보기 401/403 뒤 기존 알림 유지, R61-02 늦은 업무 보기 응답의 최신 목록 덮어쓰기, R61-03 표시 전 재검사 연속 실패의 2분 고정**을 재현해 수정 요청했습니다. 모두 P2이며 Draft·미병합을 유지합니다. 최신 alpha388d50b+head5cca781의 결합 tree e79df7c0에서 공식 마감78·기존 공지35·Release0오류/0경고를 직접 확인했고 별도 대역으로 세 결함을 재현했습니다.

2번은 현재 잠금/실효 기한·변경 감사 원본을 재사용할 수 있으나, 알림용 최소 reader·실제 사건/초기 이력 근거·확인 저장·업무 탐색 조립은 후속입니다. [검사와 1·2번 기술 접점](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr61-review-20261021)을 따릅니다. A1~A6 채택 기준은 유지하며 실제 HTTP/MySQL·DDL·90일 이력·MainForm 양방향 조립/공유 환경 완료로 확대하지 않습니다.

## 최신 — PR57·58·60 재검토 후 모두 병합, 2026-10-21

| PR | 현재 판정 |
|---|---|
| [60](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/60#issuecomment-6310090050) | SQL100%rename/013경로239검사 통과, **e88da9d109ed7dda55872a65d06fc4d0c76a5157** 병합 |
| [58](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309407344) | R58-01 장치바인딩 수정 해결, 실제WinForms341·95·46/지문58·독립회귀 통과, **fef447280e4c6cd81298dad4fc63d20cf62bbd17** 병합 |
| [57](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308590391) | 기존5건 및R57-04 팝업중복 해결, UI113/독립회귀 통과, Draft→Ready 후 **ba9e312e0b55de7febf10a603f84e5da008fa617** 병합 |

세PR최종조합 Release0오류0경고, 실제alpha tree **0e12ec6d6e328500a4c2b29d1af8449542d004e7**가 검토본과일치합니다. [직접검사·이전결과재사용·작성자보고/미실행경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr57-58-60-merged-20261021)를따릅니다. 아래57/58 보류/Draft와013번호대기는 당시기록입니다. 실제공유서버/DB·설정/DDL/ACK·장비·작업폴더동기화는이번에실행하지않았습니다.

## 최종 — PR52·59 병합 / PR57·58 수정 대기, 2026-10-21

| PR | 현재 상태 |
|---|---|
| [52](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6287667210) | 기존R52-01/02/03 모두 해결. **merge cbda025ea9b2f83e8f63f5bca45bed74cec7ef77**, 최종tree68b87114 검토본과 일치 |
| [59](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/59#issuecomment-6309450248) | 업무 메뉴 실제 웹 연결 **merge f0430b7** 완료 |
| [57](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308590391) | 기존5건 해결, 새R57-04/P2 조회중 수동창/자동창 중복으로 Draft·미병합 |
| [58](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309407344) | R58-01/P2 확정거절 뒤 장치재선택/실제채널 불일치로 미병합. 후속6886aa1은 메뉴수신뿐이고 UI수정없음 |

[최종 직접 검사·재사용·담당 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr52-merged-final-20261021)를 따릅니다. 검토중 도착한52수정cc84c1c와 최신2번 업무웹96734bc까지 결합했습니다. 아래52 미업로드/보류/메뉴충돌 표시는 당시 기록이며 현재는 해결·병합 상태입니다. 코드 병합과 실제DDL/설정/배포ACK/쓰기503·작업폴더 동기화/공유환경·두PC 검증은 구분합니다.

## 최신 검토 — PR59 병합 / PR57·58 수정 요청 / PR52 대기, 2026-10-21

| PR | 현재 판정 |
|---|---|
| [59](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/59#issuecomment-6309450248) 업무 메뉴→실제 웹 | 소유MySQL33통과, **merge f0430b7b2ec33eeac69b0ac78b83ddad6af90695**, 실제tree cc5a7499가 검토본과 일치 |
| [57](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308590391) 협업 요청 수정본8613c947 | 기존 팀장3건·1번2건 해결 확인. 새 **R57-04/P2 조회 대기 중 수동 응답창이 열리면 같은 요청창1→2개**로 Draft·미병합 |
| [58](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309407344) 기존 회원 복구 진입2910cbb | 새 **R58-01/P2 A의 확정 NO_FREE 거절 후 B복구 선택 시 실제채널은A에 고정되어 진행 차단**, 미병합 |
| [52](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6287667210) 결과물 웹32fc711 | 새 코드 미업로드, R52-03/P2 첨부 참조 경합 수정대기 유지. 최신alpha와 web.py 메뉴 충돌도3번에 안내 |

[중앙 직접 검토·검사/범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr57-59-review-20261021)를 따릅니다. 59의1번 공통 메뉴 코드 접점은 완료이며 실제5000 재시작/배포·작업폴더 동기화·공유DB/옵션/DDL/ACK/쓰기503·실물 검증은 실행하지 않았습니다. 아래 과거 미해결·병합대기 표시는 당시 이력으로 보존합니다.

## 최신 재검토 — PR52 기존 두 오류 해결, 공용 첨부 경합 수정 필요, 2026-10-21

[PR52](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52) 새 head `32fc711`을 최신 alpha `44c85f3`과 결합한 tree `c80e19d0acac1ed3856e23bffc813c56d6d732b8`에서 전체 재검토했습니다. 기존 **R52-01/P1·R52-02/P2는 해결 확인**했고, **R52-03/P2: A 연결 해제와 새 B 연결이 겹치면 B 참조가 남아도 파일을 deleting으로 바꿔 다운로드404가 지속되는 문제**를 별도 실제 MySQL 검사로 재현하여 **미병합·수정 요청**입니다. [현재 회신·재현·수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6287667210), [중앙 직접 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr52-full-recheck-20261021)을 따릅니다. 아래 이전 PR52 수정 미업로드/두 UI 오류 미해결 표시는 당시 이력입니다.

직접 검사: 서버191·웹183·C#HTTP/WinForms41/115소스0오류0경고·취소연장258·공용첨부134·JS52와 추가세션12·기존 UI race·cold/JS구문/공백·Release0/0. 수치를 이전 회차와 합산하지 않습니다. 새 첨부 경합 재현/정상 순서 대조2는 결함 확인이며 정상기능134에 더하지 않습니다. 3번 제공부와1번 추가3테이블 검토·적용/2번 실제 업무TX·파일UI·웹 이동/갱신 소비는 구분합니다. 실제 공유DB/설정/DDL·배포ACK/쓰기503·작업 폴더는 변경하지 않았습니다.

## 최신 검토 — PR57 협업 Draft 수정 필요, 2026-10-21

[PR57](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57) headfe209ae를 alpha8ed25c6과 결합해 공식서버24·Directory25/6시나리오·Requests23·Release0/0을 확인했습니다. 별도검사에서 **R57-01/P2 취소 snapshot 혼합, R57-02/P2 상대 비활성화 시 종료 차단, R57-03/P2 미기록 확인 뒤409의 입력잠금**을 재현하여 미병합/Draft 유지입니다. [고정 코드·조건·수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308590391), [중앙 검토 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr57-review-20261021)를 따릅니다.

기존 B1~B7 채택은 유지합니다. 1번의 새 API/W1·미번호DDL/등록·MainForm 로그인수명,2·4번의 CURRENT취소/실제 중간이력 연속성, B4지연표시·B7상태이력 조회가 남아 있습니다. 협업10초/닫기/재로그인값은 로컬 제안이며 기한알림60초와 혼동하지 않습니다. [2번 기술 대조 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#pr57-member2-bridge-review-20261021)를 기록했습니다. 이번 검토는 제품/실제폴더·공유서버/DB·.env/DDL·장비를 변경하지 않았습니다.

## 최신 재검토 — PR55·PR56 병합 완료, PR52 코드 수정 대기, 2026-10-21

[PR55](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55) headbc1751d의 R55-04 단일 종료 시각 수정과 회원관리 연결 fixture 보완을 최신 alpha864a2a6에서 검증하고 **`b37fe3a38babe1553fc6d78e39e4b8c07ce0d822`**로 선행 병합했습니다. 공식8모듈239통과·독립 초 경계4통과·Release0/0이며 기존R55-01~04는 모두 해결 확인입니다. [최종 검토/범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55#issuecomment-6306967504)를 따릅니다.

이어서 [PR56](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56) head0bf93bc를 **`395d70d84172f332d9f47e162aa64b9bb5adaa57`**로 병합했습니다. C#중계540·전용WinForms31·회원관리 기본173/필수177(각 기존14시나리오46)·Python168통과/Linux2생략·실제서버/소유MySQL 어댑터2가 통과했습니다. 두 병합의 실제tree가 사전 검토tree와 일치합니다. [최종 검토/미실행 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56#issuecomment-6306967787), [중앙 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr55-56-merged-20261021)를 따릅니다.

실제 작업 폴더에는 병합된46경로만 백업 후 동기화했고 DOCX·실제설정은 보존했습니다. 실행 서버 재시작·실제010/011·옵션/서비스 전환·운영자close/reissue·Pi/센서·실물 등록은 실행하지 않았습니다. 코드 병합과 실제 서버의 새 경로404 해소/실물 검증은 별도입니다. PR52 head0a7a57d는 동일하여 기존R52-01/P1·R52-02/P2 코드 수정 대기를 유지합니다.

## 최신 재검토 — PR55 추가 수정·PR56 연결 검사 보완·PR52 대기, 2026-10-21

[PR55](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55) head0871b2c의 기존 R55-01~03은 해결 확인했습니다. 공식237검사와 별도9표적·Release0/0이 통과했지만, **R55-04 P2: 운영자 종료 중 초가 바뀌면 종료 성공 후 조회·재실행·재발급이 계속409가 되는 결함**을 독립 재현해 미병합입니다. [고정 코드/재현/수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55#issuecomment-6306292731)을 따릅니다.

새 [PR56](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56) head0bf93bc는 PR55와 결합해 Python168통과/Linux2생략·중계540·전용WinForms31·실제55 서버/소유MySQL 연결2·Release0/0을 확인했습니다. 다만 기준 alpha에서 통과하는 기존 회원관리 연결 검사가 새 physical-result 대역 누락으로 timeout하며, **PR55 수정·선행 병합과 원본 통합 검사 갱신/재실행 전까지 보류**합니다. [담당1·5 연결 보완 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56#issuecomment-6306293058)과 [중앙 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr55-56-recheck-20261021)를 따릅니다.

PR52 head0a7a57d는 코드 변경 없이1번의 공통3파일 수용 확인 댓글만 추가됐습니다. 기존 R52-01/P1 표시·실제 판단 대상 불일치와 R52-02/P2 버전충돌 후 재준비 경로 수정을 기다립니다. 이번에는 어떤 제품 PR도 병합하지 않았으며 실제 작업 폴더·공유 서버/DB·010/011·옵션·Pi/센서를 변경하지 않았습니다.

## 최신 PR 검토 — PR54 문서 병합·PR55 수정 필요·PR52 보류, 2026-10-21

[PR54](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54)는 기존6문서198행 추가를 검토해 LOG45d5f52에 병합했습니다. [PR55](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55) headffc421b는 공식180검사/Release0/0이 통과했으나 **R55-01 P1(기존 intent의 flagOFF 우회),R55-02 P2(RR snapshot의 늦은 채취 승인),R55-03 P2(terminal 결과와 충돌하는 첫 BLOB)**를 소유MySQL에서 별도 재현해 미병합입니다. [작성자 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55#issuecomment-6305522973)과[고정 SHA·검사·정리 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr55-review-20261021)을 따릅니다. PR52 head0a7a57d는 수정이 없어 기존P1/P2 보류를 유지합니다.

팀장이 채택한 **A1~A6·B1~B7 진행 기준**은 [통합 정책](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/f6ea8e7adad0a3c15dcf03ca97154a8fff781119/2번/공통규약.md#member4-policy-adopted-20261021)에 푸시하고[전원 통합 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305519634)으로 전달했습니다. 정책 채택과 코드 구현·실제 서버/장치 적용·통합 검증은 별도입니다.

## 최신 — PR51·53 병합, PR52 수정 필요, 2026-10-20

[PR51](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/51)의 취소·연장 성공 결과 조회/009 공통 검토를 `bb281e3`에, [PR53](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/53)의 협업 개발 환경 확인 요청 문서를 LOG `6664519`에 병합했습니다. [PR52](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52)는 기존 R52-01 P1(표시한 과거 결과물과 실제 판단 대상 불일치)을 독립 재현했고, R52-02 P2(버전 충돌 후 이전 요청만 반복하고 재준비 불가)를 추가 재현해 **OPEN·미병합**입니다. 공식 검사 통과와 이 JavaScript 결함을 구분합니다.

[고정 SHA·직접 검사·수정 기준](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr51-53-review-20261020)을 따릅니다. 공유 DB 적용·통합 ACK·실제 환경 준비는 별도이며, 회신용 새 MD/PR을 요구하지 않습니다.

## 최신 — PR52 검토 회차 불일치 수정 필요·미병합, 2026-10-20

[PR52](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52) head `0a7a57d`를 alpha `43f69c9` 기준으로 전체 검토했습니다. **R52-01 P1: 검토 context를 기다리는 동안 과거 회차를 선택하면 확인창의 과거 결과물과 실제 최신 제출물 판단 대상이 달라질 수 있음**을 독립 지연 대역으로 재현했습니다. 공식 서버185·웹177·C# HTTP41, cold/독립 인증·입력16, Release0/0은 통과했으나 이 JavaScript 경합을 포괄하지 않습니다. **OPEN·미병합**을 유지합니다. 1번 PR51은 이번 검토에서 제외했습니다.

[3번 수정 요청·재검증 기준](3번/2026-10-20_PR52_검토회차_불일치_수정요청.md)을 따릅니다. 기존 코드 PR에 수정하며 회신용 새 MD/PR은 요구하지 않습니다. 2번은 타 담당 제품 코드를 수정하지 않았고 취소·연장503과 실제 소비/공유 검증 미완료를 유지합니다.

## 최신 — PR49 코드·PR50 기술 문서 검토 병합, 2026-10-20

[PR49](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/49)의 회원 지문 등록 버튼/장치별 주소 연결은 `f428034`에, 이후 발견한 [PR50](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/50)의006/008 현행화·009 번호 예약/5번 접점 수용 기록은 `8cbdf8b`에 검토 후 병합했습니다. 50번은 기존Markdown2개뿐이며 실제009 SQL·신규 모델/flag/action·공유 적용을 추가하지 않습니다. 최신589 결합으로2번 ABC31파일을 보존했고, 실제/검증 tree `0fc6fae7e063baaad72faa9eb9c697a143014ff7`가 일치합니다. 병합 직후 열린PR은0건이었습니다.

[정적 대조·현재 준비지원·고정 SHA](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr50-review-20261020)를 따릅니다.

## 최신 검토 — PR49 생성 회원 지문 등록 버튼·주소 연결 병합, 2026-10-20

신규 [PR49](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/49) head `4825efd`를 alpha `73b17e0`와 결합 검토해 추가 차단 결함 없이 `f428034`에 병합했습니다. 실제/검증 tree `03098b4a645499c259c9afb4102b705da4a090bf`가 일치합니다. 생성 응답 회원ID·원래 관리자Session을 유지하고 명시 버튼으로47 등록창을 열며, 선택 장치의 Pi 주소 누락은 발급POST/claim 전에 차단합니다. 원본 연결112·기존회원46/14시나리오·중계114·등록창12·ApiClient125/25시나리오와 독립 수명/경합검사·Release0/0이 통과했습니다. 실제 장치/공유 환경 활성화는 별도입니다.

[고정 SHA·수명/채널 검사·남은 실물 연결](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr49-review-20261020)을 따릅니다.

## 최신 검토 — PR48 센서·OLED 공유 구성 병합, 2026-10-20

신규 [PR48](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/48) head `a4407f1`를 alpha `df2af6d`와 결합 검토해 추가 차단 결함 없이 `73b17e0`에 병합했습니다. 실제/검증 tree `a193994c2aa98bbb5a4e3d86bb082845b72c903f`가 일치합니다. 기존 단일 UART·센서 잠금·OLED를 빌려 명시 factory로 등록 runtime을 조립하며 **기본 실행은 등록 비활성**입니다. 전체Pi123개 중122통과/Windows POSIX권한1생략, 독립동시성3개 통과, Release0경고/0오류입니다. 45번 공지 삭제 정책과2번 결과 제출 연결·공통 코드는 보존합니다.

[고정 SHA·실제 검사·남은 연결](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr48-review-20261020)을 따릅니다.

## 최신 — 팀장 삭제 정책 확정·PR45 병합 완료, 2026-10-20

팀장이 본 채팅에서 **관리자가 공지를 삭제하면 해당 공지의 직원별 확인 기록도 함께 삭제**하도록 확정하고 문제없으면 병합하도록 지시했습니다. **삭제 감사 로그는 별도로 남깁니다.** [PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) head `c5dad3b`를 최신 alpha `2718601`와 결합해 추가 차단 문제 없음으로 확인했고, Draft를 해제하여 alpha `a554242`에 병합했습니다. 기존 보존 요구·로컬 선택/팀장 미확정·Draft/미병합은 아래의 과거 이력입니다. 공유DB 적용·팝업 옵션 활성화와 나머지 로그인 경계/주기 정책은 별도입니다.

[팀장 확정·최종 검증·실제 병합](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr45-merged-delete-policy-20261020)를 따릅니다.

## PR45 최신 추가분 — 삭제 정책 문서 정정·Draft 유지, 2026-10-20

[PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) 검토 마무리 중 head `c5dad3b`이 추가됐습니다. Markdown3개·SQL/시험 주석만 바뀌었고 실행 로직·DDL은 이전 검증본 `0dfa517`과 동일합니다. **4번 문서는 로컬 선택을 “공지 삭제 시 확인 기록도 함께 삭제”로 정정**했으므로, 아래 “보존 요구와 CASCADE 불일치”는 이전 요구 시점 기록입니다. 이 보고를 팀장의 공통 삭제 정책·최종008/공유 적용 승인으로 확대하지 않습니다. 최신 head는 충돌 없이 결합되며 **Draft·미병합을 유지**합니다.

[최신 고정 head·기능 동일성·문서 정정](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr45-policy-followup-20261020)를 따릅니다.

## PR45 후속 수정 확인 — 충돌·PID 경합 해결 / Draft 유지, 2026-10-20

[PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) head `0dfa517`에서 최신 alpha `14c1c80` 반영과 병합 충돌 해소, 공식 Activity 회귀의 PID 파일 읽기 보완을 확인했습니다. 공식 원본 전체 **297검사/12시나리오를2회 통과**, 공지35검사·Release 경고0/오류0입니다. R45-01·02 해결도 유지됩니다. **공지 삭제 후 확인 기록 보존 구조는 여전히 미구현이므로 Draft·미병합·008공유 적용 보류**입니다. 아래 과거 “alpha 충돌/공식 Activity PID 경합 실패”는 해당 head 시점 이력이며 최신 해결 상태와 구분합니다.

[최신 원본 실행·고정 SHA·보류 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr45-pid-recheck-20261020)를 따릅니다.

## 최신 재검토 — PR44·46·47 병합 / PR45 Draft 유지, 2026-10-20

[PR44](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/44) R44-01과 [PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) R45-01·02의 수정 업로드를 확인했습니다. **44번은 수정 재검증 후 병합, 45번은 기존 지적 해결·보존 구현 미완료로 Draft/미병합**입니다. 팀장의 이번 “46번은 수정됐으니 검토” 지시로 이전 보류를 해제하여 [PR46](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/46) 실제 제출·검토 코드를 검토했고, 신규 [PR47](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/47) 등록 중계도 검토했습니다. 44→47→46 순서로 alpha에 병합했으며 최종 코드 merge `1d2cb6b`/tree `0608974b29eb5996f67d34c14b63ae8fbf4fa51f`는 검증본과 일치합니다. 아래 이전 미해결·46보류·미병합은 작성 시점의 이력입니다.

[고정 SHA·직접 검증·남은 연결 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr44-47-recheck-20261020)를 확인합니다.

## 최신 — PR45 Draft 수정 요청 / PR46 사용자 보류, 2026-10-20

[PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) 최신 head `8f9e2d2`는 **Draft·미병합**이며 R45-01 P2(다른 DB의 같은 이름 부모를 참조한 FK도 준비 검사 통과), R45-02 P2(MainForm 새 popup 요청으로 기존 인증 종료 회귀 실패)를 확인했습니다. 서버 표적118통과와 별도 FK 독립1실패, 공지C#35·Release0/0을 구분합니다. 기존 활동 회귀 원본은 실패했고, 새 경로404만 추가한 검토 복사본은294/12통과입니다. 제출 원본의 통과로 대체하지 않습니다.

**PR46은 사용자의 '46번은 보류' 지시에 따라 검토·병합 보류 / OPEN·미병합 유지**입니다. 문서 답변/임시 시연 정보·정책을 수용하거나 제품 코드로 반영하지 않았습니다. PR44의 R44-01 수정 요청·미병합도 유지합니다.

[재현·수정/재검증 조건](4번/2026-10-20_PR45_FK대상DB_종료회귀_수정요청.md)에 실제 결과를 남겼습니다.

## 최신 검토 — PR44 수정 필요·미병합, 2026-10-20

[PR44](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/44) head `ba6a37a`를 alpha `6cb4d5d`와 결합해 검토했습니다. **R44-01 P2: 정상/비활성 장치에 모두 명시 권한이 있으면 eligible-devices가409로 끝나 정상 장치도 선택할 수 없음**을 새 소유 MySQL에서 재현했습니다. 기존 전체 서버 **1차 704통과·3환경실패·12미설정생략,535.01초 → 보완 관련 23통과·실패/생략0,13.54초**와 별도 독립 회귀 **1실패,3.10초**를 구분합니다. C# 사용자 관리14시나리오/46검사·공개 벡터16검사·Release 경고0/오류0은 통과했습니다. 코드 병합/공유007적용·물리 등록 완료로 표시하지 않습니다. 아래 과거 PR40/41 승인과 신규44 판정은 별개입니다.

[재현·수정 방향·재검증 조건](1번/2026-10-20_PR44_비활성장치_목록조회_수정요청.md)을 확인합니다.

## 최신 검토 — PR40·41 코드 / PR42 회신 병합, 2026-10-19

[PR40](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/40) 1번 공통 지원은 alpha `7073817`, [PR41](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/41) 5번 독립 감지 서비스/공유 view는 alpha `763f11e`에 검토 후 병합했습니다. 새 차단 수정 요청은 없습니다. 직접 검증은40번 Python107·C#31,41번 C#294/12시나리오·Python30, 결합 Release 경고0/오류0이며 실제 병합 tree와 일치합니다. [PR42](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/42) 4번24개 통합 회신은 기존2문서/원문·링크/앵커를 검토해 LOG `2e6a439`에 병합했습니다. 문서 검토 때문에 제품/DB 시험을 추가 실행한 것은 아닙니다.

40번은 독립 읽기 점검/바이트 초안/화면 요청 수명 도구이고41번은 소유자가 주입할 서비스 준비입니다. 실제 본인 확인 서버·근태 저장·출근~퇴근 MainForm 연결·실장비/공유DB/두PC는 아직 구현/검증 대상입니다. 회신 수신을 제안 정책의 확정으로 확대하지 않습니다. 아래의 이전 대기/미연결은 해당 시점 기록입니다.

[독립 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr40-pr41-pr42-review)에 근거를 남겼습니다. 추가 차단 수정 요청 파일은 만들지 않습니다.

## 최신 — PR38 R38-01 해결·병합, 2026-10-19

[PR38](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/38)의4번 수정본8b57ed1을 직접 재검증해 **R38-01 해결·새 차단 문제 없음**으로 alpha `8cfdfea`에 병합했습니다. 소유 MySQL75통과와 이전 실패 재현1통과, 검증/병합 tree 일치를 확인했습니다. 웹 공지는 기존notice opt-in 개발 서버에서 목록·상세·관리자 작성/삭제가 연결됩니다. 팝업/확인·실제 첨부·마감 알림·공유 적용은 별도이며 닫힌PR35는 승인 근거가 아닙니다. 아래 미병합/실패는 당시 기록입니다.

[4번 최종 검토](4번/2026-10-19_PR38_웹공지_페이지선택_수정요청.md)에 재현 전후와 실행/미실행을 구분했습니다.

## 최신 PR 검토 — PR38 수정 요청·PR39 병합 확인, 2026-10-19

코드 [PR38](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/38)은 **R38-01 P2: 2페이지 선택→조회 시 page 누락으로 다른 상세 표시**를 실제 MySQL에서 재현해 미병합입니다. 기존65통과·별도 회귀1실패입니다. 문서 [PR39](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/39)는 검토 도중 LOG `754a6ea`에 병합된 상태를 확인했고 독립 문서 검증에서도 차단 문제를 찾지 못했습니다. 1번이 회원/장치 본인 확인 서버·DB/MainForm,5번이Pi/PC 중계·감지/근태를 작성한다는 역할 회신이며5번 확인·제품 구현은 대기입니다. [4번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-19_PR38_웹공지_페이지선택_수정요청.md)을 따릅니다. PR35는 CLOSED·미병합을 유지합니다.

> **2026-10-19 · 팀장 최신 처리: 잘못된 PR35 닫힘 / 문서 PR37 수신**
> 사용자 지시로 [PR35](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/35)는 **CLOSED·미병합** 처리했습니다. #35의 기존 요청을 승인된 정책·공통 파일 수정·실행 근거로 사용하지 않습니다. [PR37](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/37)은 1번의 기술 의견/협의 자료로 수신·병합했으며, WEB-N4 내용은 닫힌 요청의 작성 당시 검토 의견으로 보존합니다. 웹 작업의 유효 요청·범위는 별도로 확인해야 합니다. 회원/장치·근무 수명안과 남은 정책도 공동 합의·제품 구현 완료로 확대하지 않습니다.
> [검토 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md)에 이번 문서 검증과 기존 작성자 실행 보고를 구분했습니다. 새 코드 수정 요청은 없습니다.

# PR 수정 요청

## 최신 검토자 결과 — PR23·32·33 병합, 2026-10-19

코드 #23 head7af259d의 **R23-01~03 해결·새 수정 요청 없음**을 확인해 alpha `5d9602d`로 병합했습니다. 관련 문서 #32(LOG `54aeae5`)·#33(PR `e4955b7`)도 수신·병합했습니다. 이전에 실패한 환경에서 수정된 원본 C#264검증/11시나리오·Python30개·독립 지연 검사·Release 경고/오류0을 확인했고 병합 tree도 검증본과 일치합니다. [5번 최종 검토](5번/2026-10-19_PR23_과거프레임_지연검사_수정요청.md)를 따릅니다. 실제 장비/공유 DB·회원/근태·백그라운드 통합은 별도입니다. 아래 과거 미병합/실패는 당시 기록입니다.

## 최신 독립 재검토 — 2026-10-19

문서 #30(LOG)·#31(PR)은 수신·병합했습니다. 코드 #23 head8191b43의 R23-01 지연 방어와 R23-02 종료 동작은 확인했습니다. 다만 원본 C# 회귀가 만료 안내 닫기에서 실패해 **R23-03 검사 안정성 P2 요청·코드 미병합**입니다. 안내 닫기만 조정한 검토용 복사본260/11 통과와 원본 실패를 구분합니다. [5번 최신 요청](5번/2026-10-19_PR23_과거프레임_지연검사_수정요청.md)을 따릅니다. 아래 이전 R23-02 미해결 표시는 과거 검토 시점입니다.

## 최신 검토자 확인 — 2026-10-19

코드 #25(DB 검증)·#21(채팅)은 alpha에, 문서 #26·#27·#29는 LOG에, 수정 회신 #28은 PR에 병합했습니다. #29의 공지 확장 정책은 제안 수신이며 새 수정 요청은 없습니다. [4번 R21-01~04](4번/2026-10-19_PR21_채팅저장_수정요청.md)는 해결 확인했습니다. 코드 #23은 **수정 필요·미병합**입니다. [5번 요청](5번/2026-10-19_PR23_과거프레임_지연검사_수정요청.md)에 기존 R23-01 해결과 신규 R23-02(자식 정리 중 인증 만료 시 부모 종료 취소) 재현·수정 방향·재검증을 구분했습니다.

독립 실제 테스트·작성자 보고·미실행 범위와 병합 SHA는 [LOG 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md)을 따릅니다. 문서 수신을 코드 승인·공유 DB 적용·실장비/근태 연동 완료로 확대하지 않습니다. 아래의 과거 상태와 수치는 해당 검토 시점 기록으로 보존합니다.

## 최신 재검토 — PR21 수정 확인·추가 요청, PR23 지연 오류, 2026-10-19

[4번 R21-04](4번/2026-10-19_PR21_채팅저장_수정요청.md)는 초기화 배치의 활성화 환경 누출, [5번 R23-01](5번/2026-10-19_PR23_과거프레임_지연검사_수정요청.md)은 지연된 과거 프레임을 현재 활동으로 표시하는 P2 수정 요청입니다. PR #21의 기존 세 건은 독립 수정 확인했으며 새 요청·Draft·README 충돌·공동 합의는 남아 있습니다. #23도 미병합입니다. 문서 #24만 README 양쪽 기록을 보존해 LOG `afd990b`로 병합했습니다. 아래 과거 상태는 당시 기록입니다.

## 현재 — 2번 문서 통합 참조 갱신, 2026-10-19

[팀장 공통규약·협의 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md)과 [2번 이동표](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/README.md)를 대표 기준으로 연결합니다. 기존 수정 요청·재검토 결과 본문은 보존하고 통합으로 이동한 문서 링크만 갱신했습니다. PR #21은 동일 head의 Draft·미병합이며 수정 수용 회신은 아직 없습니다.

## 현재 — PR #21 채팅 코드 수정 요청, 2026-10-19

[R21-01~03 수정 요청](4번/2026-10-19_PR21_채팅저장_수정요청.md)에서 같은 세션의 정보 갱신을 로그인 변경으로 오인하는 문제, 공통 초기화에 미확정 채팅 DDL이 포함되는 문제, 서버/C# 메시지 길이 불일치의 P2 3건을 확인합니다. 정확한 head `832478e`의 HTTP 대역·MySQL mock DDL 재현이며 실제 MySQL 저장 검증은 아닙니다. 코드 #21은 Draft·미병합입니다.

협의 문서 [PR #22](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/22)만 LOG `0339b29`로 병합했습니다. 수정 후 새 head 검토, 1번 API/DDL 회신, 저장 통합 검증과 Ready 확인이 남아 있습니다. 아래 과거 완료/대기 기록과 별개인 신규 요청입니다.

## 최신 검토자 확인 — PR #17·#18 병합

[공통 접속 범위·기능 계약 검토 결과](1번/2026-10-17_PR17_접속범위_기능계약_검토결과.md): 코드 #17은 alpha6f5f113, 문서 #18은 LOGdd75964로 병합했습니다. 검토 범위에 신규 수정 요청은 없으며 검토자 소유 MySQL168·접속125·빌드 경고/오류0을 확인했습니다. [2번의 후속 연결과 W3 남은 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#r025)를 따릅니다. 아래 과거 대기 상태는 작성 당시 기록으로 보존합니다.

## 최신 검토자 확인 — PR #15·#16 병합, 2026-10-17

[W1 공통 지원 검토 결과](1번/2026-10-17_PR15_W1_공통지원_검토결과.md): 코드 #15는 alpha `5fd99a8`, 문서 #16은 LOG `54c6cd3`로 병합했습니다. 이번 범위에서 신규 수정 요청은 없습니다. 공통 지원 수신 완료와 제품 업무 쓰기·W3 미정 정책·공유 DDL/버전/두PC 미완료를 구분합니다. [실제 검토·검증 상세](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#r019)를 따르며 아래 이전 대기/미구현 표현은 당시 기록입니다.

## 최신 검토자 확인 — PR #7·#13·#14 병합, 2026-10-17

**코드 [PR #7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/7)은 재검토 후 alpha에 병합했습니다.** 제출 head는 `d58064ccd30e7e718f94d5e5556d0f19c6afd88c`, 병합 커밋은 [733b47a](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/733b47afca123ed3b161e6d96b492a8afc6e163f)이며 GitHub 확인 시각은 **2026-10-17 16:42:08 KST**입니다. Ready·README 충돌·해당 head의 독립 재검토 조건은 해소됐습니다. 최종 alpha 문서 기준은 `f486758d119080511a4bf27e1b3b328d144fe530`입니다.

문서 [#13](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/13)은 [c38ba6f](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/c38ba6fa8e55c898878853100900320252669695)(16:36:44 KST)에 LOG로, [#14](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/14)는 [96a0574](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/96a05740e072a7193bd1f19c6736ba3bdf013a83)(16:36:48 KST)에 PR로 병합했습니다. [이번 독립 재검토·문서/코드 병합 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#r021)에서 작성자 보고와 검토자 실제 실행을 구분합니다.

검토자는 #7 head에서 MySQL 회귀 **110통과·2선택제외**, 실제 C#→TCP→raw003 MySQL **37검증 통과**를 확인했습니다. 이 결과는 아래 작성자 MySQL112 보고와 다른 실행이며 합산하지 않습니다. 2번의 [업무 조회 MySQL 실제 HTTP 실행기](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#r030)는 별도 변경/검증 기록입니다. 두 변경을 합친 최종 alpha `f486758`에서 업무 HTTP Release 빌드 **경고0·오류0**, 실제 C#→TCP→raw002 MySQL **직원43·관리자64검증 / 실행기301검사(권한회수11포함)**를 통과했습니다. 공지/채팅 WinForms 회귀는 명시적 대역 서비스로 **5시나리오·85검증**을 통과했으며 DB/네트워크 통합 검증과 구분합니다. 검증 중 소스133개와 기존 공유 MySQL 프로세스를 보존했고 소유 DB·서버·임시 디렉터리 정리를 확인했습니다. 실제 로컬 작업공간에는 변경23파일을 반영해 추적179파일 일치·DOCX7개 보존·Release **경고0·오류0**를 확인했습니다. 공유 DB·두 PC·정식 버전/DDL 적용·제품 업무 쓰기/제출/승인·장치/수동 전체 GUI는 미실행입니다.

**코드 병합과 예약003의 정식 SQL 승인·공통 실제/기대 버전 지원·공유 적용은 별도입니다.** schema version1·독립 기본off를 유지합니다. 정식002/003 적용 순서·공유 백업/복구·실제 두 PC·삭제/감사 보존과 팝업/확인 정책은 담당 합의/지원 후 진행합니다. 1번 W1의 공통 쓰기 지원과 3번 W3 회신 대기도 유지하며 제출·검토 기능을 대신 구현하지 않습니다.

아래의 Draft·미병합·충돌·재검토 대기 표현과 과거 테스트 수치는 작성 당시의 원문으로 보존합니다. 현재 병합 상태는 이 절을 기준으로 확인합니다.

## 4번 후속 — 최신 결합본·003 검토 요청, 2026-10-17

코드 #7 head [`d58064c`](https://github.com/zx234x/jang1/commit/d58064ccd30e7e718f94d5e5556d0f19c6afd88c)에 alpha `c56f3c87e446adb30309c46287cd7c4aa3cff273`의 업무 제약/입력 준비를 보존하고 README 충돌을 해소했다. N4 수용 반영·003 SQL/검증 추가 후 **새 head·SQL 재검토 대기**, 원본 미병합이다. [현재 처리 상태](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-17_PR7_공지DB_검토상태.md)·[실제 검증/남은 확인 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/4번/2026-10-17_PR7_1번회신반영_003DDL_검증.md)·[LOG 검토 자료](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG/4번)을 따른다. 아래 원문은 당시 기록으로 보존하며 Ready 여부는 GitHub #7의 현재 상태를 확인한다.

> **후속 alpha 기준:** [c56f3c8](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/c56f3c87e446adb30309c46287cd7c4aa3cff273)에 W1-05 상태CHECK 보완을 반영했습니다. 실제DB/주요검증은 MySQL, SQLite는 DEMO 종료 후 삭제 예정입니다. [2번 변경·검증 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#r024)을 참조합니다. #7과 새alpha의 충돌은 README1개이며 새업무 제약은 보존됐습니다. #12는697a8e3으로 병합, N4 ID/API 수용·R1/R2 해결 확인 상태이고 Draft/README충돌·결합재검증/Ready 확인은 남습니다.

`PR`은 검토에서 발견한 수정 요청과 재검토 조건을 담당 번호별로 관리하는 문서 브랜치입니다. 기준일은 **2026-10-17**입니다. 구현·변경 이력은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG)에 남기며, 이곳의 요청 문서에서 수정 상태를 확인합니다.


## 최신 후속 확인 — PR #12의 1번 N4 회신 수신

2026-10-17, [문서 PR #12](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/12)에서 [1번 N4-01~10 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_공지DB_API_DDL_협의회신.md)이 도착했습니다. **공지 ID·404·현재 API/DTO·독립 opt-in/기본 off는 1번 책임 범위에서 수용**됐습니다. 이 범위의 ‘1번 API/ID 회신 대기’는 해소됐습니다. 기존 head `c7282d1e9e332daf3996aeb00f9a1b20c6ad2e61`의 **R1·R2 해결 독립 확인은 유지**합니다.

| 구분 | 수신 후 현재 상태 | 남은 조건 |
|---|---|---|
| ID·오류 | N4-02/03: signed INTEGER/C# int, `1..2147483647`, 없는/범위 초과 공지는404 `NOTICE_NOT_FOUND` 수용 | 음수/문자열의 라우트 미매칭은 일반 HTTP_404일 수 있음. 업무 task_id BIGINT 계약은 변경 없음 |
| API·인증·등록 | 현재 목록/상세 GET·등록/삭제 POST·DTO/페이지·관리자 작성/삭제·로그인 직원 모든 부서 조회·공통 연결 재사용 수용. 공지 독립 opt-in/기본 off 유지 | 팝업/확인·추가 query 강화·실제 파일 전송까지 승인한 계약이 아님 |
| R1·R2 | 검토자가 새 MySQL RR/SQLite에서 해결 확인한 기존 판정 유지 | 새 결합 head에서 영향 범위·필요 회귀를 확인해야 함 |
| 코드 PR #7 | **Draft·미병합**. 최신 alpha의 루트 README 충돌 해소와 결합 재검증·새 head 및 Draft 해제/Ready 상태 확인 필요 | 회신 수신을 코드 승인·자동 병합 완료로 표시하지 않음 |
| DDL·버전 | `003_notices.mysql.sql`은 **기본 notices 테이블 번호만 예약**. 업무002와 현재version1 유지 | SQL·전후 영향·FK/default/index·실패/복구안은 4번 작성·1번 검토. 실제/기대 버전 지원은 1번 후속 구현. 번호 예약은 DDL 승인/실행이나 version3 확정이 아님 |
| 공유 활성화·공동 정책 | 공식 환경·백업/복구·실제 적용/명시적 활성화는 별도 후속 조건 | 삭제/보존·팝업 초기/복구·확인 기록/재확인 정책은 팀장·1·4번 공동 확인 필요 |

아래의 API/ID 회신 대기·작성자 보고·최초 오류 재현·독립 재검토는 각 작성 시점의 근거로 보존합니다. 최신 수신 상태는 이 절과 [2번 수신 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#r009)을 따릅니다. 공통 지원 구현·정식 DDL/버전·공유 DB 적용은 문서 수신과 별도 단계입니다. [W1 업무 쓰기 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_업무쓰기_공통규약_회신.md)도 별도로 수신됐으며 3번 상태/제출 합의를 대신하지 않습니다. 이번 수신 정리에서 프로그램·DB 테스트를 새로 실행한 것으로 기록하지 않습니다.

## 이전 병합 기록

| PR | 담당·내용 | 상태 |
|---|---|---|
| [#1](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/1) | 4번 공지·로컬 채팅 | 재검토 통과·alpha 병합 · [9b8d00c](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/9b8d00cebe3e8a2a5555e06bcc512a802cc8152b) |
| [#2](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/2) | 1번 공통 서버·DB·인증·통신 | R1~R3 재검토 통과·alpha 병합 · [d27213e](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/d27213eea7d703c7c325b48c0f21b89c7f91cdf6) |
| [#3](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/3) | 1번 협의·검증 문서 | LOG 병합 완료 · [6348997](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/6348997d78976ad5127116ccd606aaa36fc264b5) |
| [#4](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/4) | 4번의 타 담당 파일 수정 전달 문서 | LOG 병합 완료 · [831670e](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/831670e58df09d79b124d1cb0b19975bea6216b2) |
| [#5](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/5) | 1번 수정·검증 기록 | LOG 병합 완료 · [6ca6d8f](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/6ca6d8faafa12ff0774f93724bdb6c9957fc647b) |
| [#6](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/6) | 1번 수정 요청 회신 | PR 병합 완료 · [2e89714](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/2e89714c2f726950a732f2308c87d42964937522) |

2026-10-17에 기존 결함 4건을 재검토하고 코드 PR #1·#2를 병합했습니다. 당시 통합 alpha는 `9b8d00c`입니다. 앞선 작성자 수정 검증의 원본 alpha는 [`8a652ad`](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/8a652adaba4a8471b2fff581f1265bf27c8ff1d3)이며 0.0.2V는 이전 `8f09183`으로 보존합니다. 아래 신규 PR의 재검토·미병합 상태와 이전 병합 완료를 구분합니다.

## 담당별 수정 요청

| 담당 | 대상 | 재검토 결과 | 문서 |
|---|---|---|---|
| [1번](1번/README.md) | PR #2 | R1~R3 해결 확인(a35c972)·alpha 병합 | [공통 기반 수정 요청](1번/2026-10-16_PR2_공통기반_수정요청.md) |
| [4번](4번/README.md) | [PR #7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/7) · 공지 DB 연결 | **R1/R2 독립 해결 확인·1번 ID/API 수용 회신 수신** · Draft·미병합 · README 충돌/결합 재검증·Ready 확인 · DDL/버전/공유 별도 · headc7282d1 | [검토자 수정 요청](4번/2026-10-17_PR7_공지DB_수정요청.md) · [작성자 보고](4번/2026-10-17_PR7_공지DB_검토상태.md) |

4번의 [이전 PR #1 필터 수정](4번/2026-10-16_PR1_공지필터_수정요청.md)은 해결·병합 완료 상태로 보존합니다. 신규 [코드 PR #7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/7)은 검토자 재현(head7478101) 뒤 수정 headc7282d1에서 R1/R2의 해결을 독립 확인했습니다. 코드 PR은 Draft·미병합입니다. PR #12의 N4 회신으로 1번 ID/오류·현재 API 수용은 확인됐으며, 최신 alpha의 루트 README 충돌 해소·결합 재검증·새 head/Ready 확인과 DDL/버전/공유 활성화 후속 조건은 남아 있습니다. [LOG PR #8](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/8)은 `066f25e`, [문서 PR #9](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/9)는 `9edcb08`로 병합 완료입니다. 문서 병합은 코드 승인·alpha 반영·API/DDL 합의 완료를 뜻하지 않습니다. [이번 독립 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#r022)에서 기존 테스트 통과와 추가 결함을 구분합니다.

## 수정 후 재검토

각 담당은 기존 코드 PR에 수정본과 최신 README를 푸시하고, 새 head SHA·변경 파일·각 재현 절차의 수정 후 결과·기존 검증 결과를 알려 주세요. 기존 테스트 통과와 추가 결함 해소를 구분합니다. 검토자는 새 커밋에서 재현이 사라지고 기존 동작이 유지되는지 확인한 뒤 병합 여부를 판단합니다.

새 요청 문서는 `담당번호/YYYY-MM-DD_PR번호_수정내용.md`로 작성합니다. 수정 여부·확인일·근거 커밋·실행한 검증·미실행 항목을 기록하고 푸시할 때 해당 담당 목차와 이 README를 함께 갱신합니다. 1번 a35c972와 4번 837e411은 재검토를 통과해 alpha에 병합됐습니다. 각 담당 문서에 최초 요청·담당자 보고·검토자 확인을 구분해 보존합니다. 업무 API·두 PC 등 미검증 기능까지 완료로 표시하지 않습니다.

3번의 제출·재제출·제출 차수·승인/반려·파일과 5번 기능은 각 담당 범위를 유지합니다. 자동 결과 제출은 제외하고 업무 API·권한 범위·상태·날짜·페이지·자동 검토 요청은 합의 후 연결합니다.

확장 협의 [LOG PR #10](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/10)은 2026-10-17 15:55:04 KST에 LOG `530f171`로, 수정 회신 [PR #11](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/11)은 15:55:09 KST에 PR `120d855`로 병합했습니다. 당시에는 1번 공지 전용 회신 대기였으며, 후속 PR #12에서 회신을 수신했습니다. 문서 병합·회신 수신은 코드 승인·alpha 반영·공유 적용과 구분합니다.

## 검토자 독립 재검토 — 2026-10-17

수정 head `c7282d1e9e332daf3996aeb00f9a1b20c6ad2e61`에서 **R1·R2 해결을 독립 확인**했습니다. 코드 PR #7은 **Draft·미병합**입니다. 최종 ID/오류·API/DTO·DDL/버전·정식 활성화에 대한 1번 회신과 최신 alpha의 README 충돌 해소가 남아 있습니다. 결함 해소 확인과 코드 승인·공유 적용은 구분합니다.

| 항목 | 이번 검토자 확인 | 남은 사항 |
|---|---|---|
| R1 · 동시 삭제 | 새 MySQL 8.2 REPEATABLE-READ의 독립 인증 요청에서 200/404, 삭제 감사 1건, 삭제 경고 0건. SQLite도 동일. 잠금 뒤 current read와 ORM 캐시 갱신의 회귀 통과 | 기존 R1 수정 요청 해결 확인 |
| R2 · ID 범위 | `2^31`·`2^63`·`10^100`을 두 DB의 상세 GET·관리자 삭제 POST로 요청하면 모두 404 `NOTICE_NOT_FOUND`. SQLite 500 재현 해소 | `1..2147483647`·범위 밖 404는 현재 개발 방어안. 최종 범위/400 또는 404는 1번 N4-02/03 확인 필요 |
| 서버 회귀 | SQLite 전체 74통과·16생략. 별도 MySQL 공지/공통 관리자 표적 32통과. SQLite 동일 표적 28통과·4생략 | MySQL 전체 회귀는 미실행. 표적 SQLite는 전체 결과에 포함되므로 합산하지 않음 |
| 클라이언트 | C# 공지 HTTP 대역 30검증·실제 C#→HTTP→임시 SQLite 37검증 통과 | 37에는 기본 30검증이 포함됨. 실제 C#→MySQL·수동 전체 GUI는 미실행 |
| 코드 병합 준비 | 검토 기준 alpha `c96d495`와 수정 head의 `merge-tree`에서 충돌은 루트 `README.md` 1개 | 최신 2번 입력 준비·검증·합의 요청 안내를 보존하며 공지 수정/미합의 설명을 함께 반영하고, 새 head를 재확인해야 함 |
| 문서 병합 | PR #10은 LOG `530f1710ed67a3f2649da984912a2ea09935aaf9`, PR #11은 PR `120d855f3168a0631d7e7b0cf9d1a429ec4749b8`로 병합 | 1번 계약 수용·코드 PR #7 병합과 별개 |

공통 인증/DB와 2번 서버 영역은 검토 기준 원본과 동일하며 공지 기본 off·공통 version1·업무002 예약을 유지했습니다. 이번 검토에서 공통 기능·3번 제출/판단·5번 장치를 변경하지 않았습니다. 공유/운영 DB·정식 DDL/복구·배포·두 PC·웹·장비 검증은 미실행입니다. 작성자의 수정 보고 수치를 이번 독립 실행으로 대체하거나 합산하지 않습니다.

[이번 독립 검토·문서 병합 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#r023) · [1번 공지 계약 협의](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_4번_공지DB_등록_협의요청.md)

최종 게시 준비에서 최신 alpha [6ab86e4](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/6ab86e4efa80aba9d001dd5edbe007319ecaf18d)와도 README.md 1개 충돌을 재확인했습니다. 최신2번 로컬 확인값·실제HTTP 준비 창 안내를 함께 보존해야 합니다. 코드 #7 원본 head는 이번 작업에서 변경하지 않았습니다.
