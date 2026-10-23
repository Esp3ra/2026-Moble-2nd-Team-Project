<a id="owner4-pr83-provider-20261023"></a>
## 4번 최신 — PR83 공개 reader 공급 병합 / D3 실제 삭제 연결은 후속

[PR83](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/83#issuecomment-6355309762) **048572cb→badffbdf**로 동일TX의 현재첨부ID reader가 제공됐습니다. 원nativeMySQL/HTTP188과독립정적검토를통과했지만4번 실제 `notice_resource(...,'release')`409·`block_notice_file_delete`는 diff0/차단 유지입니다. 시험consumer의삭제통과를실제서비스/화면연결로확대하지않습니다.

3번채팅의 사용자선택보고(A연결만해제/B다운로드유지·마지막참조정리·commit뒤파일정리실패재시도)를 수신했습니다. 정책전달/담당수용뒤 기존관리자TX·공지잠금→공개reader→기존release→공지삭제·감사→commit으로 연결하고, 불명결과는원요청보존·확정전UI삭제성공표시금지를 유지하면 됩니다. 공지생성 durable원UUID/W1은1/4번 별도입니다. 확인기록CASCADE를 이번검토만으로 파일삭제활성화로 확대하지않습니다.

기존78병합/협업기본30초·모달owner허용은유지합니다. [PR80](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/80#issuecomment-6354991039)도수정병합완료입니다. [직접근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr80-83-reviewed-20261023)·[후속경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr80-83-boundaries-20261023). 실제공유삭제·두PC·배포는이번에실행하지않았습니다. 아래는이전기록입니다.

<a id="owner4-pr78-merged-20261023"></a>
## 4번 최신 — PR78 R78-01 해소·협업 수신까지 병합

[78](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/78#issuecomment-6337428379) 최신 **f4ab296c→ea45d437** 병합 완료입니다. A완료/B실패/C미시도 뒤 B만 재시도해도 C를 보존해 보내기·Enter를 막고, C완료/명시 제외 뒤 원순서 첨부를 전송합니다. 불명 메시지의 원 UUID/body/files도 보존합니다. 소유HTTP33·C#11·부분복구16·실제Edge12/pageErrors0, 별도 협업118·기한UI166/연결75·독립모달7 및 Release0/0을 직접 확인했고 겹치는 수치는 합산하지 않습니다.

환경변수 누락 시 로그인 직후/30초 수신과 ShowDialog 메인 owner 허용·다른 모달 양보를 확인했습니다. 기본30초는 보고된 사용자 요청의 클라이언트 기본값이며 전체 운영정책 승인으로 넓히지 않습니다. **D3는 별도 대기**, 첨부공지 삭제409·공지 저장 불명 시 입력/파일ID 유지·자동재등록 차단을 보존합니다. 확인기록 CASCADE는 파일 삭제 승인으로 확대하지 않습니다.

[중앙 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr78-82-reviewed-20261023)·[후속 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr78-82-boundaries-20261023).1번/4번 각PC 읽기/파일 왕복은 작성자보고로 수신했고 이번에 공유서버·동시두PC·25MiB UI/장시간 부하를 재실행하지 않았습니다. 아래는 이전 이력입니다.

<a id="pr7778-owner4-current-20261022"></a>
## 4번 최신 — PR78 채팅 첨부 원선택 보존 수정 요청

[78 고정 e0bfbb4a](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/78#issuecomment-6337428379)의 공지/채팅 실제 자원 콜백·첨부/웹/LAN hash·마감 binding 소비를 검토했습니다. 기본 회귀 및 실제 Edge 검사는 통과했으나 **R78-01/P2**로 미병합입니다. A/B/C 선택→A 성공·B 실패·C 미시도→B만 재시도 성공 시, 웹과Desktop이 C를 미완료 선택으로 유지하지 않아 A+B만 전송하도록 허용합니다. 원선택 전체의 미완료 항목을 완료 또는 명시 제거 때까지 보존해 주세요. 서버 저장/권한 우회 결함으로 확대하지 않습니다.

[77](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/77#issuecomment-6337145116)의 물리 CHECK 검사 수정은 **38233949**에 병합했습니다. [검사/독립 재현](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr7778-reviewed-20261022)·[담당별 후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr7778-boundaries-20261022)에서 제공부·원제품 소비·대역·실제 브라우저 범위를 구분합니다. 공유 서버·실계정에서의 PC GUI/파일 대화상자·두PC 검증과 삭제/보존·연차 미합의는 별도이며 이번 기록으로 정책을 새로 선택하지 않았습니다.

<a id="pr7576-owner4-next-consumption-20261022"></a>
## 4번 최신 — 마감 기준 공급·자원 첨부 소비 접점 수신

[75](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/75#issuecomment-6336468832) **e8945bba**는 현재 기준 사건/014를, [76](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/76#issuecomment-6336589781) **36f1e55d**는 공지/채팅 자원 공용첨부·웹 어댑터 계약을 공급했습니다.75의작성자5000·37→40·4baseline·HTTP22·복원1 보고는 이번 검토에서 재실행하지 않았습니다. [소유DB 직접검사/미실행](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr7576-pr58-review-20261022)을 구분합니다.

마감은 최종 d1 task/event/assignment·원 operation/key/body·4필드receipt/로그인 wrapper의 **실제 binding 소비·PC GUI/두PC**가 남았습니다. 자원 첨부는 [76 계약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/36f1e55d44cd3fa2fd2c7f634fbcd0d059ce7a9d/docs/SHARED_ATTACHMENTS.md)에 실제 공지 대상/현재 방참여·개인숨김·남은 참조를 대조하는 권한/원TX 콜백과 UUID/결과복구·감사/UI를 연결해야 합니다. 제공부108 대역 콜백을 실제4번 인가로 사용하지 않습니다. 정식DDL 번호/공유적용·삭제/보존정책과 LAN HTTP crypto.subtle 제약은 공동 후속이며 검증을 끄거나 정책을 새로 확정하지 않았습니다. [담당별 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr7576-pr58-boundaries-20261022)를 따릅니다.

<a id="member4-pr70-merged-supply-20261022"></a>
## 마감 제공부 최신 — PR70 병합·실제 binding 소비 후속

[PR70 최종 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/70#issuecomment-6335250250)의 고정2a50535는 RR P2 해결 후 사용자 지시에 따라 Draft→Open→**cca5b621 병합**을 완료했습니다. 실제 tree **5f27a06dc4dbcfc797514eb9056dc4e572deb84c**가 검토본과 같습니다. 서버/문서211파일과 외부 RR 회귀 해시 일치로 기존23/21/7·RR1/2/1·HTTP8·cold12를 재사용했으며 이번 신규 실행 수치로 합산하지 않습니다.

실제 HTTP는 `d1:task:event:assignment`와 원 key/body·최소receipt를 제공합니다. 기존 string-event 중심4번 source/성공·불명 cache/로그인 wrapper가 이를 자동 소비한 것은 아닙니다. 2번 실제 writer hook, 기존자료 baseline/중간상태·옛 성공 열람,정식 DDL·공유 적용은 후속입니다. [정확 검사](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr707172-merged-20261022)·[공동 접점](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr707172-merged-20261022)을 확인하며 채택된 A1~A6는 재질문하지 않습니다. PR71·72 병합 및 최종 Release0/0은 실제 마감 source/두PC 운영 완료를 뜻하지 않습니다.

<a id="pr54-deadline-boundary-owner-status-20261022"></a>
## PR54 후속 수신/제공 상태 — 2026-10-22

[1번14:06 갱신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305220140)은 현재 facts 수신과 새 UUID 사건/배정세대·snapshot/확인 W1 제안입니다. 4번은 11:30 이후 추가 회신 없음으로 구분합니다. 2번은 [기한 변경 원 작성부/같은 TX 결합 위치](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/94a40353a1e6cf2cdff24106a07b34d1ad46b88e/docs/TASK_QUERIES.md#deadline-mutation-boundary)와 소유 MySQL 17검사를 제공했으며 [제공·제안·합의 필요·미실행](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-deadline-boundary-reply-20261022)/[실행 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#member2-deadline-boundary-check-20261022)/[기존 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305519634)로 취합합니다. 실제 기록 함수·event/확인 저장 API/DDL/source는 미제공/대조 대기이며 새 정책 승인/같은 facts 재요청을 반복하지 않습니다.

<a id="member2-own-deadline-facts-status-20261022"></a>
## PR54 요청 대응 — 2번 현재 본인 업무 읽기 제공, 2026-10-22

2번은 [전체 현재 사실·단건 재검사 내부 함수](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/dc525d08312eb6d31c4c9e56aa6367a71103dfb9/docs/TASK_QUERIES.md#own-deadline-current-facts)를 alpha **dc525d08312eb6d31c4c9e56aa6367a71103dfb9**로 제공했습니다. 관리자도 본인 배정만,5상태·취소·기한·배정/업무 버전을 같은 인증 TX에서 읽습니다. 실제 소유 MySQL **52통과·실패/오류/skip0**, 읽기 전후 전체 DB 무변경·오래된 RR/ORM 캐시 뒤 현재 사실/인증 재검사를 확인했습니다. [제공 범위·1/2/4 합의 구분](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-own-deadline-facts-20261022)·[검사 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#member2-own-deadline-facts-check-20261022)·[기존 PR54 통합 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305519634)에 취합합니다. 실제 알림 API/source·서버UTC·기한 사건/초기자료 전환·확인 저장은 후속입니다. 아래 과거의 최소 reader 미제공 표시는 당시 이력입니다.

<a id="pr66-pr67-status-20261022"></a>
## 추가 PR 검토 — PR66 병합·PR67 수정 요청, 2026-10-22

**PR66** head b8825dd의 마감 알림 확인 성공/목록 재조회 분리를 검토했습니다. 공식 마감162·업무 이동24·공지35·협업115검사와 화면 수명·경합 독립19검사 PASS, Release 0오류/0경고를 확인해 [alpha에 병합](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/8cc082155561ea060e3676c346aed1d87fb8e5e9)했습니다. 실제 병합 tree **8723d2a270dd03bd15f3d8bdc4f096c25b733ab5**는 검토본과 정확히 같습니다. source가 원 사건/계정·W1 key/body의 성공을 검증한 뒤 목록 실패와 분리하는 내부 계약이며, 실제 마감 API/DB/source 설치 완료는 아닙니다.

**PR67** head a4b9d58은 실제 서버·Pi HTTP/MySQL 회귀4파일 추가입니다. 독립 소유 MySQL/두 loopback HTTP 10PASS/55.61초, exit0·소유 DB/프로세스/runtime 정리를 확인했습니다. 정상 경로와 별도로 **R67-01/P2: Job 등록 실패 시 생성된 suspended 검사 자식 정리 누락**을 대역 재현해 [수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/67#issuecomment-6330831748)했습니다. 기존 PR67에 수정 후 재검토하며 현재 미병합입니다. 고정 source의 server/Pi와 합성 센서를 잇는 검사로 실물·C# UI·공유 서버 성공과 구분합니다.

[상세 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr66-pr67-review-20261022)에 정확한 SHA·검증 범위·후속을 취합했습니다. 실제5000/.env·공유 DB/DDL·운영 키·Pi/센서는 변경하지 않았습니다. 아래 이전 PR 상태는 각 당시의 기록입니다.

## 최신 — PR64 공통 조립 병합과 실제 마감 source 후속, 2026-10-22

[PR64](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/64#issuecomment-6330306907)을 **ca781fd**로 병합해 협업 일반 서버 시작/coldOFF 결과읽기, 로그인당scope·메뉴borrow, 중요공지/협업 await뒤양방향gate, 명시적 MainForm deadline 연결과2번 navigator 조립을 수신했습니다. 서버121·UI24/130/105/115/144/35·최종Release0/0을 직접 확인했습니다. [중앙 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr62-65-review-20261022)를 따릅니다.

실제 마감source는 기본 설치하지 않으며 업무의 현재사실/기한사건·초기자료·확인DB와 계정확인 원자성은 후속입니다. Recheck는 권위 있는 전체 snapshot을 제공해야 하고 최소W1 성공영수증과 선택적 목록실패를 분리하는4번 포트 보완 요청도 유지합니다. TaskRequested 발행은 실제 업무 표시 성공이 아니며 현재본인 탐색·접속수명검사를 유지합니다. 협업 polling은 명시값이 필요하고 제안10초를 채택하지 않았습니다. A1~A6/B1~B7을 새로 바꾸지 않고 실제공유환경·팝업/두PC 검증과구분합니다.

## 최신 — PR61 R61-01/02/03 해결·alpha 병합, 2026-10-21

head3ae607ee를 최신alpha739c510에서 독립 재검토했습니다. 업무 보기401/403 폐기, 늦은 응답의 Apply/탐색 차단, Read성공/Recheck503의2→4→5→5분을 해결 확인했습니다. 공식 마감105·공지35·독립16·backoff2대조/폐기 polling3시나리오·Release0/0 통과 후 사용자 지시에 따라 **ad46517**로 병합했고 실제 tree가 검토본과 일치합니다. [기존 최종 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/61#issuecomment-6311013832)·[중앙 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr61-merged-20261021)을 따릅니다.

alpha739c510의2번 실제 협업 연속성 reader는 알려진 완전한 진행중 구간의 True/False/None을 제공합니다. 기한 사건·최초 자료 완전성·알림 최소 reader/DTO·확인 writer까지 제공하는 것은 아니며 1·2번 실제 계약/조립은 후속입니다. 실제 HTTP/MySQL·DDL·90일 history·MainForm/팝업 양방향·물리 두PC는 미실행입니다. A1~A6와 권한/W1/같은TX 경계를 유지합니다. 아래 Draft/수정대기는 당시 이력입니다.

## 최신 — PR61 마감 알림 검토·수정 3건과 2번 기술 회신, 2026-10-21

alpha388d50b+head5cca781을 독립 결합해 공식 마감78·공지35·Release0/0을 직접 확인했습니다. 별도 실제 WinForms 대역에서 **R61-01/P2 업무 보기 재검사 401/403 뒤 scope/기존 팝업 유지, R61-02/P2 늦은 응답이 제거된 미확인 항목 복원/탐색 실행, R61-03/P2 Read 성공+Recheck503 연속 실패의 2,2,2,2분**을 재현하여 Draft·미병합입니다. [수정 조건·재검사 요청과 2번 기술 회신 한 곳](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/61#issuecomment-6311013832)·[중앙 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr61-review-20261021)를 따릅니다.

A1~A6 정책을 재질문하지 않습니다. 기존 2번의 현재 잠금/실효 due_date·감사 원본은 재사용 가능하지만 알림 최소 reader·실제 사건과 초기 자료 전환/연속성·같은 TX 확인 저장은 아직 후속입니다. 1번 인증/서버 시각·W1/확인 저장·MainForm과 2번 탐색 조립도 미완료입니다. 구체 endpoint/DTO/DDL 번호·설정명·due_revision을 이번 검토로 확정하지 않습니다. 실제 HTTP/MySQL·90일 history·공유 서버/DB·물리 두 PC 검사는 미실행이며 실제 제품 폴더/환경은 변경하지 않았습니다.

## 최신 — PR57 전체지적 해결·병합 완료, 2026-10-21

head0f47e5d의R57-04를UI113/이전독립재현으로해결확인했습니다. 수동창중자동추가0·닫힌후1회표시·동일버전중복0/쓰기0, 기존409최신문맥/새key도확인했습니다. 사용자지시에따라Draft→Ready후 **ba9e312**로병합했습니다. [기존최종회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308590391)·[검사/재사용·실제경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr57-58-60-merged-20261021)를따릅니다. 기존서버/검사/012동일성으로이전MySQL55를재사용했고이번에재실행했다고표시하지않습니다.1번공통설치/MainForm/공지양방향·실제012/공유배포,2번실제연속성reader와운영값은후속이며None/503과원업무/파일권한경계를유지합니다.

## 최신 — PR57 기존5건 해결, 새 알림중복으로 Draft 유지, 2026-10-21

head8613c947을 실제alpha44c85f3과 결합한 tree47bfdd6f에서 서버55·UI25+48=73·Release0/0을 직접 확인했습니다. 기존 팀장3건/1번2건은 해결 확인했고 공유scope소유/지연표시/당사자이력도 제공됐습니다. 다만 **R57-04/P2: 조회중 수동 응답창을 열면 같은 요청창1→2개**를 실제WinForms로 재현하여 Draft·미병합입니다. [기존 위치의 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308590391)·[중앙 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr57-59-review-20261021)를 따릅니다. MainForm/공통 설치·012 실제적용·2번 버전구간 연속성reader·운영값/실제공지동시통합은 계속 후속입니다. 실제 환경/제품폴더는 변경하지 않았습니다.

## 최신 검토 — PR57 협업 Draft 수정 필요, 2026-10-21

[PR57](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57) headfe209ae를 alpha8ed25c6과 결합해 공식서버24·Directory25/6시나리오·Requests23·Release0/0을 확인했습니다. 별도검사에서 **R57-01/P2 취소 snapshot 혼합, R57-02/P2 상대 비활성화 시 종료 차단, R57-03/P2 미기록 확인 뒤409의 입력잠금**을 재현하여 미병합/Draft 유지입니다. [고정 코드·조건·수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308590391), [중앙 검토 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr57-review-20261021)를 따릅니다.

기존 B1~B7 채택은 유지합니다. 1번의 새 API/W1·미번호DDL/등록·MainForm 로그인수명,2·4번의 CURRENT취소/실제 중간이력 연속성, B4지연표시·B7상태이력 조회가 남아 있습니다. 협업10초/닫기/재로그인값은 로컬 제안이며 기한알림60초와 혼동하지 않습니다. [2번 기술 대조 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#pr57-member2-bridge-review-20261021)를 기록했습니다. 이번 검토는 제품/실제폴더·공유서버/DB·.env/DDL·장비를 변경하지 않았습니다.

## 최신 — 기한 알림 A1~A6·협업 B1~B7 진행 기준 채택, 2026-10-21

팀장이 본 채팅에서 앞선 추천안대로 진행하고 문서를 푸시하도록 지시했습니다. **A1~A6·B1~B7을 개발 진행 기준으로 채택**하며 [전체 정책·담당·기술 확인/검증 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member4-policy-adopted-20261021)를 따릅니다. 기능 구현·새 API/DB/DDL·실제 통합 검증 완료를 뜻하지 않습니다. 기존 문서의 F4 정책 미확정 표시는 당시 이력으로 보존합니다. 지문 운영·근태/연차·별도 수동/자동 검토 요청과 실제 데이터 삭제/감사·백업 보존 정책은 이번 채택 범위에 포함하지 않습니다.

## 현재 — 1·2·3·5번 최신 수신·미해결 접점 통합 회신 요청, 2026-10-20

[4번 대표 통합요청](2026-10-19_C08_C09_채팅공지_협의회신.md#member4-owner-followup-request-20261020)에 이미 받은 제공물의 수신 회신과 남은 차이를 정리했다.1번 환경/공통,2번 실제업무/읽기,3번 협업자료권한,5번 독립수명 추가접점 유무를 각각 기존담당폴더 링크로 전달한다. 열린PR54에갱신하며새문서/추가PR은만들지않는다. 5번새기능/장치성공은4번의선행조건이아니다.

팀장 F4-02/03 정책결정과 담당별 미해결차이의통합답변 대기이며 문서병합/수신을 구현·실제검증완료로 바꾸지 않는다. 실제등록환경근거와1번준비요청은PR53/54의기존대표기록을 유지한다. 아래공지PR45해결·병합과 과거상태이력은별도로보존한다.

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

45 기존 FK/인증 대역 지적은 해결됐습니다. 삭제 후 확인 기록 보존과 CASCADE 불일치로 Draft/미병합·008공유 적용 보류를 유지합니다. [검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr44-47-recheck-20261020).

## 최신 — PR45 Draft 수정 요청 / PR46 사용자 보류, 2026-10-20

[PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) 최신 head `8f9e2d2`는 **Draft·미병합**이며 R45-01 P2(다른 DB의 같은 이름 부모를 참조한 FK도 준비 검사 통과), R45-02 P2(MainForm 새 popup 요청으로 기존 인증 종료 회귀 실패)를 확인했습니다. 서버 표적118통과와 별도 FK 독립1실패, 공지C#35·Release0/0을 구분합니다. 기존 활동 회귀 원본은 실패했고, 새 경로404만 추가한 검토 복사본은294/12통과입니다. 제출 원본의 통과로 대체하지 않습니다.

**PR46은 사용자의 '46번은 보류' 지시에 따라 검토·병합 보류 / OPEN·미병합 유지**입니다. 문서 답변/임시 시연 정보·정책을 수용하거나 제품 코드로 반영하지 않았습니다. PR44의 R44-01 수정 요청·미병합도 유지합니다.

[상세 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr45-review-pr46-hold)를 따릅니다.

> **2026-10-19 · 1번의 전체 잔여 협업 통합 회신안** — [14개 세부키 답변](../1번/2026-10-19_공통규약_사전협의요청.md#member1-reply-20261019). 공통 인증/통신·DB/운영·웹/파일·장치/MainForm 접점의 유지 기준·현재 제안·제공물·담당/결정 조건을 한 번에 정리했다. PR40/41 병합·1/4/5번 통합회신 수신을 반영하고,4번X02 배정revision 타입·5번 실제감지서비스/보고된지문요구의 연결 경계를 대조했다.  공유 환경·3번 최종 모델·4/5번 후속 정책/실제 통합은 협의사항이다. 사용자 요청에 따른 LOG 검토 PR이며 상대 수신/정책 합의·구현/병합 완료를 대신 선언하지 않는다. 기존 질문/팀장 규약 원문은 아래에 보존한다.

## 최신 검토 — PR40·41 코드 / PR42 회신 병합, 2026-10-19

[PR40](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/40) 1번 공통 지원은 alpha `7073817`, [PR41](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/41) 5번 독립 감지 서비스/공유 view는 alpha `763f11e`에 검토 후 병합했습니다. 새 차단 수정 요청은 없습니다. 직접 검증은40번 Python107·C#31,41번 C#294/12시나리오·Python30, 결합 Release 경고0/오류0이며 실제 병합 tree와 일치합니다. [PR42](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/42) 4번24개 통합 회신은 기존2문서/원문·링크/앵커를 검토해 LOG `2e6a439`에 병합했습니다. 문서 검토 때문에 제품/DB 시험을 추가 실행한 것은 아닙니다.

40번은 독립 읽기 점검/바이트 초안/화면 요청 수명 도구이고41번은 소유자가 주입할 서비스 준비입니다. 실제 본인 확인 서버·근태 저장·출근~퇴근 MainForm 연결·실장비/공유DB/두PC는 아직 구현/검증 대상입니다. 회신 수신을 제안 정책의 확정으로 확대하지 않습니다. 아래의 이전 대기/미연결은 해당 시점 기록입니다.

[검토·실행/미실행 상세](../2번/PR_검토기록.md#pr40-pr41-pr42-review)를 기준으로 확인합니다.

## 최신 제출 — 4번 전체 잔여 협업 통합 회신, 2026-10-19

[기존 대표 문서의24개 답변](2026-10-19_C08_C09_채팅공지_협의회신.md#member4-unified-answer)을 팀장에게 LOG 문서 PR로 제출합니다. LOG/2번의1회 텍스트 회신 기준을 확인했고, 사용자가 추가로 PR 제출을 직접 요청했습니다. **제안/협의사항은 검토 대기**이며 정책 확정·새 제품 구현·공유 검증이 아닙니다. 신규 회신 MD·날짜 LOG·2번 문서 편집 없이4번 기존2문서만 갱신합니다. 아래 기존 요청/완료/검증 이력은 원문 보존입니다.

## 현재 협업 기준 — 2026-10-19 전체 최신화

**4번은 [통합회신서 — 1회 제출](2026-10-19_C08_C09_채팅공지_협의회신.md#member4-once-reply)의 전체 질문에 한 묶음의 텍스트로 팀장에게 답합니다.** 공지/웹·첨부·팝업 확인, 개인/단체 채팅·협업 요청, 마감 전날 알림·연장 후 갱신, 운영·공유 시험까지 1·2·3·5번과의 접점을 모았습니다. 새 MD·추가 회신 PR 없이 팀장이 기존 LOG에 일괄 반영합니다. 확인되지 않은 정책/범위는 협의사항이며 제품 PR 검토는 별도입니다.

4번의 현재 회신은 [C08/C09 대표 문서](2026-10-19_C08_C09_채팅공지_협의회신.md)에서 관리하고, 담당 간 남은 접점은 [팀장 협의표의 4번 항목](../2번/공통규약.md#collab-member4)과 [공통 회신 양식](../2번/공통규약.md#collab-reply)을 사용합니다. **미확정 항목은 모두 협의사항**입니다. 아래 날짜별 원문·헤딩·검증 수치는 보존하며 과거의 Draft·미병합·기술 회신 대기 표현을 현재 상태로 재사용하지 않습니다.

| 항목 | 현재 확인된 상태 | 남은 협의사항·다음 회신 |
|---|---|---|
| C08 채팅 API·005/ORM | 코드 PR21·DB 검증 PR25 병합, 문서 PR26·29 수신. [1번 후속 회신](../1번/2026-10-19_C08수신_C09공지_기술검토회신.md)에서 005/ORM 정합성·UUID 단일 효과·본인 숨김 기준의 수신/기술 수용을 확인함 | 팀장·1번·4번: 공유 적용 환경/실행자/백업·복구, 운영 보존·숨김 복원·탈퇴/비활성 처리, 전체 이력 polling 부하와 페이지/차분 조회 필요 여부 |
| C09 / N4-05·08·09 | 기존 사용자 기준과 4번 후속 제안, 1번의 시작 경계/cursor·확인/FK·삭제 경합 기술 검토를 수신함 | 4번 구체 모델/API/cursor·잠금/복구 시험안 → 1번 기존 인증/DB/삭제 호환 검토 → 팀장 정책 결정 후 구현. 팝업·확인/재확인·삭제/보존·주기는 협의사항 |
| WEB-N4 / PR35 | 잘못된 요청으로 CLOSED·미병합. 관련 1번 회신은 당시의 기술 의견으로만 보존 | 유효 구현 요청·정책 승인·공통 파일 수정 근거로 사용하지 않음. 새 웹 범위는 유효 요청과 변경 경계를 별도로 확인 |
| PR38 웹 공지 코드 | 수정 head `8b57ed1cd74ffcc80a66e53784061874b86af149`가 alpha `8cfdfea21425c2cd43f05907c618098b34a56dd7`에 병합됐고 다른 채팅의 재검토 기록을 수신함. 기존 opt-in 개발 서버의 웹 목록/상세·관리자 작성/삭제 연결 | [다른 채팅 검토 기록](../2번/PR_검토기록.md)에 소유 MySQL75통과·이전 실패 재현1통과가 있음. 아래 이전 head `0924db4`의 R38-01·65통과/별도1실패와 분리하며 이번 문서 작업이 새로 실행한 결과가 아님. 팝업/확인·실제 첨부·마감 알림·공유 적용 및 후속 정책은 별도 |

이번 최신화는 문서·링크·기존 회신 및 원격 alpha의 관련 안내를 정적으로 대조한 작업입니다. 제품 코드 수정, PR38 새 head 내용 검토, 프로그램/DB·공유 서버·두 PC·부하 시험은 실행하지 않았습니다. 수신·기술 수용·정책 확정·코드 병합·공유 환경 검증을 구분합니다.

## 최신 — PR38 R38-01 해결·병합, 2026-10-19

[PR38](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/38)의4번 수정본8b57ed1을 직접 재검증해 **R38-01 해결·새 차단 문제 없음**으로 alpha `8cfdfea`에 병합했습니다. 소유 MySQL75통과와 이전 실패 재현1통과, 검증/병합 tree 일치를 확인했습니다. 웹 공지는 기존notice opt-in 개발 서버에서 목록·상세·관리자 작성/삭제가 연결됩니다. 팝업/확인·실제 첨부·마감 알림·공유 적용은 별도이며 닫힌PR35는 승인 근거가 아닙니다. 아래 미병합/실패는 당시 기록입니다.

## 최신 PR 검토 — PR38 수정 요청·PR39 병합 확인, 2026-10-19

이 절은 이전 코드 head `0924db4` 검토 기록입니다. 코드 [PR38](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/38)의 **R38-01 P2: 2페이지 선택→조회 시 page 누락으로 다른 상세 표시**를 당시 실제 MySQL에서 재현했고 기존65통과·별도 회귀1실패를 기록했습니다. 새 head `8b57ed1`은 이후 다른 채팅의 해결 확인·재검토를 거쳐 alpha `8cfdfea`에 병합됐으며 과거 수치를 새 결과로 적용하지 않습니다. 문서 [PR39](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/39)는 검토 도중 LOG `754a6ea`에 병합된 상태를 확인했고 독립 문서 검증에서도 차단 문제를 찾지 못했습니다. 1번이 회원/장치 본인 확인 서버·DB/MainForm,5번이Pi/PC 중계·감지/근태를 작성한다는 역할 회신이며5번 확인·제품 구현은 별도입니다. [당시 4번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-19_PR38_웹공지_페이지선택_수정요청.md)을 보존합니다. PR35는 CLOSED·미병합을 유지합니다.

> **2026-10-19 · 팀장 최신 처리: 잘못된 PR35 닫힘 / 문서 PR37 수신**
> 사용자 지시로 [PR35](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/35)는 **CLOSED·미병합** 처리했습니다. #35의 기존 요청을 승인된 정책·공통 파일 수정·실행 근거로 사용하지 않습니다. [PR37](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/37)은 1번의 기술 의견/협의 자료로 수신·병합했으며, WEB-N4 내용은 닫힌 요청의 작성 당시 검토 의견으로 보존합니다. 웹 작업의 유효 요청·범위는 별도로 확인해야 합니다. 회원/장치·근무 수명안과 남은 정책도 공동 합의·제품 구현 완료로 확대하지 않습니다.

> **2026-10-19 · 신규 수신 요청35/36 후속**
> [PR35 WEB-N4-01~06에 대한1번 기술 회신](../1번/2026-10-19_WEB-N4_웹공지_연결규약_기술회신.md) — 경로/CSRF·채널·함수공유·opt-in/공통3파일 최소 범위와 필요 회귀의 당시 의견입니다. 닫힌 요청의 기술 의견을 현재 구현 승인으로 사용하지 않으며, 당시 ‘실제코드 PR 대기’ 안내는 현재 PR38 상태와 구분합니다.

> **2026-10-19 · 1번 수신 요청 후속 회신**
> [C08 수신 확인·C09 기술 검토 회신](../1번/2026-10-19_C08수신_C09공지_기술검토회신.md) — C08의005/ORM·UUID/숨김 수용을 확인했습니다. C09 cursor·확인/FK·삭제 경합의 구체 설계안을 회신하며, 정책·모델/실제API 구현은 별도 대기입니다.

## 2026-10-19 - 4번 C08/C09 협의 회신

[대표 회신](2026-10-19_C08_C09_채팅공지_협의회신.md)에서 C08 수용·005/ORM 정리·PR21 원본 병합 확인, C09 사용자 기준 수신과 미합의 제안을 구분합니다. **1번 C08 정합성 확인·C09 기술 검토는 수신 완료**이며, C09 공동 정책과 구체 모델/API 확정은 협의사항입니다. PR21 head599a55f는 원본 alpha ff908ab로 병합됐으며 아래 과거 Draft/미병합 표현은 작성 당시 기록입니다. 공유 DB 적용·두 PC 검증 완료는 아닙니다.

> **2026-10-19 · 1번 공통규약 사전 협의 요청** — [요청서](../1번/2026-10-19_공통규약_사전협의요청.md)
> 4번 확인: C08 채팅 005 SQL/초기화 설명 정합성 회신은 처리됐습니다. C09 공지 보존/팝업/확인의 정책·모델 확정은 협의사항이며, 기술 검토 수신을 새 규약 확정·제품 구현 완료로 표시하지 않습니다.

## 4번 채팅 사용자 조회·DB 저장·개인 숨김 검토 요청 — 2026-10-19

아래는 최초 Draft 제출 시점의 보존 기록입니다. 현재 기술 수용·PR21/25 병합 및 남은 협의사항은 맨 위 현황을 따릅니다.

[대표 협업 요청](../1번/2026-10-19_4번_채팅초대_DB저장_개인숨김_검토요청.md)에서 구현/미합의/미실행을 구분합니다. [Draft 코드 PR #21](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/21), head832478e·기준alpha606e21c. 활성 사용자 초대·방/메시지 서버 저장·본인 목록 숨김을 구현했으며 저장 기능 빌드/MySQL/두 PC 검증과 1번 API·DDL/등록 경계·정식 migration·공유 적용은 대기입니다. 이전 공지 #7 완료 기록과 구분합니다.

# 4번 — 공지·채팅 협업 기록

## 최신 검토자 확인 — PR #15·#16 병합, 2026-10-17

[독립 검토·실행/미실행·병합 기록](../2번/PR_검토기록.md#r019)에서 검토자 MySQL159통과와 복원2건 재실행 통과, 포함된 실제 C# HTTP12 및 별도 C# 회귀를 확인합니다. W1 공통 지원은 원본 수신 완료이며 제품 업무 쓰기·W3 미정 정책·공유 적용은 남아 있습니다. 아래의 포크/검토 대기 표현은 제출 당시 원문으로 보존합니다.

게시 상태: [코드 PR #15](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/15)은 alpha `5fd99a8`, [LOG PR #16](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/16)은 LOG `54c6cd3`로 병합 완료했습니다. 공유 적용·제품 업무 쓰기는 별도입니다.

## 1번의 MySQL 복원 검증 경로 수정 전달 — 2026-10-17

[원인·최소수정·재검증](2026-10-17_1번_MySQL복원_테스트경로_수정전달.md)을확인합니다. 제품공지/003SQL변경없이기존복원검증CLI의한글경로처리만보완했고, 최신결합MySQL161통과에포함했습니다. 4번수신확인을대신하지않습니다.

## 4번 후속 전달 — 2026-10-17

아래는 당시 재검토 요청 기록입니다. 공지 코드 PR7은 이후 alpha `733b47afca123ed3b161e6d96b492a8afc6e163f`로 병합됐으며 [독립 병합 기록](../2번/PR_검토기록.md#r021)을 따릅니다. 공유 적용·후속 팝업/확인 정책과 구분합니다.

코드 #7 head [`d58064c`](https://github.com/zx234x/jang1/commit/d58064ccd30e7e718f94d5e5556d0f19c6afd88c)에 최신 alpha 결합·N4 수용·예약003 SQL/검증을 반영했다. 원본 미병합·새 코드/SQL 재검토 대기다. [대표 협의 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_4번_공지DB_등록_협의요청.md)에서 수신 상태를, [이번 검증/1번 확인 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/4번/2026-10-17_PR7_1번회신반영_003DDL_검증.md)에서 실제 결과와 미합의·미실행을 확인한다. 아래 내용은 이전 시점의 원문이다.

## 4번 PR #7 R1/R2 수정·1번 협의 요청 — 2026-10-17

R1/R2 코드 수정·작성자 격리 검증 완료, **재검토 대기**. 코드 head `c7282d1e9e332daf3996aeb00f9a1b20c6ad2e61`, 기준 원본 alpha `8a652adaba4a8471b2fff581f1265bf27c8ff1d3`. R1은 가드 안 current/locking read와 populate_existing으로 재확인해 삭제200/후속404·감사1건을 검증했습니다. R2는 현재 INTEGER/C#int 기준 개발 방어 경계1..2147483647와 기존 미존재404를 적용했습니다. **ID 범위·오류 응답의 최종 공지 계약은 1번 회신 대기**이며 BIGINT·공통 API/DDL 확정이 아닙니다. 추가로 MySQL 시각 저장 정밀도에 따른 등록/재조회 차이를 저장값 refresh로 보완했습니다.

구현/미합의/미실행은 [1번 대표 협의 문서](../1번/2026-10-17_4번_공지DB_등록_협의요청.md)의 0절·N4-01~10을 따릅니다. 이전 작성자·검토자 재현 보고와 이번 수정 검증을 구분하며 원래 기록은 보존합니다. [2번에게 전달할 수정 회신](../2번/PR_검토기록.md#r018)에서 새 head·재검토 요청을 연결합니다. 코드/문서 PR 병합과 공지 계약 수용은 별도입니다.

이번 직접 실행: SQLite 전체 **74통과/16생략**, MySQL8.2 REPEATABLE-READ 공지/공통관리자 표적 **32통과**(전체 MySQL 아님), 실제 공지 C#→HTTP→임시SQLite **37검증**, 최신 업무 HTTP 프로젝트/참조앱 Release **경고0/오류0**, 업무 격리 HTTP 실행기 **300검증·권한회수11개 포함**, C#직원36/관리자52검증 통과. 포함/반복 수치를 합산하지 않습니다. 공지 actual HTTP는 기존 공지 DLL과 수정 서버를 사용했고, 업무 DLL은 최신 원본 소스로 재빌드했습니다. 공유 DB·전체 MySQL/DDL 회귀·이번 실제 C#→MySQL·두 PC·웹·장비·수동 전체 GUI는 미실행입니다.

아래는 앞선 요청/검토 시점 기록이며 최신 처리는 위 수정 보고를 따릅니다.

> **후속 검토 · 2026-10-17:** #7 head7478101은 서버P2 2건을 직접 재현해 **수정 후 재검토 / Draft·미병합 / 1번 API·DDL 합의 필요**입니다. [4번 수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-17_PR7_공지DB_수정요청.md)과 [독립 검토 기록](../2번/PR_검토기록.md#r022)을 확인합니다. 문서 #8·#9는 각각 LOG066f25e·PR9edcb08로 병합됐습니다. 아래 내용은 앞선 작성자 기록으로 보존하며 문서 병합을 코드 승인으로 해석하지 않습니다.

공지 DB 저장·조회·삭제의 검토 대상은 [코드 PR #7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/7)이다. 팝업·확인 기록·정렬 추가 조정은 이번 PR에서 제외한다.

- [1번에게 전달한 공지 DB 등록·API/DDL 협의 요청](../1번/2026-10-17_4번_공지DB_등록_협의요청.md): 공통 파일 영향과 미합의 항목의 대표 기록.
- [2026-10-17 실제 작업·검증 기록](../LOG/2026-10-17/01_4번_공지DB_연결.md): 최신 결합 검증·환경 재검증·미실행 범위.

현재 상태는 포크 코드 게시·Draft PR 검토 요청·격리 검증 완료이며 원본 병합·공유 DB 적용·타 담당 합의 완료가 아니다. 기존 PR #1 디자인팀 필터의 병합 이력과 이번 DB 연결은 구분한다.

## PR 브랜치 목차 최신화

[문서 PR #9](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/9)는 4번 현재 대상을 코드 PR #7·재검토 대기로 표시하고 기존 PR #1의 완료 기록을 보존합니다. PR 브랜치 문서 commit `e1dfb96ba5dce4ead3b9a53ebf32eb5293b0988d`, base 원본PR9c9e3b6 ← 포크PR입니다. 이 문서 갱신에서 실행 코드·계약은 변경하지 않았고 프로그램 테스트도 재실행하지 않았습니다. 검토자 승인·원본 PR/alpha 병합과 1번 회신은 계속 별도 대기입니다.

## 1번 PR21 기술 회신 — 2026-10-19

아래는 최초 기술 회신·게시 시점 기록입니다. 이후 PR21·25·26의 병합과 C08 수신/정합성 확인은 완료됐고, 공유 운영과 C09 정책/모델은 협의사항입니다. 현재 상태는 맨 위 현황과 대표 회신을 따릅니다.

[항목별 수용/수정/보류와 독립 검사](../1번/2026-10-19_협업요청_기술검토_회신.md)에서최신907ae77의API/참여권한·Session/UUID단일효과·기본off/배치환경격리를검토했습니다. 005번호·실제DDL은1번이콜레이션을명시해제공하며기존번호미지정SQL정리와물리차이확인이필요합니다. 사용자기준은본인숨김·메시지/감사자동삭제없음,복원/탈퇴·운영보존은별도입니다. 코드PR21의Ready/최종병합·공유적용은별도이며4번후속확인을요청합니다.

게시 확인: [DB SQL·검증 PR #25](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/25) · [협업 LOG PR #26](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/26) 생성 완료. 포크 alpha/LOG → 원본 alpha/LOG 검토 대기. 이번 보완의 원본 병합·각 담당 수신/구현·공유 적용은 아직 완료로 표시하지 않는다.


## 보존 - 포크의 이전 R21-04 전달 시점

아래는 당시 재검토 대기 기록이며 최신 병합 상태는 위 회신을 따릅니다.

## PR21 R21-04 수정·검증 회신 — 재검토 대기

[1번 대표 협업 기록](../1번/2026-10-19_4번_채팅초대_DB저장_개인숨김_검토요청.md)의최신후속을확인합니다. 코드907ae77·alpha3558b05결합, 배치환경격리9·C#대역40·UI47·등록경계9통과, Release0/0. 1번채팅API/DDL수용·검토자확인·원본병합/공유적용은대기입니다. 아래는기존기록으로보존합니다.
