<a id="pr7778-reviewed-20261022"></a>
## PR77 수정 재검토·병합 / PR78 검토·R78-01 보완 요청 — 2026-10-22

[77 최종 통합 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/77#issuecomment-6337145116)은 기존 검토 원문을 보존하고 수정 해결·병합 근거를 앞에 갱신했습니다. [78 통합 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/78#issuecomment-6337428379)은 아래 단일 P2로 미병합입니다. CLEAN/기본 회귀 통과를 부분 실패 시 원선택 보존의 증거로 사용하지 않습니다.

|PR|고정 head/base·최종 상태|tree|
|---|---|---|
|77|최초`57f8c26e7261850c0431c8533407e120337e1a04`→수정`ef84aeddaa7e070ef88a9216c13dd0fc68eaac16`;base`36f1e55d44cd3fa2fd2c7f634fbcd0d059ce7a9d`;Draft→Open→merge`38233949b49ed113db2e0d7e4d60c80ee7e9d22a`|`168fa93366c38dd65e0e15522e432cacbed418eb`; 실제 병합본과 검증본 동일|
|78|head`e0bfbb4ad86ba120e17e01d18dddf99948946f49`;base`36f1e55d44cd3fa2fd2c7f634fbcd0d059ce7a9d`;R78-01/P2 보완 대기·미병합|`e44e33d55f36f18d813ec5bce4bdee4ac3df263d`|

### PR77 — R77-01 해결·직접 재검증과 이전 근거 구분

기존 `_expression`은 따옴표 안까지 소문자화/공백 제거해 `HEX('NOTICE')`와 `HEX('not ice')`를 정상 `HEX('notice')`로 오판했습니다. 수정은 SQL 토큰과 literal 원문을 분리하고, 허용된 표현식의 AND/OR 의미 구조를 대조해 문자열 바이트·식별자/토큰 경계를 보존합니다. 정상015와 MySQL 재표현을 허용하고 의미가 다른 literal·우회 표현식을 거절하도록 확인했습니다.

|검사 밴드|실제 근거·합산 경계|
|---|---|
|수정 표현식|원본29 PASS를 별도 독립 실행/정적 대조. 정상식·괄호/연산자·literal case/space/escape/introducer·비허용 토큰 거절|
|수정 물리 checker|원본49 PASS/자식7.55초·격리wrapper24.56초, 자식/wrapper exit0·새 소유MySQL. 기존32를 포함하며, **29는 사전검사로 실행**하므로 수치를 더하지 않음. 정상015·변형 CHECK 거절/실제INSERT·정상복원/덤프의 검증 범위를 따름|
|원 자원첨부108|최초57f8 고정본의 원108 PASS/7.41초(격리wrapper13.42초). ef84에서 원108·015/014·제공부 관련 소스 diff0을 확인해 재사용했으며 수정본 새108 실행으로 표시하지 않음|
|최초32·독립 반례|원32 자식은 PASS/7.69초, 뒤의 외부audit SHOW DATABASES 처리 ValueError로 wrapper exit1은 별도 보존. 독립 반례10.02초에서는 정상notice INSERT1행, NOTICE/not ice는 readiness true지만 INSERT3819/0행으로 옛 결함을 재현. 이 두 반례를 수정 PASS 수에 합산하지 않음|

015는 고정PR76 draft의 주석/공백 정규화 후 동일하고014는 공백-only 변경입니다. ef84의015/014와 원108 관련 소스는 최초77 대비 diff0, 전체7파일 변경 중 수정patch5파일을 대조했습니다. root rawclone clean·git diff --check PASS, 각 소유MySQL/DB/native/runtime 정리를 확인했습니다. 기존LOG의 별도 작성자29/49/108 보고는 그 출처의 증거로 보존하고 이번 독립 재검증과 합산하지 않습니다. 새77에서 C#/브라우저/두PC·공유 운영DB 검증을 수행했다고 표시하지 않습니다.

### PR78 — 기본 회귀·실제 브라우저와 확정 단일 P2

|직접 검사|결과·경계|
|---|---|
|WebChat 원 서버/MySQL|44 PASS·10 deselected/148.90초(격리wrapper163.30초), 각 새 소유MySQL. 제외된 cold/명시CLI SQLite 검사는 실행하지 않았고 SQLite접속시도0|
|CollaborationFiles 원 공지 회귀|55 PASS·8 deselected/159.90초(wrapper172.72초), 별도 새 소유MySQL/원본 그대로 실행·SQLite접속0|
|WebChat 실제 Edge|원18 PASS/errors0·28.22초, 실제loopback Flask/MySQL·서로 다른 새브라우저 문맥. 두물리PC 검사는 아님|
|CollaborationFiles 실제 소비|원33 HTTP/11 C#/9 Edge PASS·pageErrors0, 별도 소유환경. 원본 C# fixture/제품과 실제 브라우저 범위를 구분|
|마감·UI·빌드|마감 관련 원본75/166/21/4 PASS. Notices37·ChatStorage40·ChatAutoReceive32 PASS는 실제WinForms+명시HTTP대역·DB 없음. 전체Release0오류/0경고|
|독립 경계 재현|원 chat.js 등록 handler·HTTP/최소DOM 대역으로 아래 A/B/C 유실을 확인. pureSHA256 fallback은10 입력을 Nodecrypto와 일치 확인; 실제LAN/장비 검사가 아님|

**R78-01/P2 — 채팅 다중 파일 선택이 부분 실패 후 축소됩니다.** [웹 chat.js274–283](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/e0bfbb4ad86ba120e17e01d18dddf99948946f49/RemoteWork.Server/remotework/static/chat.js#L274)는 A/B/C를 차례로 업로드하다 B가 실패하면 C를 시도하지 않습니다. B만 다시 선택해 성공할 때 방 단위 fileErrors를 지워 송신을 허용하고, send177–184는 성공한 A+B ID만 전송합니다. 원 선택의 미완료 C를 완료/명시 제거까지 유지하는 구조가 없습니다. 원 handler 대역에서 `selectedInitially=[A,B,C]`, `fileStartRequests=[A,B]`, `sendEnabledAfterPartialRetry=true`, `sentAttachmentIds=[A,B]`, `unattemptedOriginalCNoLongerTracked=true`를 확인했습니다. [Desktop ChatControl409–426](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/e0bfbb4ad86ba120e17e01d18dddf99948946f49/RemoteWork.WinForms/Features/Collaboration/ChatControl.cs#L409)도 같은 원선택 보존 문제를 독립 소스 대조했습니다. 서버 저장/중복전송·권한 우회 결함으로 확대하지 않으며 웹/Desktop의 미완료 선택 집합과 명시 제거/전체완료 조건 보완을 요청했습니다. 기본18 브라우저 시나리오는 이 부분실패 재선택 경로를 포함하지 않아 PASS와 모순되지 않습니다.

WebChat 최초 Edge는 OS path discovery 환경 제약으로0검사/28.50초에 중단됐으며 실패 보고를 보존했습니다. USERPROFILE/APPDATA/LOCALAPPDATA 문자열만 정상 OS 값으로 복원하고 TEMP/TMP와 명시 user-data-dir는 새 소유runtime 안에 둔 후 원18이 통과했습니다. 개인 프로필/사용자 브라우저에 연결하지 않았습니다. 성공18·자식exit0 이후 외부runtime rmtree의 일시적 PermissionError로 wrapper exit1이 남았으나 정확한 소유경로·종료프로세스·reparse없음을 확인해 뒤정리했고 cleanup-addendum에 제거true/재검사false를 기록했습니다. 최초 실패/성공/정리 보완을 덮어쓰거나 합산하지 않습니다. CollabFiles도 동일 launch환경과 후속 외부runtime 정리 문제를 별도 기록해 최종 정리true를 확인했습니다.

원본 clone·archive export774파일 전후hash 불변/gitclean, 각 DB/MySQL/native/파일루트/runtime 정리 완료입니다. 원본 검사·product·테스트를 수정하지 않았습니다. WebChat 성공 근거는 `remote_work_pr78_audit47_20261022/browser-retry/browser-audit-report.json`·`browser/browser-results.json`·`cleanup-addendum.json`이며, 최상위 browser-audit-report.json은 최초0검사 실패 보고입니다. 원handler 재현은 `js-edges-report.json`, 순수hash10은 그 대역 범위입니다. stop 뒤 기존 파일명/cache 잔류 관찰은 비차단 보조 메모로만 보존하고 새 finding으로 추가하지 않았습니다.

### 인계·운영/정책 미실행

77 작성자의015 적용18:02·43테이블·공유DB/복원 보고는 **작성자 운영보고/이번 검토 미재실행**이며 위 소유DB 검사에 더하지 않습니다. 검사기 결함이 실제 공유015 구조가 잘못됐다는 의미는 아닙니다. PR78 본문18:06의 GET404 보고와, 그 뒤1번의18:16 같은5000에 **e0bfbb4a** 적용(PID34880·new identity OFF·ACK 없음) 보고는 시점별 수신 사실로 구분합니다. 후속 운영보고의head도 R78-01이 지적된 동일e0bf이지만, 저희가 실제5000의 현재 응답/배포/프로세스를 재검증하거나 조작한 것은 아닙니다.

이번 검토/문서 취합에서 실제5000/.env·공유DDL/DB·서비스/flags·계정/키/센서·사용자 프로젝트를 변경하지 않았고 새지문OFF·같은LAN 사설IP+PORT를 유지했습니다. PR58 연차 미합의·실제loader/startup/장비·두물리PC/공유운영·삭제/보존 정책은 [후속 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr7778-boundaries-20261022)와 기존 기록으로 보존합니다. 동시LOG ef84 수정 기록과 기존2번8문서·원문/앵커를 유지하고 새공개MD/브랜치/PR은 추가하지 않았습니다.

<a id="pr77-r77-01-fixed-20261022"></a>
## PR77 R77-01 직접 수정·검증·기존 브랜치 반영 — 2026-10-22

사용자의 PR77 오류 수정 요청으로 [R77-01/P2 지적](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/77#issuecomment-6337145116)을 직접 수정했습니다. 원 head57f8c26에서 [ef84aeddaa7e070ef88a9216c13dd0fc68eaac16](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/ef84aeddaa7e070ef88a9216c13dd0fc68eaac16)로 포크의 기존 `codex/resource-attachment-readiness` 브랜치에 일반 push했고 GitHub PR head의 일치를 확인했습니다. PR base36f1e55d·전체7파일이며 **Draft/OPEN·미병합을 유지**합니다. 본문과 원 검토의57f8c26·32건은 원 제출/검토 당시 근거이며 아래 수정본과 구분합니다.

`_check_tokens`가 문자열 리터럴의 bytes·토큰 경계를 보존하고 SQL 키워드·허용된 식별자·리터럴 밖 charset 도입자만 정규화합니다. 정확한015 원자식만 기존 AND/OR 괄호 AST로 매핑해 `HEX('NOTICE')`·`HEX('not ice')` 등을 정상으로 오인하지 않습니다. 미지원/미종결 구문은 거절합니다. SQL014/015 선언·3번 runtime·제품 권한/기능 등록은 수정하지 않았습니다.

| 이번 직접 확인 | 결과·범위 |
|---|---|
| 표현식 회귀 |29 PASS, DB 없이 정상015·MySQL 표기/괄호 수용과 리터럴 대소문자/공백/backtick/introducer/escape·잘못된 토큰/묶음 거절. 원57 parser에 같은 검사 실행 시NOTICE에서 실패하는 음성대조 확인|
| 실제 물리 검사 |49 PASS, 고정PR76 peer·소유MySQL8.2. 기존32 포함; 8개리터럴변형503, 정상notice/null INSERT성공과NOTICE/not ice의MySQL3819·0행, 정상복원/덤프→새소유DB복원·DDL/전행/readiness 유지|
| 기존 자원첨부 |108 PASS, 원 verify_resource_attachments.py 무수정 실행·실제loopbackHTTP/소유MySQL/파일.4번 권한/소비콜백은시험대역이며 실제4번소비완료로 확대하지 않음|
| 정적·문서 |변경Python2파일AST·diffcheck·문서109/링크1529오류0. 제품/회귀/설명5파일만수정, SQL014/015·3번provider·원108시험은원57blob과동일|

표현식29는 전체49 실행의 사전 검사에도 포함되므로 합산하지 않습니다. 기존32/과거108과 반복 실행도 합산하지 않습니다. 정확 실행시간은 별도 계측하지 않아 추정하지 않습니다. 소유 MySQL/DB/native프로세스/runtime 정리 확인, 운영5000/.env·공유DB/DDL·서비스/ACK·계정·키/장비·사용자 프로젝트는 미변경입니다. C#/수동브라우저·두PC·실제4번UI/운영적용은 이번 미실행입니다. 1번의015실제적용/복원 보고는 수신 상태를 유지하며 이번 수정본의 직접 운영 검증으로 쓰지 않습니다.

[변경파일·로컬 근거](업무_구현검증.md#member2-pr77-fix-evidence-20261022)와 [기술 경계](공통규약.md#member2-pr77-fix-boundary-20261022)를 기존 기록에 취합합니다. 이번 범위의 수정은 완료했으며 별도 병합/정책승인·추가 회신용MD/PR·외부댓글은 만들지 않았습니다.

<a id="pr7576-pr58-review-20261022"></a>
## PR75·76 최종 검토·병합 / PR58 회신 확인 — 2026-10-22

확정 P1/P2 차단 결함 없이 사용자 지시에 따라75 Draft→Open→Merge,76 Open→Merge를 완료했습니다. REST/원격Git 실제 병합본과 검증 결합본 전체tree가 각각 같습니다. [75최종댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/75#issuecomment-6336468832)·[76최종댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/76#issuecomment-6336589781)에 통합 판정을 유지합니다.

|PR|검토head/base·실제병합|실제tree|
|---|---|---|
|75|최초8167d59→최종`bbb1700030e0e7122e8f5d3076d53f77de61e28e`;base`652b21a20b08fd7c4e3d77332a7d219366dac779`;merge`e8945bba039186229af25b3d689f5b021cc2b15e`|`8d040a11522507c8831cb0aa51fd1dee9302bc19` (최종head는동일검증tree의alpha수신)|
|76|head`a6dc89c91d69c7b345d34a3b72762206e1443cee`;PR등록base652b/실제mergebase e8945bba;merge`36f1e55d44cd3fa2fd2c7f634fbcd0d059ce7a9d`|`b4aaff7d5e6fe846ef452208c77a016636863849` (75포함결합과동일)|

### PR75 — 명시 기준 전환·014와 기존 실제writer

현재 활성관리자·원 인증/TX/DB guard에서 업무/배정을 current locking read로 확인하고 기대 버전·배정·담당자·DATE와 canonical run UUID/사유가 맞을 때만 현재 기준 사건/감사를 기록합니다. 기존head는 보존,계획변경409/고아·불일치503,전체manifest는같은TX 실패전파/rollback계약입니다. 과거최초기한 추정·업무버전 변경·자동seed/새HTTP는 없습니다.014 CREATE3는 기존두draft SQL과 주석/줄바꿈 정규화 후 정확히 같습니다.

|직접검사|결과·정확한포함/fixture|
|---|---|
|기준전환|30 PASS/56.67초,기존journal23+신규baseline7 포함;원 관리자·정확문맥/반복·다음변경·직원거절·rollback/고아거절|
|기존마감HTTP|21 PASS/49.47초,별도새소유MySQL·fixture가draftDDL 준비|
|실제2번writer|32 PASS/188.13초,선택필터/감사경계record주입없음·실제제품hook.물리DDLunchanged/changed_tables없음/schema_versions=[1]|
|DDL/빌드|014 CREATE3 정규화동등성·Release0오류/0경고.새014공유설치/복원시험으로확대하지않음|

actual32는원run_mysql_check.py --suite actual을수정없이실행했고 raw002/004/006/009/013·기존draft3테이블/공통fixture를새빈DB에준비했습니다. 실제 생성/기한A→B→A/승인연장·제목/본문/첨부-only·웹준비/실행·재배정/관리자·제출/반려/재작업/재제출/승인·HTTP확인과 record/audit/receipt뒤 전체rollback·성공replay,OFF/결과전용/미연결head 자동baseline금지를검사했습니다. 기존writer/actual32는alpha652b와diff0,원본/export각744파일불변·gitclean·소유DB/native프로세스/runtime정리true입니다.75끝빈줄28/44의CR·EOF형식경고는비차단메모이며 git diff --check 전체PASS로쓰지않습니다.

### PR76 — 자원첨부 공급·기존참조회귀

3번 자원첨부 제공부/웹 어댑터는 실제4번의현재권한·원자원/원TX 콜백을요구합니다. 새108은실제loopbackHTTP·소유MySQL에검토SQL3테이블을준비해PK/동일DB6FK/schema1을확인했으며4번인가/소비콜백은대역입니다. 정식DDL번호/공유적용·실제4번UI/원UUID·W1/감사·보존/삭제정책·LANcrypto는이검사로완료되지않았습니다.

|직접검사|결과·경계|
|---|---|
|새자원첨부|108 PASS,원loopbackHTTP/소유MySQL·권한/소비콜백대역·SQL3테이블|
|기존공유|251 PASS/18.88초,105현재참조회귀포함·실제Waitressloopback/MySQL,업무소비route는fixture|
|기존연결|150 PASS/15.66초,원Flask cookie/CSRF/IO취소·원문복귀/W1 consumer fixture/MySQL|
|기존웹/서버|웹183·서버191 PASS/18.95초,verify_web --skip-csharp의서로다른밴드.실제브라우저/C#아님|
|cold|PASS/2.69초,DB접속시도0/MySQL0/실제SQLite0.원sqlite://설정문자열과접속을구분해커넥터대역차단확인|

76기존회귀는각원본main을1회실행했습니다. 공유/연결/웹은서로분리한3개새MySQL·포트/파일루트이며전체원본/export각747파일불변·gitclean·경고없음·DB/MySQL/native프로세스/runtime정리true입니다. 수치/포함105·반복기록을합산하지않습니다.76에서새C#솔루션/실제브라우저·두PC는실행하지않았고75Release를76신규빌드로재표시하지않습니다.

### PR58 최신 회신·미실행

[1번17:11:39 KST](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309878130)는PR74독립소비수신,[5번17:12:14 KST](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309485288)는연차전체서버권한/잔여일·원UUID결과복구를추가요청했습니다. 그연차분에1번후속답변은아직없습니다. [AttendanceControl](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/36f1e55d44cd3fa2fd2c7f634fbcd0d059ce7a9d/RemoteWork.WinForms/Features/Attendance/AttendanceControl.cs)·LeaveApplicationForm/LeaveDecisionForm은 [UnconnectedLeaveWorkflow](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/36f1e55d44cd3fa2fd2c7f634fbcd0d059ce7a9d/RemoteWork.WinForms/Features/Attendance/LeaveWorkflow.cs)를기본사용해실제저장하지않습니다. 기존병가/공가미차감은유지하고부여기간/휴일·주말/반차/차감·반려복원/중복/과거·연도/반려사유필수여부를새로확정하지않았습니다.

75의같은5000·37→40테이블·기준4·HTTP22·독립복원1은 **작성자운영보고/이번검토미재실행**이며위소유DB검사에더하지않습니다. 이번검토/취합에서실제5000/.env·공유DB/서비스/flags·dump/복원·계정/키/장비·사용자프로젝트를변경하지않았습니다. PR74기본공통퇴근소비완료·새지문OFF·같은LAN 사설IP+PORT를보존하고실제loader/startup·4번binding·PC GUI/두PC/운영readiness와연차계약은 [후속접점](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr7576-pr58-boundaries-20261022)으로분리합니다. 기존원문/2번8문서·앵커를보존하고새공개MD/브랜치/PR은추가하지않았습니다.

<a id="pr74-reviewed-merged-20261022"></a>
## PR74 최종 검토·실제 병합 — 기본 제품 경로 재검증

[최종 통합 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/74#issuecomment-6336089532)의 고정 **4452996cba22cba9af31ab919097012c766906ba**를 검토해 **652b21a20b08fd7c4e3d77332a7d219366dac779**에 병합했습니다. 추가 확정 차단 결함을 발견하지 못했습니다. 초기031 검토 도중 작성자가 실제 기본 adapter로 바꾼445를 올려 병합을 멈추고 새 DLL/새 소유MySQL로 재검증했습니다. 초기 결과를 최신 실행으로 합산하지 않습니다.

| 기준 | 고정 SHA/tree |
|---|---|
|최종 PR head/base|`4452996cba22cba9af31ab919097012c766906ba` / `f542902e3edfa551e33eb97ed81f4ef12eff27c3`|
|실제 alpha merge/tree|`652b21a20b08fd7c4e3d77332a7d219366dac779` / `d527263428820b2b39acef5e42053331b94b5ae4` (최신 검증본과 완전 동일)|
|초기 검토 head/결합 tree|`0319768b5ba1c9aa52c929992642e9609bd5eb86` / `27d15f6d902006ad088d649b6fd7b450b3a98266`|

### 최신 코드·권한·비동기 연결

기존6파일 수정으로 MainForm 기본 overload의 `prepareClockOut ?? PrepareWorkdayClockOutAsync`가 실제 PR73 공통 Prepare/Complete/Finish를 조립합니다. 이전 시험 ClockOutPeer factory/조건부 컴파일은 제거했습니다. 명시 근무 연결은 병합된 WorkdayStateConnection의 현재 서버 값을 projection하며 기본 startup에 자동 설치하지 않습니다.

같은 로그인/근무의 verified/pending/clock_out·원 work/DATE/challenge/OriginalFinalizeId를 feature에서 대조한 뒤 UI 스레드로 factory를 호출합니다. stop callback은 monitor와 진행 중 근무 조회/poll을 차단·대기하고 공통 요청 drain으로 이어집니다. 준비 이후 정오/조회/감시 재시작을 막고, 응답 유실/빌린 폼 재오픈에도 원 operation·원 common owner를 보관해 새 스캔/일반 Session finalize로 우회하지 않습니다. 잘못된 반환 식별값/현재성·늦은 응답을 거절하고 사용자 확인·창닫기 뒤 같은 검증receipt로만 Finish합니다. 기존 정상 Logout/단순 창닫기는 자동 퇴근이 아닙니다.

### 직접 검사 — 포함·선행 범위 구분

| 실행 | 결과·범위 |
|---|---|
|최신445 C#|원 WorkdayActivity51 PASS(기존26 포함), ClockOutOwner11 PASS, 솔루션 Release0오류/0경고. ClockOutPeer 옵션 없이 실제 기본 제품 factory 경로|
|최신445 실제 연결|원 run_check.py **14 PASS/65.28초**, exit0·failed_nodes없음. 기존10 + 실제 정오2 + 실제 공통 퇴근2가14에 포함됨|
|초기031/27d|51·179·115·공통35/12시나리오·Owner11·Release0/0 PASS, 실제14 PASS/64.38초. 초기 기준의 별도 검사이며 최종에 누적하지 않음|

최신 원 prepare_peer.py가445+실제73병합 f542를 외부에 export했고 tree **d527263428820b2b39acef5e42053331b94b5ae4**, 충돌0입니다. 원 runner/테스트를 수정하지 않고 원 C# MainForm/reader/폼/common/relay→원 Flask/AttendanceWorkflow·Pi HTTP/runtime/codec→소유MySQL8.2를 연결했습니다.14의 기존10은 정확 원문/결과 재생·미등록/다른지문·발급유실·현재 취소/회수·재시작capture fence·badMAC·출퇴근/정오 실제근태를 검사합니다.

정오2는 같은 열린 근무의 noon_at/미종료·challenge1/approval2/receipt1·센서검색1·monitor start/stop1을, 퇴근2는 정상/첫 성공응답 유실 후 원 작업 복구·prepare1/검색1·finalize1또는2·auth/logout0·ended/대상AuthSession0·receipt1·challenge1/approval2를 확인합니다. 일반 auth/me는 네트워크 전 차단되고 같은 원 결과는 복구됩니다. UI/HTTP 유실·정오시각 hook은 fixture이며 제품 endpoint/시계를 추가·완화하지 않았습니다.

Windows·소유 Python3.12.14/tzdata2026.5·MySQL8.2 InnoDB/RR에서 실행했습니다. 최신 DLL SHA256 **f0437f21a024e1ba83202388434df204c9a98efeb627a8d8dc2c98e21151713e**, 원본 export대상699파일/외부export700(원699+manifest)·DLL/deps/runtimeconfig/제품assembly 폴더 전후 불변·git clean입니다. MySQL1개/DB 삭제·모든 owned native process 종료·runtime 삭제 모두 true입니다. pytest65.28초/외부wrapper71.38초는 같은 실행입니다. 초기14/64.38초의 원 source/DLL/보고도 별도 보존했습니다.

센서/UART/OLED·monitor·시험 회원/키/매핑/readiness·미리 열린 근무는 대역입니다. 실제 C#·서버/Pi HTTP·DB 코드를 사용했지만 실장비·두PC·공유 운영 검증이 아닙니다. 공통 테이블 ORM 생성/identity 초안DDL은 빈 소유DB에서만 수행한 원 fixture 범위로 공유DDL 적용/보존 증거가 아닙니다.

### 후속·보존

최신 제품은5번 기본 공통 소비를 제공했으므로 아래73의 ‘소비 대기’는 당시 이력입니다. 기본startup/실제 loader·프로필/분리키/회수·보존/복원·Linux readiness·실장비/두PC·공유 활성화는 후속입니다. 실제5000/.env·공유DB/DDL·flags·키/센서·사용자 프로젝트는 이번에 미변경이며 새지문OFF와 같은 LAN 사설IP+PORT 기준을 유지했습니다.

[담당별 접점](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr74-default-consumption-20261022)·[제품 규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/652b21a20b08fd7c4e3d77332a7d219366dac779/RemoteWork.WinForms/Features/Attendance/CLOCK_OUT_CONNECTION.md)을 따르며 [2번 실제 journal 연결·기존32/7/21·sync 보고](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr72-journal-connection-20261022)를 원문 그대로 보존하고74 신규검사로 합산하지 않습니다. 기존2번8문서/앵커·기존 공개 문서를 보존하며 추가 회신용MD/브랜치/PR은 만들지 않았습니다.

<a id="pr73-reviewed-merged-20261022"></a>
## PR73 최종 검토·실제 병합 — 2026-10-22

사용자 “병합 가능 시 Draft→Open→Merge” 지시에 따라 아래 고정본을 검토하고 Draft 해제 후 병합했습니다. 추가 확정 차단 결함을 발견하지 못했습니다. 최신 alpha7692ce4의2번 기한 journal 변경14파일은 보존했습니다. 작성자가 이를 정상 merge한 최종5070911의tree가 마지막 검증 결합tree와 완전히 같아 추가 코드변경 없이 근거를 수신했습니다. 기존73 base7135와 최종 결합본을 구분합니다.

| 항목 | 정확한 기준 |
|---|---|
|PR/최종 댓글|[PR73 통합 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/73#issuecomment-6335730169)|
|최종 PR head / base|`5070911612d038d9e9395a52eaa469cc6623d14f` / `7692ce41cf36e89e6f5881b61efcc7fcb05fe5e5`|
|최초 검토 head / base|`b442b9b20cc21c18f29f1073c2d2b3abeed6904b` / `7135a2933ae9ae6382da28ab6dbef36acbc421ec`|
|원본 head tree|`fd199a2a1e04603350063315071c4ac3cf71111a`|
|최종 검토 base|`7692ce41cf36e89e6f5881b61efcc7fcb05fe5e5`|
|실제 alpha merge / tree|`f542902e3edfa551e33eb97ed81f4ef12eff27c3` / `0a6ee3ad17fb5ed1610088eb065c94957f791248`|

### 코드·권한·수명

현재 로그인과 verified/pending/clock_out 원 도전을 확인하고 일반 요청 lane을 닫은 뒤 in-flight 요청을 취소·drain합니다. 취소는 서버 TX rollback의 증거가 아니며 서버 공통guard/퇴근·AuthSession 폐기 TX가 최종 인가·직렬화를 담당합니다. 늦은 일반401과 실제 복구401을 구분하고 전역 인증우회는 추가하지 않습니다.

work UUID/DATE는 준비 입력으로 고정하고 CompleteAsync의 응답 영수증에서 원 값과 대조합니다. 결과 소유자는 원 challenge/UUID/body만 가진 CompleteAsync, 검증된 최소receipt·확정 cache를 제공하고 최대24시간 RAM 상한을 재시도로 연장하지 않습니다. 로그아웃/서버 변경·같은 계정 새 로그인세대는 옛 소유권을 폐기합니다. 미송신 Dispose와 첫 전송 이후 미확정 Dispose를 구분하고 옛 EndSession/Dispose가 새 로그인을 지우지 않습니다. MainForm은 monitor/poll 종료·대기를 먼저 받고 소유권을 보관해 빌린창 수명과 분리합니다. 같은 소유자의 검증receipt 후에만 타이머/알림 정리·EndSession을 하고 auth/logout을 다시 보내지 않습니다.

### 이번 직접 검사 — 서로 합산하지 않음

| 검사 | 결과·경계 |
|---|---|
|원 ClockOutRecovery.Tests|초기b442에서35검사/12시나리오 PASS, 실제ApiClient+명시HTTP대역: drain/늦은401·응답유실 원문복구·같은token 새세대·동시/취소·폐기|
|원 ClockOutOwner.Tests|실제MainForm11 PASS, 합성loopbackHTTP: monitor종료대기·poll0·빌린창·동일소유자receipt·새로그인 보존, 실제Flask/센서 아님|
|기존 회귀|초기b442의동일 제품 소스에서ApiClient144/29시나리오·WorkdayActivity26·정오/퇴근179·Leave115 PASS, 각 원본 검사와 명시대역 범위|
|실제 원서버·MySQL 결합|최신7692+b442 tree에서 원 C#→loopback Flask/AttendanceWorkflow→소유MySQL **2 PASS/10.87초**, fail/error/skip0|
|Release|초기b442 Release0오류/0경고; 최종 결합본의 C# 제품/검사 소스 동일 확인(추가 빌드 실행 수로 합산 안 함), 외부 artifacts/offline 환경|

실제MySQL2는 원 `test_clock_out_recovery_client.py`를 수정 없이 실행했습니다. 정상 finalize1회와 서버 commit 뒤 첫 성공응답만 유실시키고 같은 원 UUID/body/token으로2번째 finalize해 결과를 복구하는 두 건입니다. 각 원본 검사가 대상 AuthSession0·WorkSession.ended_at 존재·WorkflowReceipt1을 확인했습니다. 동적 `__test/clock-out` route와 회원·매핑·키·스캔/서명은 시험 fixture이며 제품 새 route나 물리 Pi 검사가 아닙니다. 원 fixture의 공통 테이블 ORM 생성/identity 초안DDL 적용은 빈 소유DB 안에서만 수행했습니다.

최종 결합 실행 pytest10.87초/자식12.25초/격리wrapper18.84초는 같은 실행입니다. Server/관련 원본219파일·원DLL SHA256 `63d0a85ea5c7d718f708779b129281fea6b3d6a56ae8dd86eda3fd3905c8bfe9`가 전후 불변, git clean·소유MySQL/DB·전체native process/runtime 정리 완료입니다. 앞선 head-only2 PASS/10.52초(217파일 불변)는 최초 대조로 보존하고 최종2건과 합산하지 않습니다. 최신base와 기존head 사이 C#/identity/auth/원2test/DLL소스 diff0을 확인했습니다. 별도 독립 담당의 drain/취소·권한 경합15검사도 통과했으며 [최종 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/73#issuecomment-6335730169)의 별도 근거를 따릅니다.

### 인계·미실행

[공통 소유권 규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/f542902e3edfa551e33eb97ed81f4ef12eff27c3/docs/DEVICE_IDENTITY_V2.md)의 **5번 verified 퇴근→원 finalize 경계 명시 소비/결합 검증**이 남았습니다.5번 Form/Operation을 대신 교체하거나 기본 경로에 자동 설치하지 않았습니다. 실제5000/.env·공유DB/DDL·flags·키·센서/두PC는 변경·검증하지 않았고 새지문OFF를 유지합니다. 강제 종료/재시작 뒤 RAM복구·정지 monitor의 임의 재시작·실제identity 저장/회수·보존/복원·readiness는 운영 후속입니다. 같은 LAN 사설IP+PORT 기준을 보존합니다.

병행2번 실제 기한 writer 연결32/기존7·21 및 사용자 프로젝트728파일/DOCX7 보존 보고는 [별도 최신 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr72-journal-connection-20261022)으로 보존하며 이번 PR73 신규 실행/검증으로 합산하지 않습니다. [1·5번 인계와 공동 후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr73-handoff-20261022)을 따르고 기존2번8문서 구조/원문/앵커를 유지합니다.

<a id="pr70-72-merged-owner2-connected-20261022"></a>
## PR70·71·72 실제 병합 확인·2번 연결 — 2026-10-22

| PR | 실제 병합 commit | 반영 범위 |
|---|---|---|
| [70](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/70) | cca5b621962a3511cab9c1ae961126197a3d4b3b | 고정2a50535의 사건/확인 저장·서버UTC/HTTP, 이전 P2 해결본 |
| [71](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/71) |30fcb30d78fe7b26a9c57a5e394f0c4b748b8148| 현재 근무 조회/API·C# reader 제공 |
| [72](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/72) |7135a2933ae9ae6382da28ab6dbef36acbc421ec| 근태/정오 감시·연차 화면, 종료 시 미확정 요청 보존 |

GitHub 실제 merged 상태와 alpha의 연속된 merge를 대조해 위7135a29로 작업용 코드를 fast-forward했습니다. 이번에는 이미 병합된71·72를 새 전체 검토/독립 승인했다고 표시하지 않습니다. 1·5번 근무/연차를2번 업무 조건으로 추가하지 않았습니다. 아래 PR70 Draft/미병합·제품 복사 없음은 이전 재검토 당시 이력입니다.

후속2번 실제 생성·기한 수정·승인연장의 journal 호출과 [직접32검사/빌드·미실행](업무_구현검증.md#member2-pr72-journal-check-20261022), [담당별 잔여](공통규약.md#member2-pr72-journal-connection-20261022)를 취합합니다. 공통/다른 담당 제품·공유 DB/운영 ACK는 수정하지 않았으며 실제4번 source·기존자료 전환/확인 정책과 공유 배포는 후속입니다.

<a id="pr707172-merged-20261022"></a>
## PR70·71·72 최종 검토·병합 — 2026-10-22

추가 확정 차단 결함 없이 사용자 지시에 따라 **PR70·71 Draft→Open→Merge**, 이어 PR72를 병합했습니다. 각 실제 merge tree를 검토 결합본과 대조했습니다. 아래 이전 PR70 Draft/보류/미병합 표시는 당시 고정 head의 기록이며 원문을 보존합니다.

| PR | 검토 head | 실제 alpha merge | 실제 merge tree/범위 | 최종 댓글 |
|---|---|---|---|---|
|70|`2a505354c521c0ba23da8ed40da458ebb08affe3`|`cca5b621962a3511cab9c1ae961126197a3d4b3b`|`5f27a06dc4dbcfc797514eb9056dc4e572deb84c` 고정70과 일치|[통합 판정](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/70#issuecomment-6335250250)|
|71|`81eb88060d36767f5a50de74ea9cd40b2f7b52b5`|`30fcb30d78fe7b26a9c57a5e394f0c4b748b8148`|`3a3d1d5aa24878adfb37eac63e4147f1e4cad120` 선행70+검토71 결합|[통합 판정](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/71#issuecomment-6335251016)|
|72|`10f80ffd17673d412db9408328acf8b5a6ca5123`|`7135a2933ae9ae6382da28ab6dbef36acbc421ec`|`e7d92227853ab10c8ded7c1ca3adbd3d1b21d7ba` 선행70/71+검토72 결합|[통합 판정](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/72#issuecomment-6335251407)|

### PR70: 기존 RR P2 해결과 같은 소스의 검증 재사용

head2a50535/base57a9b08의11파일을 검토했습니다. 기존920145는 오래된 인증 RR snapshot 뒤 새 사건을 부재로 읽었으나,수정본은 정확한 부모를 먼저 잠그고 current journal을 읽으며 binding도 task_id를 전달합니다. 원 task_id 없는 신뢰된 호환 경로는 현재 부모를 정렬 잠금하고1000개 초과503입니다. 현재 own·원 배정 actor·원 task/event의 W1 인가와 새 확인의 현재 사건/배정·두버전·상태/취소·발생시각을 분리합니다. 동일 key/body replay는 새 mutate를 실행하지 않으며 옛 성공을 새 사건 확인으로 승격하지 않습니다.

이번 GitHub 고정본의 RemoteWork.Server와 INTEGRATION **211개 파일**이 기존 `remote_work_pr70_recheck_20261022_2a50535/source`와 byte 동일(누락0/차이0),외부 RR 재현3파일도 보존 SHA256 동일했습니다. 따라서 다음 **기존 실행 근거를 재사용**하며 이번에 새로 실행하거나 합산한 수치가 아닙니다.

| 기존 실행 | 결과/정확한 범위 |
|---|---|
|공식 journal/binding|23 PASS/41.72초,소유MySQL8.2·schema1|
|공식 writer 경계|7 PASS/18.72초,실제2번 writer/W1+시험 audit adapter;제품 hook 아님|
|공식 HTTP|21 PASS/53.65초,실제 auth/W1/journal/확인·loopback TCP1건 포함(별도 합산 안 함)|
|이전 RR 회귀 원문|사건1 PASS/5.38초,binding2 PASS/6.93초,키인가1 PASS/5.36초. binding2 중 원래 통과한 helper 대조1은 추가 해결 결함 수가 아님|
|독립 HTTP 경계|8 PASS/17.41초,5상태·취소·관리관계 무권한·서로 다른 두키의 단일확인/감사|
|독립 cold OFF|12 checks PASS,DB driver대역/접속시도0·모듈/route/action/reader/메타정보 미설치;실제DB검사와 구분|

이전 보고서의 고정head/schema1·source unchanged·소유DB/runtime 정리를 확인했습니다. 빈 소유DB에 원 fixture가 raw002/004/006/009와 번호 없는 초안3테이블을 만든 결과이며 기존 공유DDL 전후 불변이나 운영 설치 증거가 아닙니다. 기존 Release 근거 역시 이번70 신규빌드로 쓰지 않습니다.

### PR71: 실제 current-workday 조회·독립 RR 경합

head81eb880/base641bc71의 의존 원본은 현재57a9b08의 identity/auth/db/검사·Infrastructure와 같았습니다. 새 일반 로그인 GET은 현재 actor/session과 근무행을 공통guard/current locking read로 검사하고 새 identity Context·user/work query 선택을 만들지 않습니다. 이전 날짜·없음/종료 근무 및 receipt-only 문맥을 당일 일반 권한으로 바꾸지 않습니다. C#은 정확한 필드/UUID/DATE/UTC/KST·시각 순서와 캡처 접속·401/취소를 검증합니다.

독립 담당의 이번 직접 실행은 원 서버 **9 PASS/36.83초**, 별도 소유MySQL current-RR **2 PASS/12.13초**, 실제ApiClient+명시HTTP대역의 C# **39 PASS**입니다. RR2는 오래된 load_identity snapshot+강한 ORM 캐시 뒤 다른 요청이 실제 정오/퇴근 complete/finalize를 commit하는 경합입니다. 최신 noon_at/noon_due=false를 보고,퇴근으로 정상 세션이 삭제된 이전 요청은401로 차단됐습니다. 각각 소유MySQL/DB/프로세스/runtime 정리를 확인했습니다. 실제 공유계정/센서/두PC 검사가 아닙니다.

### PR72: 정오·근무감시 수명·연차 UI

head10f80ff/base57a9b08의43파일을 검토했습니다. 원 근무/목적·finalize UUID/결과 복구,로그인/근무당 monitor 소유·메뉴 borrow,정오 전 관찰→서버 NoonDue의 한번 안내,조회실패/근무교체/접속폐기 중지 및 정상 Logout 전 drain을 확인했습니다. 연차는 원 대상/신청·결정 UUID와 처리중 닫기·불명 경고/재확인·반려사유 상세를 보존하며 Preview는 RAM 도구입니다.

이번 원본 직접 검사는 **WorkdayActivity26 /로그인·근무179 /ActivityPresence297(12시나리오) /Leave115 /Preview11 PASS**입니다. 실제C#과 명시 reader/monitor/센서/HTTP/workflow 대역이며 Leave115는 본문89+닫기보완26의 최종 원 출력입니다. 고정71 reader를 외부 시험 Link로 연결한 실제 서버·Pi HTTP·소유MySQL·C# **12 PASS/71.74초**는 별도입니다. 결합 Server/Pi253파일·원 reader bytes·DLL SHA 불변·소유DB/프로세스/runtime 정리를 확인했습니다.

ActivityPresence의 최초 Python3.12 시계 기반 실패를 보존하고,원본 제품/검사·동일DLL을 소유 Python3.14.7/QPC·OpenCV5/NumPy 환경에서 실행해297을 통과했습니다. 카메라/detector는 대역이며 실제 장치 검증이 아닙니다. 실패 시도나 재실행을 누적 통과 수에 합산하지 않습니다.

### 최종 결합본과 미실행

실제 alpha **7135a2933ae9ae6382da28ab6dbef36acbc421ec /tree e7d92227853ab10c8ded7c1ca3adbd3d1b21d7ba**에서 root가 Release **0오류/0경고**, WorkdayActivity **26**·WorkdayState **39**를 직접 재확인했습니다.71의 StartedAt 포함 상태와72의5필드 상태는 명시 projection wrapper로 캡처 접속/취소를 보존해야 합니다.

실제5000/.env·공유DB/DDL/운영 flags·키·센서 변경은 없습니다. 새지문OFF·기존ACK 유지,2번 실제 writer hook·4번 새 binding 어댑터·baseline/중간상태/옛 성공 열람 정책·정식 DDL/공유 적용·퇴근 finalize 중 일반요청 drain/401 이후 결과전용 수명·실제 monitor/장치/두PC·연차 실제 API/권한/잔여일은 후속입니다. [담당별 다음 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr707172-merged-20261022)를 따르며 코드 병합·정상 Logout을 필수 지문 퇴근/운영완료로 확대하지 않습니다. 추가 회신용 공개 MD/브랜치/PR은 만들지 않았습니다.

<a id="pr70-rechecked-20261022"></a>
## PR70 수정 재검토 — 이전 P2 해결, 2026-10-22

고정 head **2a505354c521c0ba23da8ed40da458ebb08affe3**, base **57a9b08c35c0c3a5b6aae6249fa97074211500d1**의11파일입니다. 기존 journal/binding·RR 수정과 추가 HTTP/확인 SQL·기동/옵션·검사·안내를 함께 검토했습니다. 현재 Draft/미병합이며 원본 alpha/실제 서버로 복사하거나 병합하지 않았습니다.

**최종 검토 판정:** 이전 P2는 해결됐고 이번 범위에서 추가 병합 차단 결함은 발견하지 못했습니다. 아래 실제 검사 범위와 미확정 정책/공유 적용은 별도로 유지합니다.

**이전 P2 해결:** `read_event(...,task_id=...)`는 정확한 업무 부모를 먼저 잠근 뒤 current 사건을 읽고 `read_bound_event`도 이 경로를 사용합니다. task_id 없는 기존 내부 호출은 정렬된 현재 부모 최대1000개를 잠근 뒤 current journal을 읽으며 초과는503입니다. journal→부모 순서 역전이나 오래된 consistent snapshot의 부재 추정이 없습니다. 이전 외부 재현3파일의 SHA256을 유지한 채 새 소스에 실행해 사건 조회1·binding2·키인가1을 모두 통과했습니다. binding2 중 내부 issue_key 단독1개는 원래 통과하던 대조군이며 새 해결 건수로 합산하지 않습니다.

| 직접 실행 | 결과·범위 |
|---|---|
| 원 journal/binding |23 PASS/41.72초, 명시 소유 MySQL8.2·schema1|
| 원2번 writer 경계 |7 PASS/18.72초, 실제 writer/W1+시험 audit adapter이며 제품hook 아님|
| 이전 독립 재현 원문 |사건1 PASS/5.38초·binding2 PASS/6.93초·인가1 PASS/5.36초, 각 새 소유DB|
| 새 HTTP |21 PASS/53.65초, 실제 auth/W1/원장/확인 저장·loopback TCP1건 포함. TCP건을 별도 합산하지 않음|
| 독립 HTTP 경계 |8 PASS/17.41초, 5상태·취소·managed-only 거부·서로 다른 두 키 동시 확인|
| 기본 OFF 기동 |12 checks PASS, route/action/reader 미등록·deadline 모듈 미import·DB 연결/DDL 시도0|
| Release/diff |경고0·오류0, diff check 통과·검토 소스 clean|

각 MySQL 실행은 **빈 소유DB에 원 fixture가 raw002/004/006/009와 해당 draft SQL만 준비**했습니다. 실제 .env·공유 DB·계정/키·운영 ACK를 사용/변경하지 않았으며 모든 소유DB/프로세스/runtime 정리를 확인했습니다. 루트 `tmp/remote_work_pr70_recheck_20261022_2a50535/`의 `rr-recheck-summary.json`, `http-review-summary.md`, `cold-off-report.json`과 각 `verification.json`을 근거로 보존했습니다. 이전 검사 수치와 합산하지 않습니다.

독립8개는 제출/승인 상태409·현재취소409와 무receipt, 지정 관리자라도 현재 본인 배정이 아니면 목록[]/키·결과·confirm403, 서로 다른 두 키 동시 확인에서 확인행1·감사1·동일시각·각 W1receipt2 및 업무버전 불변을 확인했습니다. 추가 외부 검사기의 최초 모듈 검색경로 누락1수집오류·fixture 재노출 누락8setup은 외부 하네스만 교정한 뒤 통과했으며 공급 제품 실패나 성공으로 합산하지 않습니다. 근거는 같은 루트의 `http-boundary-final/retry-test_http_boundaries_independent/verification.json`입니다.

**담당별 다음 경계:** 2번은 실제 생성·기한 수정·승인 연장의 원 W1 mutation에서 기록 함수를 명시 연결하고 rollback/replay를 검증해야 합니다. 4번은 새 binding/receipt·성공/불명 cache와 로그인 wrapper의 소비를 연결해야 합니다. 기존 업무 baseline·중간상태·옛 성공의 최종 정책·정식 DDL 번호/백업복원/공유 적용은 공동 협의사항입니다. 이번 코드 재검토를 그 정책의 확정이나 운영 설치 승인으로 사용하지 않습니다. 새 HTTP 기본off·3테이블 자동생성/seed 없음·지문OFF/기존 취소연장ACK 유지와 내부망 사설IP:PORT 기준을 확인했습니다. 공유 서버/두 PC·4번 C# 새 계약 소비·운영 배포는 미실행입니다.

> 아래920145의 ‘수정 필요’는 이전 고정본 검토 이력입니다. 현재 수정본의 해당 P2는 위 재현으로 해결됐습니다.

<a id="pr70-reviewed-20261022"></a>
> **최종 상태: PR70의 RR P2는2a50535에서 해결됐고 cca5b621에 병합됐습니다.** [최종 병합·검토](#pr707172-merged-20261022)와 [공개 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/70#issuecomment-6335250250)을 따르며 아래920145 보류 원문은 당시 이력입니다.

## PR70 최신 6파일 검토 — 수정 필요, 미병합

고정 head **920145ccc631b0308779b153d19bfe8095fb4b43**, base **a37a3c41bc835e8bfae393a99245b643793de3b4**입니다. 실제 사건 journal·원 배정 binding·번호 없는 SQL·두 검사 모듈·공통 안내6파일을 읽고 독립 소유 MySQL에서 검증했습니다. Draft/mergeable 상태와 기능 검증 결과를 구분합니다.

**[P2] 인증의 오래된 RR snapshot으로 현재 보관 사건을 없다고 판정합니다.** [`deadline_events.py:133`](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/920145ccc631b0308779b153d19bfe8095fb4b43/RemoteWork.Server/remotework/deadline_events.py#L133)의 journal→task 사전 탐색이 일반 SELECT입니다. `load_identity()`가 만든 MySQL REPEATABLE READ snapshot 뒤 다른 guarded TX가 실제 기한 사건을 commit하면, 원 요청의 guard 획득 후 `read_current()`는 새 사건을 읽지만 `read_event(같은 UUID)`는 None을 반환합니다. 소스 무변경 실제 회귀에서 `None != DeadlineChange(...)`로 **1 FAIL /5.61초**를 재현했습니다. 권한 우회·저장 손상을 재현한 것은 아닙니다.

**1번 수정 요청:** 과거 consistent snapshot을 사용하지 않고 현재 부모/사건을 판별하도록 수정해 주세요. 기존 guard→업무 부모→사건 잠금 순서를 유지해야 하므로 journal SELECT에 무조건 `FOR UPDATE`만 붙여 순서를 뒤집는 방식은 별도 대조가 필요합니다. 실제 오래된 인증 snapshot→다른 TX commit→현재 사건/보관 사건·원 배정/W1 인가의 동일성 회귀를 포함해 주세요. 2번은 다른 담당 파일을 대신 수정하지 않으며 수정/재검토 전 runtime 연결·병합하지 않습니다.

같은 조건의 추가 공개 경로도 확인했습니다. `current_binding`은 새 UUID를 반환하지만 `read_bound_event`는404 `DEADLINE_EVENT_NOT_FOUND`를 반환하고, 실제 키 발급 route와 같은 `_authorize(db,target,_authorizer(action)) → issue_key` 경로도 인가 단계에서404로 실패합니다(독립1FAIL/6.78초). 내부 `issue_key`만 호출한1PASS는 원래 인가를 수행하지 않는 helper 대조이며 route 성공으로 쓰지 않습니다. 같은 원인의 파생 재현을 서로 다른 결함 세 건으로 세지 않습니다. 추가 근거는 `retry-test_deadline_binding_authorizer_rr_regression/verification.json`에 있습니다.

원 공급 `test_deadline_events.py` **22 PASS/45.29초**, `test_deadline_event_writers.py` **7 PASS/20.92초**입니다. 후자는 원2번 writer와 시험 audit adapter의 결합이며 제품 hook 완료가 아닙니다. 최초 리뷰 실행은 runner가 rawDDL을 미리 만들고 원 fixture가 다시 만들면서22개 중4PASS/18setup error·writer7setup error가 났습니다. 빈 소유DB에서 원 fixture가 직접 준비하도록 runner만 교정했고 원 제품/검사는 수정하지 않았습니다. 이 setup 실패와 실제 RR 회귀 실패를 구분합니다.

격리 소스/재현/보고서는 `tmp/remote_work_pr70_audit_20261022_920145/`의 고정 source와 `retry-test_deadline_events/`, `retry-test_deadline_event_writers/`, `retry-test_deadline_event_rr_regression/`에 있습니다. 모두 MySQL8.2·raw002/004/006/009와 번호 없는 journal 초안, schema1·소유 DB/프로세스/runtime 정리를 확인했습니다. SQLite·공유 서버/DB/.env·운영 ACK/설치·두 PC는 사용/변경/검증하지 않았습니다. 새 binding/wire/확인 SQL·baseline 정책은 여전히 공동 검토 제안입니다.

<a id="pr69-reviewed-merged-20261022"></a>
## PR69 검토 — 지문 출근·로그인 연결/연차 화면, 병합 완료

head **80af53fff0705e73e0c186ccec060a06091c4424**, base **cb2114113a7a7dea8709bc15376ce9e4d4d917fd**의35파일을 독립 검토해 추가 병합 차단 문제를 발견하지 못했습니다. [alpha 병합](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/78581f21a61bd14de97a719136c2a5f067d4e1f7)의 실제tree **fe3c3842f3f9688ebabb3b4bf166423e252deb99**가 검증본과 정확히 같습니다. [최종 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/69#issuecomment-6333291195)을 대표 근거로 유지합니다.

정확한409/IDENTITY_FINGERPRINT_REQUIRED만 제한 로그인으로 전환하고, 원 finalize UUID/receipt·provisional auth/me를 확인한 뒤 원 로그인 세대에서 정상토큰을 인수합니다. verified만으로 업무를 열지 않습니다. 같은 UUID 복구·구로그인/같은문자열다른세대·취소/닫기·동시Complete와queuedRevoke를 대조했고, 연차 기본 미연결 화면은 저장 성공으로 표시하지 않습니다.

| 이번 직접 검사 | 결과/범위 |
|---|---|
| 공식 로그인 UI |77PASS, 실제 C# 폼/ApiClient·명시 HTTP/센서 대역|
| 독립 로그인/공통 회귀 |복구·취소·동시27PASS; 기존ApiClient144PASS/29시나리오. 별도 실행 수치|
| 신규 연결·연차/기존pending |Connection88·Leave57·IdentityPending43PASS, 각각 명시 대역/실제 C#|
| 실제 서버/Pi HTTP·MySQL·C# |13PASS/61.69초, exit0. source export249파일·고정DLL SHA 일치, 소유DB/프로세스/runtime 정리true|
| Release/diff |제품 참조 포함 오류0·경고0, diff check통과|

최초 긴중첩 임시경로 실행은 MySQL3913/연쇄1050 setup오류로 실패했고 별도 보존했습니다. **소스/DLL/fixture 변경 없이** 짧은 새 소유TEMP에서13PASS를 확인했으며 실패 시도·공식/독립/이전 수치는 합산하지 않습니다. UART/OLED·센서·계정/키/readiness는 대역입니다. 실제5000/.env·공유DB/DDL·운영옵션·장비는 미변경이며 **새 지문OFF 유지**, 실물/두PC·운영 AttendanceWorkflow·정오/퇴근/활동 감시와 연차 실제 HTTP/정책/저장·검토는 후속입니다.1번 보고의 공유서버 갱신을 이번69 실제 배포로 확대하지 않습니다.

<a id="pr68-merged-20261022"></a>
## PR68 최종 재검토 — R68-01 해결·병합 완료, 2026-10-22

수정 head **194c472f61c6573f237f8e9d26067345bfeaa9aa**에서 A의 원래 업무·동작에 묶인 성공 안내와 B의 미확정 상태가 구분됨을 확인했습니다. 공식 JS **83PASS**, 독립 해결 회귀 **20PASS**이며 같은 검사는 이전 fec2364 원본에서 실패합니다. JS 구문·diff check도 통과했습니다. 추가 병합 차단 문제가 없어 [PR68](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68)을 alpha **ff32ba225f9079332e83d568fc545acb595b623f**에 병합했습니다. 실제 tree **b813e2d6e0a27199ad51cf6fcb7efed99b11852f**를 확인했습니다.

서버·템플릿·C# tree가 이전 검증본과 동일해 기존 연결150·공용251·lifecycle258·서버191·웹183·C#43/독립198·Release0오류/0경고 근거를 유지합니다. 이 수치는 이번 JS 수정 재실행 수치로 합산하지 않습니다. [최종 검토 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68#issuecomment-6331304609)을 따르며, 아래 fec2364의 R68-01 보류/미병합은 당시 이력입니다. 2번 실제 웹 전송·일반수정 첨부 writer 소비와 공유 환경 검증은 후속입니다.


<a id="pr54-current-facts-delivery-20261022"></a>
## PR54 기존 요청 후속 — 현재 사실 제공, 2026-10-22

PR54는 기존 문서 병합 상태를 유지합니다. 별도 회신 PR 없이2번 내부 현재 본인 업무 reader와 단건 재검사를 alpha **dc525d08312eb6d31c4c9e56aa6367a71103dfb9**에 제공하고 [기존 통합 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/54#issuecomment-6305519634)로 회신합니다. 소유 MySQL52PASS와 [검사 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#member2-own-deadline-facts-check-20261022), [1/2/4 사건·초기자료·무효화·API/확인 잔여 계약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-own-deadline-facts-20261022)을 구분합니다. 정책 재확정/실제 알림 연동 완료로 표시하지 않습니다.

<a id="pr68-review-20261022"></a>

> **최종 상태: PR68은 R68-01 해결 후 ff32ba2에 병합됐습니다.** [최종 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68#issuecomment-6331304609)을 따르며 아래 보류 표시는 이전 head 당시 기록입니다.

## PR68 전체 검토 — R68-01/P2 수정 요청·미병합, 2026-10-22

추가 PR 조회에서 새 PR68 한 건을 확인했습니다. 실제 base alpha **5dcd66e291a0b12470c3347909653a64458ef70e**, head **fec2364b8caf9ba8c4ac43a0052f02f3af479223**를 고정하고 별도 checkout에 결합했습니다. 검토 tree **41120bc743f3f57ebaa96e7d454f51094a7dec5a**, 변경13파일입니다. 정적 권한/현재 잠금·트랜잭션·W1 replay/rollback·IO 전후 참조 재검사·서명 문맥·내부 base64 converter를 대조했습니다.

### R68-01/P2 — 이전 성공과 새 미확정 작업의 안내가 섞임

results.js243~244행은 업무/동작 구분 없이 일반 저장 성공 안내를 표시하며 choose137~147·prepareWrite233~238·응답 유실 catch252~261에서 다음 작업과 구분하지 않습니다. 원본 JS/명시 DOM·HTTP 대역에서 **A(10)/KEY1 제출 성공 → B(11)/KEY2 제출 응답502 유실 → B 모달 닫기**를 재현했습니다. 현재 상세/제출 문맥은B이며 pending은 결과 확인 필요인데 save-confirmed가 visible인 채 “서버 저장 성공을 확인했습니다”를 계속 보여 줍니다.

이는 현재 작업 성공 여부의 UI 혼동입니다. 서버의 B 성공 오판/잘못된 대상 전송·중복 저장이 발생했다고 확대하지 않습니다. A의 확인된 성공을 업무번호·동작/필요 시 회차에 묶어 별도로 보존하고 B의 미확정 안내와 구분하도록 요청했습니다. 동일 작업의 성공 뒤 목록/상세403에서 원 성공 유지, 원 요청/W1 key/body 보존·명시 결과 조회/재시도도 유지해야 합니다. [기존 PR68 통합 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/68#issuecomment-6331304609)에 요청했으며 **R68-01 수정 전 미병합**입니다.

### 직접 실행한 검증

| 범위 | 결과·한계 |
|---|---|
| 연결 제공부 | 150PASS·실제 소유 MySQL/Flask cookie/CSRF handler·서명 복귀/다중탭/구접속·현재 ID·W1 replay/rollback·IO중 권한403/참조404와 B보존. 업무 writer는 명시 소비 fixture |
| 공용 첨부 | 251PASS·실제 별도 소유 MySQL/loopback HTTP·오래된 RR snapshot 이후 현재 참조. 내부105회귀는251에 포함 |
| 취소/연장 | 258PASS·다른 새 소유 MySQL/실제2·3번 loopback·파일/경합 검사 |
| 기존 서버/웹/C# | 원본 verify_web 경로 191/183/43PASS·별도 소유 MySQL·원본 C#143소스 컴파일 Errors0/Warnings0 |
| C# 독립 | 198 assertions PASS·원본 transport/ApiClient+HTTP 대역. plus-heavy0~32769바이트·16KiB 경계/다중청크·offset/원 bytes/hash·파일명 escaping·Bearer·취소/접속 폐기 |
| JS | 원본 구문·공식 DOM/HTTP61PASS. 독립10확인은 현상 검출7+읽기 전용 늦은 응답 안전성3으로 구성되며 해결 증거/실제 브라우저 시험으로 표시하지 않음 |
| OFF/정적/빌드 | 원본 cold assertions의 DB URL을 audit wrapper에서 MySQL engine으로 바꾸고 Engine.connect를 금지해 결과물 경로/모델/provider/모듈 미등록 확인. SQLite/실제DB접속 없음. 변경Python6파일 AST·diff check 통과, 최종 결합 Release0오류/0경고·외부 참조팩 설정/제품csproj 무수정 |

connections/shared/lifecycle은 각각1회·exit0이며 세 MySQL/DB/native프로세스/runtime 정리를 확인했습니다. 웹/C#의 소유 DB/프로세스/inner·outer runtime도 정리했습니다. 작성자/이전 반복 수치나 shared 내부105를 합산하지 않습니다. 공식 검사 통과가 위 새 UI 재현을 상쇄하지 않으며 등록 GitHub status/check-run은0건입니다.

### 제공/소비와 실제 적용 구분

T3-1~5 제공부 코드는 수신했으나 PR68은 아직 미병합입니다.2번 실제 웹 own/read 링크의 원 필터 전달·첨부 UI·일반수정의 전후 집합/no-change·감사/버전/receipt 소비와 shared 저장소/관계·같은 배포ACK·실계정/두PC/실제 브라우저 전체 동작은 후속입니다. 이 제공만으로 TASK_ATTACHMENT_REPLACEMENT_NOT_READY를 해제하거나 미합의 정책/새 자동 제출을 추가하지 않았습니다.

기존 PR/LOG/통합 댓글에 취합하고 회신용 새 MD/브랜치/PR은 만들지 않았습니다. 실제5000/.env·공유 DB/DDL·운영 키/계정·장비·제품 workspace 배포는 변경하지 않았습니다.1번의006/009/013 실제 적용 보고 수신과 이번 소유 시험 검증도 구분합니다.

<a id="pr67-fixed-merged-20261022"></a>
## PR67 수정 재검토·병합 — 실행기 실패 정리 R67-01 해결, 2026-10-22

추가 열린 PR 조회에서 새 PR은 없고 PR67의 수정 head **d9500fec6df9bc0e4797869bee92291345a5fa1f**를 확인했습니다. 최신 실제 원본 alpha **f534b68933c35d9c48a613c2269eebab4d8411ab**에 고정 결합한 tree는 **4fafbaa04286356832a3959eddc5d536d3cef99a**입니다. f534는2번 C# 등록 첨부 UI·업로드·검사/문서24파일이며 서버/Pi 코드는 변경하지 않았습니다. PR67은 Tests/FingerprintIdentity.Server.Tests 아래5파일만 추가합니다.

### R67-01/P2 — 해결 확인

신규 runner의 자체 Popen/Job 수명 관리를 제거하고 capture_pytest가 기존 run_owned_client에 위임합니다. 기존 공통 helper는 process/job=None부터 Popen/assign/스레드 재개 이전의 finally 보호를 적용해 Job close→소유 자식 stop/wait→stdout/stderr close를 보장하며 helper 자체는 변경하지 않았습니다. timeout은 bytes/None 출력을 문자열로 정리해 timed_out/exit1로 보고하고 자동 재실행하지 않습니다.

- 공식 unittest 실패/정상 **7검사 PASS**를 ResourceWarning=error로 직접 실행했습니다. assign 실패·등록 중 취소·등록 뒤 resume 실패·통신 중 취소·timeout·정상 종료·Popen 실패의 종료/회수와 스트림 닫힘을 확인했습니다.
- 이전 main 전체 경로 assign 실패 대역을 수정본에 적용해 **spawn1/assign1/job.close1/stop1/terminate1/wait1·child_alive=False·stdout/stderr closed=True**를 독립 확인했습니다. 이 실패 주입은 실제 프로세스/DB를 만들지 않았고 정상 MySQL 결과와 분리합니다.
- 수정 runner의 실제 소유 MySQL/두 loopback HTTP **10 PASS/63.01초, exit0·소유 DB/프로세스/runtime 정리 완료**. 실패/생략0이며 소유 MySQL 종료·DB 삭제·runtime 삭제를 확인했습니다. 이전10검사·작성자 보고·실패 주입7개를 같은 통과 수로 합산하지 않습니다.
- 고정 export alpha9c5c07c+server95ef89d의 서버/Pi는 최신 원본f534와 각각 **835cc2ada917d87d9454ec51639d03963758cb9d / ad967ff2c6b11f2f10bd13f08ee3f9bff2f4822a**로 정확히 같습니다. prepare_source/test_server_pi도 이전 검토본과 같은 blob이며 source manifest의 commit/tree·충돌 파일 hash 범위를 전체 export hash 보장으로 확대하지 않습니다.
- 최신 결합 솔루션 Release **0오류/0경고**, diff check 통과. 설치 참조팩용 외부 설정을 사용하고 제품 csproj는 변경하지 않았습니다. 등록 GitHub status/check-run은0건으로 CI 통과로 표시하지 않습니다.

추가 차단 문제를 발견하지 못해 실제 merge **[5dcd66e291a0b12470c3347909653a64458ef70e](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/5dcd66e291a0b12470c3347909653a64458ef70e)**로 병합했습니다. fetch 후 부모가 최신base/head이며 실제 tree가 위 검토 tree와 일치함을 확인했습니다. [기존 PR67 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/67#issuecomment-6330831748)을 최종 판정으로 갱신했습니다. 아래 R67-01 미해결/미병합 표시는 이전 head 검토 이력입니다.

### 적용 범위와 후속

이 검사는 실제1번 identity/근태 MySQL 코드와5번 Pi runtime·비교/서명을 loopback TCP로 연결합니다. 센서·키/계정·readiness는 대역이며 실제 C# 접속/화면·정상 토큰 UI 인수·Pi/센서·Linux ACL/BOOTTIME·운영 재시작 준비·실계정/두PC는 후속입니다. 합성 회원의 소유 시험DB 근태 성공을 실제 사용자 출퇴근/운영 ACK로 쓰지 않습니다.

기존 PR/LOG/통합 댓글에 취합하고 새 회신용 MD/브랜치/PR은 만들지 않았습니다. 실제5000/.env·공유DB/DDL·운영 키/계정·Pi/센서·제품 workspace 배포는 변경하지 않았습니다. 미합의 정책을 새로 확정하지 않습니다.

<a id="pr66-pr67-review-20261022"></a>
## PR66·67 신규 검토 — 66 병합, 67 R67-01 수정 대기, 2026-10-22

열린 PR 전체 조회에서 새 PR66/67을 확인했습니다. 기본 원본 alpha **a6427adf221074d27df143c02f3386b2ed073642**를 고정하고 각 head를 별도 격리 checkout에 결합해 코드·검사를 독립 대조했습니다. GitHub status/check-run은 각0건이며, CLEAN/MERGEABLE을 검사 통과로 대신하지 않았습니다.

### PR66 — 마감 알림 확인 성공과 목록 재조회 분리, 병합

head **b8825dd108b4194990fdac783f6854bbff092f8b**, 검토한 결합 tree **8723d2a270dd03bd15f3d8bdc4f096c25b733ab5**입니다. 성공 receipt는 정확한 사건/계정·원 W1 operation/key/body에 묶어 source가 검증한다는 내부 계약을 유지합니다. 성공 뒤 snapshot 없음/불량이면 성공을 보존하고 현재 facts/팝업·업무 이동을 비워 GET 재조회가 필요함을 표시합니다. 로그인당 정확한 사건만 기억해 재POST/불필요한 결과 조회를 막으며 재로그인/새 사건에는 승계하지 않습니다. 불명 결과·401/403·동일 source 수명·늦은 응답 경계를 대조했습니다.

- 최신 결합본에서 공식 DeadlineAlerts **162PASS**, DeadlineNavigation **24PASS**, Notices **35PASS**, CollaborationRequests **115PASS**를 각각 직접 실행했습니다. 실제 WinForms/명시 source·HTTP 대역이며 실서버/DB·물리 두PC 검사가 아닙니다.
- 별도 독립 검증: **화면 수명·경합 독립19검사 PASS**. 공식 검사와 반복 합산하지 않습니다.
- 최종 솔루션 Release **0오류/0경고**, diff check 통과. 설치 참조팩용 외부 설정으로 실행했고 제품 csproj는 변경하지 않았습니다.
- 추가 차단 문제를 발견하지 못해 실제 merge **[8cc082155561ea060e3676c346aed1d87fb8e5e9](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/8cc082155561ea060e3676c346aed1d87fb8e5e9)**로 병합했습니다. fetch 후 부모와 실제 tree가 검토본과 일치함을 확인했습니다.

**남은 연결:** 2번의 현재 업무·실효기한·배정/상태·실제 사건/초기자료 reader,1번의 인증/TX·확인/감사/W1·최종 API/DB·OFF 결과 reader,4번 실제 source adapter·공유 저장/경합/두PC 검증은 계속 후속입니다. 이번 내부 port 보완이 endpoint/DTO/DDL·migration/새 정책 확정이나 실제 마감 알림 가동을 뜻하지 않습니다.

### PR67 — 실제 서버·Pi HTTP/MySQL 검사, 수정 요청

head **a4b9d58efd2c65139582aec71e63a3ae8d75cb45**, 원본a642와 결합 tree **ac88e3ab3734f8b914a0e3506837fe02ef501c93**입니다. 새4파일이며 실제 두 loopback HTTP·서버 MySQL identity/근태·Pi runtime/비교/서명을 사용하고 센서/키·계정/준비상태는 시험 대역입니다. 고정 export는 alpha9c5c07c + server95ef89d이며 현재 병합된 서버/Pi 코드와의 동일성을 대조합니다. source manifest는 고정 commit/tree·충돌 파일 hash 기록이며 전체 export hash 검증으로 확대하지 않습니다.

**정상 경로의 독립 결과:** 독립 소유 MySQL/두 loopback HTTP 10PASS/55.61초, exit0·소유 DB/프로세스/runtime 정리를 확인했습니다.

**R67-01/P2 확정:** 신규 `run_check.py`52~56행의 Popen(CREATE_SUSPENDED)·job.assign이57행부터의 try/finally 밖입니다. AssignProcessToJobObject 실패 때 자식이 Job에 미소속이므로 Job close로 종료되지 않으며 stop_owned_process에도 도달하지 않습니다. 독립 대역에서 spawn1/assign1/job.close1/**stop0/child_alive=True**, DB/runtime cleanup True 보고를 확인했습니다. 실패경로 재현에는 실제 자식·MySQL·서버를 생성하지 않았습니다. 기존 공통 helper의 _spawn/run_owned_client는 이 경계를 이미 보호하므로 신규 실행기 범위의 문제입니다.

요청: child=None부터 생성/등록 이전의 try/finally로 종료/회수·stdout/stderr 닫기를 보장하고, assign 실패/취소/timeout·정상 완료의 회귀와 정상10검사를 유지해야 합니다. [PR67 통합 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/67#issuecomment-6330831748)에 요청했습니다. **R67-01 수정 전 미병합 유지**이며 정상 연결 검사가 이 정리 결함을 상쇄하지 않습니다.

### 공통 범위

기존 PR/LOG와 통합 댓글에 취합하고 새 회신용 MD/브랜치/PR은 만들지 않았습니다. 실제5000/.env·공유 DB/DDL·운영 키/계정·Pi/센서·제품 workspace 배포는 변경하지 않았습니다. 코드 병합/소유 시험 성공과 실제 운영 연결은 구분하며 미합의 정책을 대신 확정하지 않았습니다.

<a id="pr62-merged-20261022"></a>
## PR62 최종 재검토·병합 — R62-01·R62-02 해결, 2026-10-22

사용자의 수정 확인·전체 재검토 후 문제없으면 병합 요청에 따라 최종 head **95ef89dfa8d9478e0cb6c3cb26282cd43c09a820**를 고정했습니다. 추가 병합 차단 문제를 발견하지 못해 alpha에 병합했습니다. 실제 merge [a6427adf221074d27df143c02f3386b2ed073642](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/a6427adf221074d27df143c02f3386b2ed073642)의 부모는 **9c5c07c9aa6757c83a6d52a0c01cf7bcc3b85fc6 / 95ef89dfa8d9478e0cb6c3cb26282cd43c09a820**, 실제 tree는 **84c01fb8ec05cd59241cbaf1ccfe3367a79d4663**입니다. 실제 tree와 검토한 index/merge-tree 간 차이가 없음을 fetch 후 확인했습니다. GitHub 등록 status/check-run은 각각0건으로 CI 통과와 구분합니다.

### 해결 확인

| 항목 | 독립 재검토 결과 |
|---|---|
| R62-01/P2 | 현재 DB의 referred_schema(None/현재 DB)를 정규화하고 외부 DB의 동명 부모를 거절합니다. 컬럼 순서·부모 테이블/컬럼 검사는 유지됩니다. 소유 MySQL/원본 HTTP에서 정상FK200 → 현재DB 명시FK200 → 외부DB 동명users FK503 IDENTITY_STORAGE_NOT_READY → 정상복구200을 확인했습니다. |
| R62-02/P2 | 최초 finalize 송신 전에 challenge/원 finalize UUID·최초24시간 상한을 RAM에 고정합니다. 첫 응답 유실 후6분이 지나도 기록된 challenge GET·같은 UUID/동일 본문 finalize로 결과를 복구합니다. 새 인증 활동·다른 challenge/UUID는 HTTP 전에 차단합니다. 현재 로그인 세대·외부 취소·Dispose·401·늦은 응답 차단, 동시 다른UUID 차단과 재조회로 연장되지 않는 상한을 확인했습니다. |

미완료 pending의 서버 만료401·현재 권한/원 UUID·결과 보관기한은 계속 최종 기준입니다. 신규 활동 전체를24시간 허용하거나 새 UUID/새 채취로 대체하지 않습니다.

### 직접 실행한 검사와 재사용 범위

- 최신95 pending 공식 **43검사 PASS**, 기존 실패 사례를 바꿔 실행한 독립 복구·동시성/기한 검사 **20검사 PASS**입니다. 합성 HTTP로 확인했으며 실제 C#↔Flask/MySQL/Pi 종단 검사로 표시하지 않습니다.
- R62-01 소유 MySQL/원본 HTTP **1PASS/4.99초**와 인증/codec **120PASS**는447에서 직접 실행했습니다.447→95 전체 서버 tree가 **835cc2ada917d87d9454ec51639d03963758cb9d**로 정확히 같아 이 근거를 재사용했습니다. 첫 audit DDL은 동일 FK 이름 DROP+ADD를 한 ALTER에 넣어 제품 HTTP 전에1826으로 실패했으며, 별도 문장으로 고쳐 새 소유 DB에서 확인했습니다. 준비 실패를 통과 수에 합산하지 않습니다.
- 서비스·웹/복원39대상은 최초 **38PASS/정오1FAIL**이었습니다. 검사 실행기의 SESSION_HOURS=1과 시험의2시간 이동 때문에 세션이 만료됐습니다. 제품 기본8시간으로 실패 검사만 새 소유 MySQL에서 재실행해 **1PASS/4.56초**를 확인했습니다. 제품 수정이나 전체39 재실행으로 표시하지 않고 실패·반복 수치를 합산하지 않습니다. 소유 MySQL·복원/그림자 DB·프로세스/runtime 정리를 확인했습니다.
- 최종95 결합 솔루션 Release **0오류/0경고**, diff check 통과. 설치 참조팩용 외부 offline 설정을 사용했고 제품 csproj는 변경하지 않았습니다.
- 작성자의49PASS/ApiClient144검사 등은 작성자 보고이며 위 독립 실행 수치와 합산하지 않습니다.

### 병합 이후 담당 연결

제한 문맥/지문 인증의 실제 서버·근태 확정·당일 열린 근무 재접속과 C# 전용 전달부의 코드가 alpha에 포함됐습니다. **기본 OFF·검토용 초안6테이블이며 실제 공유 서버/DB에는 적용하지 않았습니다.** 정식 migration 번호·백업/복원/공유 적용·비공개 키/프로필/provider·LoginForm/MainForm/5번 relay·정상 토큰 UI 인수·실제 Pi/두PC 검증은 후속입니다. 소유자는 활동 만료만으로 복구 문맥을 Dispose하지 않고 로그인 세대/화면 종료와 RAM 복구 수명을 구분해야 합니다.

만료 후 C# 예외는 기록된 GET/원 finalize만 지원합니다. 보관 문맥의 명시 Revoke/폐기는 실제 UI 수명 조립 시 함께 검토할 후속 범위입니다. 현재 부분 지원을 운영 로그아웃 검증 완료로 확대하거나 전체24시간 활동 권한을 요구하지 않습니다.

[기존 통합 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/62#issuecomment-6330262776)을 최종 판정으로 갱신하고 기존 PR/LOG에 취합했습니다. 새 회신용 MD/브랜치/PR, 포크 강제 push, 실제5000 재시작/.env·공유DB/DDL·운영 키·계정·Pi/센서 변경은 하지 않았습니다. 코드 병합으로 미합의 운영 정책을 새로 확정하지 않습니다. 아래 R62-01/02 보류 표시는 해결 전 head 검토 이력입니다.

<a id="pr62-latest28-still-blocked-20261022"></a>
## 최신 PR62 추가 변경 확인 — R62-01 미해결·보류 유지, 2026-10-22

마무리 확인 중 PR62 head **28cd8d94108247ebc656be27e17a151da19795f5**가 추가됐습니다. 이는 외래키 수정이 아니라 근태 finalize·암호화 영수증·같은 날 근무 재로그인·전용 C# pending 연결 등15파일의 기능 확대입니다. **R62-01/P2는 그대로 남아 미병합을 유지**합니다. 최신 `features/identity/routes.py:76~82`의 FK 집합/기대값 AST는 실제 HTTP 결함 재현본 b71과 동일하고, storage()는 여전히 referred_schema를 비교하지 않습니다. [기존 댓글 한 곳의 최신 판정·정확 위치](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/62#issuecomment-6330262776)을 따릅니다.

추가로 **R62-02/P2**를 확인했습니다. 새 `IdentityPendingConnection.cs:21~22,55,91~106`은 최초5분 ExpiresAt으로 결과 복구까지 차단합니다. 서버 `attendance.py:148~159`는 확정 뒤 같은 문맥을 receipt_only/보관기한으로 연장하므로, commit 뒤 응답 유실·초기5분 경과 시 결과는 남아도 전용 연결은 HTTP 전에 취소됩니다. 원본 C#와 HTTP 대역에서 같은 원FinalizeID 복구 요청의 총HTTP1회·OperationCanceled를 재현했습니다. 신규 활동 권한과 이미 보낸 동일작업의 결과전용 복구 수명을 분리하고 현재접속/권한·취소를 유지하는 회귀를 요청했습니다. 공식 pending15검사는 직접통과했지만 이 경계는 포함하지 않습니다. 실제 근태DB 왕복/확대전체검증과 구분하며 **R62-01/02 수정 전 미병합**입니다.

63·64·65의 병합과 실제 merge tree 일치는 유지됩니다. 아래 b71의32PASS·인증/코덱 검토·finalize503 안내는 당시 범위입니다. 새28cd에는 finalize/정상토큰 전환 코드가 게시됐으므로 전체 미구현으로 부르지 않지만, 새 근태/C# 전체의 HTTP·DB·동시성 검증을 완료하거나 운영 정책/배포를 승인한 것은 아닙니다. 추가 auth/receipt 전이의 짧은 정적 대조와 기존 FK 미해결 확인만 수행했습니다. 실제 DB·키·서버/센서·환경은 변경하지 않았으며, 실제 MySQL의 현재/다른DB 동명 FK 회귀와 R62-01 수정을 기존 PR62에서 요청합니다.

<a id="pr62-65-review-20261022"></a>
## 추가 PR62·63·64와 검토 중 신규65 — 63/64/65 병합·62 보류, 2026-10-22

사용자의 추가PR 확인/검토·문제없으면병합 지시에 따라 최신alpha8444983에서62/63/64를 고정했습니다. 검토 중62가 실제서비스로 확대돼 새head를 다시 검토했고,63병합 뒤 추가된65도 포함했습니다. 새 회신용 PR/MD 없이 각 기존PR에 한 번씩 회신하고 기존 기록에 취합합니다.

| PR | 고정 head | 실제 merge / 판정 | 검토/실제 merge tree |
|---|---|---|---|
|62|b71e2e863ec59e63fca5a21b679e330e9daebf51|R62-01/P2 수정 전 미병합|단독a67c9104deeb73116c4002e57ee1ba465f1e1a86|
|63|4ca48dec8e8090444d7bf655e75a2c6fea1fe751|2eeafe9a7fba7f056dcf8faa7282ea610990681f|4f50d9817c751dd0574c8dce38f8ff163c14bd7f|
|64|6ce1012203c76ed6e4ffc4a28ea4034ec6f19679|ca781fd3c248e12e97a8230a56fe78569eb75e9f|630e6fe9dbb6b8feff15896e82bff0a2a6d998d7|
|65|b0f3eb2d3657e304832995dbdb1c42b8928e9329|18a48555d3ef995afca53ed74b4cfb8f0e1dba73|49ad064a596f27197b52339751d77a4374279fe7|

63→64→65순서로expected head를 고정해 일반 merge했고 매번fetch/부모/tree를 확인했습니다.62가미병합이므로62/64의로컬검토상충돌해결을 원격제품에 게시하지 않았습니다. 포크head 수정·강제push/우회PR은없습니다. GitHub각head status/check-run은0건이어서직접검사로판단했습니다.

### PR62 — R62-01/P2, 다른 DB의 동명 부모 FK를 정상으로 수용

[수정 요청·재현·제한](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/62#issuecomment-6330262776)을 게시했습니다.검토도중계약전용c821에서실제서비스b71로확대돼이전107검사를전체승인으로쓰지않았습니다. `features/identity/routes.py:63~68`가FK tuple에서referred_schema를버립니다. 소유MySQL에서identity_contexts.user_id만별도소유DB의users(id)로바꿔도원본GET requests UUID는503대신200/not_recorded입니다. 정상/현재DB명시복구뒤200을대조했고서로다른schema값을inspector로확인했습니다.현재actor는별도재검사하므로인증우회로주장하지않습니다.

None을현재DB로정규화해참조schema/기존컬럼순서·부모테이블/컬럼을함께검사하고같은/다른DB 동명부모의실제MySQL/HTTP 회귀를요청했습니다. 독립결함관측은통과수치에합산하지않습니다. 새서비스29+웹2+실제등록dump/새소유DB복원1의분리재검사 **32PASS/skip0**, source대조/코덱39교차·62/64시작권한분리27PASS를확인했습니다. 단일capture nonce제한은최신문서의명시범위와일치합니다. 정상토큰전환/근태finalize는미구현503입니다.

최초혼합실행은212PASS/웹1FAIL/fixture5ERROR/옵트인1SKIP였습니다.검토실행기의TASK_QUERY=1로기존준비화면기대와달랐고,지문fixture가남긴FK가있는users를뒤의협업fixture가DROP하려했습니다. 제품수정없이웹기본OFF·명시소유복원·fixture분리로위32PASS와별도협업121PASS를확인했습니다.실패/반복수를합산하지않습니다. 계약전용c821기준혼합228PASS도당시근거로만보존합니다.

### PR63 — Pi/C# 코어·HTTP 중계 병합

[최종 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/63#issuecomment-6330234285)에따라Python **228실행/226PASS·Windows POSIX2SKIP**, C#코어 **143**,소유loopbackFlask/Pi코어HTTP **110assertions PASS**를직접확인했습니다.승인freshness·단일채취예산·원문복구·키분리·현재접속취소·센서lock·redirect/size·자기worker/process정리를검토했습니다.실제issuer/키/센서·운영서비스/UI/근태통합은후속입니다.

### PR64 — 일반 서버 시작·공통 UI 조립 병합

[최종 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/64#issuecomment-6330306907)에따라독립treea6989528에서실제WinForms/명시HTTP·source의마감탐색 **24**·업무탐색 **130/9시나리오**·마감 **105**·협업 **115**·ApiClient **144/29시나리오**·공지 **35PASS**입니다.별도소유MySQL8.2RR에서normalentry/coldOFF보관결과·중복설치·현재bridge/연속성·인증 **121PASS/skip0**를확인했습니다. 최신등록첨부 **25PASS**도소유raw002/004/006/009/013·실제임시파일로대조했습니다.수치를이전/작성자보고와합산하지않습니다.

63+64실제merge를새export에서솔루션Release **오류0/경고0**으로빌드했습니다. 설치8.0.31참조팩·외부offline검토설정을사용하고제품csproj는무수정입니다.협업opt-in normalcreate_app/coldOFF result·로그인scope/borrowed메뉴·공지양방향gate·capturedDELETE·명시MainForm deadline접점/navigator를수신했습니다.실제deadline source/확인DB·사건/초기자료·4번성공receipt/선택적snapshot갱신은후속입니다. 제안polling10초를운영값으로확정하지않습니다.

### PR65 — 비공개 설정 검사·기본 차단 runtime 병합

[최종 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/65#issuecomment-6330376932)에따라전체Pi **251실행/248PASS·Linux3SKIP**,새runtime소유loopbackHTTP **110PASS**,파일/네트워크/DB/하드웨어IO금지독립import·기본차단 **16PASS**입니다.3생략은기존flock2/실제Linux파일owner·ACL·link1입니다.키분리/회수키제외·read-onlyCLI·기본factory미설치·단일RAMpool/공유UART·readiness실패후영구차단·drain을검토했습니다.

C#코어143은PR63검사후제품/시험blob동일근거를재사용했고이번재실행/합산은아닙니다.65전후WinForms/서버경로diff0으로직전실제merge Release0/0근거가유지됩니다.실제비공개파일·키/회수키·LinuxBOOTTIME/ACL·Pi서비스/실센서·재시작서버재조정·두PC는미실행입니다.운영readiness는상수True/health200/시간대기로열지않습니다.

### 실제 환경·소유 경계

모든DB/파일검사는새소유MySQL·합성계정·임시파일/loopback대역이며SQLite를사용하지않았습니다.프로세스·DB/별도복원/그림자DB·runtime정리를확인했습니다.작성자의실제37테이블/5000검사는보고로수신해직접수치에합산하지않습니다.실제서버재시작/DB·DDL/.env/flags/ACK·키/개인profile·계정/회원mapping·센서·작업폴더동기화는이번에변경하지않았습니다.62실제코드제공/검토보류와배포·제한로그인UI·근태finalize/정상토큰전환을구분합니다.기존채택정책은유지하고아래미제공/미병합표현은당시이력입니다.

<a id="pr61-merged-20261021"></a>
## PR61 수정본 재검토·병합 완료, 2026-10-21

사용자/팀장의 “수정 올라왔는지 확인하고 재검토·문제없으면 병합” 지시에 따라 기존 R61-01/02/03을 독립 재검증했습니다. 추가 병합 차단 문제를 발견하지 못해 Draft→Ready 후 expected head를 고정해 일반 merge했습니다. [기존 댓글 한 곳](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/61#issuecomment-6311013832)에 최종 결과와 최신2번 원본 대조를 반영하고 최초 검토는 보존했습니다.

| 기준 | SHA |
|---|---|
|실제 최신 alpha|739c5109bfc47be9fc31e4a50a5594241bae23a9|
|검토 head|3ae607ee0894dffa21b41e5a240ffc29d1494ddc|
|실제 merge|ad46517eb8666ef4fea7d1ef779ab256f2433c10|
|검토/실제 merge tree|e8e4a43cb91ba24bfe2ac7be75142866c069edb3|

merge를 fetch해 두 부모·tree·전체 파일 diff0을 확인했습니다. 실제 차이는 공지 기한 패널·DeadlineAlerts·관련 검사13파일이며 공통/MainForm/서버 제품을 이 PR이 설치·변경하지 않습니다.

### 해결과 직접 검사

- **R61-01 해결:** 업무 보기401/403 각각 scope비현재/snapshot제거/팝업0/탐색불가. 오래된 polling·탐색의 인증 오류가 최신 성공 뒤 도착해도 폐기합니다.
- **R61-02 해결:** 기존 지연 재현은 최신 빈 조회 뒤 옛 응답에도 unread0/callback0/navigated False 유지. 양방향 Read/OpenTask·탐색끼리 완료 순서·늦은503도 통과했습니다.
- **R61-03 해결:** 원본 독립 대역 그대로 Read503·Read성공/Recheck503 모두2→4→5→5분. 폐기된 polling의 정상/503/취소가 최신 탐색을 덮지 않고 checking을 해제하며 다음 정상 polling은60초로 복귀합니다.
- 공식 Deadline **105PASS(78+새27)**, 기존 Notices **35PASS**. 실제 WinForms+명시 source/HTTP 대역입니다. 별도 독립16검사·backoff2대조·폐기 polling3시나리오는 공식 수치에 합산하지 않습니다.
- 결합 솔루션 Release **오류0/경고0**, diff-check 통과. 설치8.0.31 참조팩/외부 검토 설정·별도 출력 경로를 사용했고 제품 csproj는 무수정입니다. GitHub head 등록 status/check-run은각0건이며 직접 검사로 판단했습니다.

### 현재 2번 원본과 실제 연동 경계

최신alpha739c510에는 tasks/continuity.py의 실제 협업 reader와 기존 opt-in 등록이 추가됐습니다. 알려진 완전한2번 버전 구간의 계속 진행중 여부만 True/False/None으로 증명하며 누락·중복·미지/3번 제출 구간·상한초과는 None입니다. 이전388d 기준의 “신규 reader 없음”은 당시 이력입니다. 기한 알림 사건·초기 자료 완전성·진행전/반려완료까지 포함한 알림 최소 reader/DTO·계정 확인 writer를 제공한 것은 아닙니다.

독립 UI·정책/source port 범위에서 안전하게 병합했습니다. 실제 source adapter/new scope 설치가 없으므로 연결 전 준비 상태/비활성 버튼·기존 공지 조회를 유지합니다. 실제 HTTP/인증·확인 MySQL/DDL·DB 경합/복원·90일history·1번MainForm/2번탐색/팝업 양방향 조립·공유 서버/물리두PC는 후속/미실행입니다. 실제 환경/.env/사용자DB·장비·제품 폴더 동기화는 이번에 변경·실행하지 않았습니다. 새 회신용 MD/브랜치/PR 없이 기존 기록만 갱신하며 A1~A6·현재 권한/W1/동일TX 경계를 유지합니다. 아래 수정3건·Draft/미병합은 최초 검토 당시 기록입니다.

<a id="pr61-review-20261021"></a>
## PR61 마감 알림 독립 검토 — 수정 3건·Draft 유지, 2026-10-21

사용자의 PR61 검토 요청에 따라 최신 alpha **388d50b68ef114b40a5df8b424f91ab3103a6093**와 PR head **5cca781b5f43e5e45dceae55d0b6497bb44e2946**를 격리 clone에서 무충돌 결합했습니다. 검토 tree는 **e79df7c0c661e2e8a5d5246b599150d6fe5bc688**입니다. [PR61 통합 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/61#issuecomment-6311013832) 한 건에 수정 요청과 2번 기술 답변을 게시했고 Draft·미병합을 유지합니다.

### 직접 재현한 결함

세 건 모두 `DeadlineAlertScope.cs`의 현재 head 기준이며 P2입니다.

| ID | 위치와 조건 | 직접 관찰·요청 |
|---|---|---|
|R61-01|OpenTaskAsync 129~139행, 표시 뒤 업무 보기 Recheck401 또는403|두 경우 모두 scopeCurrent=True/snapshotItems=1/visiblePopups=1. 일반 조회·확인과 같은 폐기 경로로 알림 문맥/팝업/타이머/callback 종료 필요|
|R61-02|132~138행, 업무 보기 Recheck 대기 중 새 조회가 빈 목록 반영|새 조회 뒤 unread0/popup0 → 옛 응답 뒤 unread1/callback1. 조회·재검사 공통 순서 보호로 옛 Apply와 탐색 모두 폐기 필요|
|R61-03|83~89행의 중간 Apply와123~124행의 failures 초기화|Read성공+Recheck503 4회 간격2,2,2,2분. Read503 대조2,4,5,5분. 중간 snapshot 반영과 전체 조회 성공의 초기화 분리 필요|

R61-02는 알림 문맥/탐색의 최신성 결함입니다. 실제 업무 화면도 현재 권한을 별도로 검사해야 합니다. R61-03은 실패 때 snapshot 제거/팝업 차단이 유지되지만 채택 A3의 부하 완화 간격이 깨집니다. 각 최소 재현/대조의 회귀를 기존 검사에 추가하도록 요청했습니다.

### 직접 검사와 미실행 범위

- 공식 DeadlineAlerts **78PASS**, 기존 Notices **35PASS**. 실제 WinForms+명시 source/HTTP 대역이며 실제 서버/DB 검사가 아닙니다.
- 독립 대역: R61-01의401/403, R61-02의지연 응답, R61-03의Recheck503/Read503 대조를 원본 코드로 실행했습니다. 위 공식 수치에 합산하지 않습니다.
- 결합 솔루션 Release **오류0/경고0**, diff-check 통과. 설치8.0.31 참조 팩·외부 offline 검토 설정과 별도 출력 경로를 사용했으며 제품 csproj는 무수정입니다.
- 실제 HTTP/인증·확인 MySQL/DDL·DB 경합/백업복원·90일 history·MainForm 양방향 조립·공유 서버/두 PC는 미실행입니다. 작성자의 실제 폴더 반영 보고도 이번 직접 검사/동기화로 표시하지 않습니다.

### 2번 기술 회신과 후속 접점

[현재 원본/잠금·사건·확인 원자성 대조](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr61-technical-review-20261021)를 기존 공통규약에 취합했습니다. 기존 reader/감사 원본 재사용 가능과 알림용 최소 투영/실제 사건·초기 이력/같은 TX 확인 저장의 미구현을 구분합니다. 최신388d50b도 실제 버전 구간 연속성 reader를 신규 제공한 변경은 아닙니다.

A1~A6 채택은 유지하고 정책 재승인 요청으로 바꾸지 않습니다. HTTP/DDL/MainForm/90일 조회는 PR 본문에 명시된 Draft 후속 범위이며 세 재현 결함과 구분합니다. 새 회신용 MD/브랜치/PR을 만들거나 제품·실제 DB/설정·공유 환경을 변경하지 않았습니다. 기존 기록은 당시 이력으로 보존합니다.

<a id="pr57-58-60-merged-20261021"></a>
## PR57·58 수정본과 신규60 재검토·모두 병합, 2026-10-21

사용자의57/58수정확인·재검토/문제없으면병합·60추가요청에따라실제alpha cbda025에서독립검토했습니다. **60→58→57순서로모두병합완료**입니다. 아래보류/Draft/번호대기는당시이력으로보존합니다. 작성자보고·이전검사재사용·이번직접실행을구분합니다.

| PR | 고정head | 실제merge | 검토·실제merge tree |
|---|---|---|---|
|60|70edfcfdc4ec1a9e0962f538e3d7291fbe89e0e9|e88da9d109ed7dda55872a65d06fc4d0c76a5157|74a727b1157a62ca7e661b5e26da676cecd7e200|
|58|dc946dcc7c31fc194cac4d30f5798098a4e40ae3|fef447280e4c6cd81298dad4fc63d20cf62bbd17|e688534476bff98a2ff519c1d16d2b306cb9f7af|
|57|0f47e5d06e2d8fb2daa735180dc9eb7d686cecb1|ba9e312e0b55de7febf10a603f84e5da008fa617|0e12ec6d6e328500a4c2b29d1af8449542d004e7|

각merge를fetch하여사전tree와대조했습니다.57은사용자의명시지시에따라Draft→Ready전환후expectedhead고정normalmerge했습니다. 최종3PR결합전체솔루션Release오류0/경고0, 검토tree와실제alpha전체파일diff0입니다. 추가코드PR/강제push/타담당제품수정은없습니다.

### PR57 — R57-04 해결, 기존5건 해결 유지

[기존최종회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308590391)에반영했습니다. cbda025+head0f47e5d의독립tree ad88b7274881df78be060107c842197d3ab57e47에서Directory25/6시나리오+Requests88=**113PASS**,전체솔루션Release0/0을직접실행했습니다. 원본8경합시나리오40검사도읽어대조했습니다.

기존독립재현은received조회대기→수동응답창에서창1→1/자동추가0, 수동창닫힘뒤자동1, 같은버전추가0/자동쓰기0으로PASS했습니다. 표시직전CanPresent재확인과보류후displayed키미소비를확인했습니다. 이전unknown→not_recorded→명시409의입력보존/편집복귀·최신version2/새key도PASS입니다.

협업서버폴더/관련검사/012는기존8613c947검토본과동일하고현재의존코드도이전alpha44c기준과일치하여 **기존소유MySQL55검사근거를재사용**했습니다. 이번에재실행하거나113에합산하지않습니다.1번공통on-off결과reader설치/012물리적용·MainForm scope/공지양방향조립,2번실제버전구간연속성reader,알림10초/닫기·재로그인운영값·공유API/두PC는후속입니다. 근거부족None/503과원업무/파일권한경계를유지합니다.

### PR58 — R58-01 해결, 확정 거절에서만 장치채널 해제

[기존최종회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309407344)에반영했습니다. cbda025+dc946dc의독립tree ceb1a864ea4f95b91117053aaa0903ebd0c0ed1e에서실제Form/owner/launcher/EnrollmentDeviceChannel필수연결 **341PASS**,기존회원복구 **95PASS**,기존14시나리오 **46PASS**,지문UI **58PASS**입니다.341은기존177을포함하며과거/반복/5번별도수치와합산하지않습니다.

독립A확정거절→B복구는A채널dispose→B바인딩/재발급1·Pi1로PASS, 직접B대조도1/1입니다. 별도dispose실패대역은attempt/장치고정/확인필요를보존하고재발급0/Pi0입니다. 불명응답·발급후·Pi송신후/reissue거절보존,선택/복구GET만으로자동추가동작0도확인했습니다.

이검사는실제WinForms+통제HTTP/Pi이며실물A→B/공유MySQL등록완료검사가아닙니다.5번실물완료·재인식과1번READ ONLY DB12항목정상은 **작성자보고로수신**하여이번직접실행/새수정실물성공으로표시하지않습니다. 새본인인증challenge/제한로그인문맥·단일소비API/새wire는기술대조안·미구현후속입니다. 기존등록/scan성공을새본인인증·출퇴근성공으로대체하지않고실제식별자/서명원문을전재하지않습니다.

### PR60 — 내용 동일 SQL013 번호화

[회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/60#issuecomment-6310090050)을한건게시했습니다.4파일전체diff, draft→013rename100%/blob73f07dbad461cf85355bd4cd29ba3b7b90608bda동일,006blobd65a713e60a4d70071e474c26d3a9d884a05288e/schema1·002/004/012보존과실행코드의이전경로잔재없음을확인했습니다. README의이전이름설명은이력입니다.

새013rawSQL을소유MySQL에만적용한verify_shared.py **239PASS**(134+현재참조회귀105포함/중복합산없음),실제TCP HTTP/파일·권한/현재참조/rollback/마지막참조정리와소유자원정상정리를확인했습니다.013은전역schema13/자동DDL·공유적용ACK를뜻하지않습니다.

### 결합 검증·실제 환경 경계

세PR은무충돌결합이며각대상제품/검사blob은독립검토본과최종조합에서동일합니다. 최종0e12ec6d전체솔루션Release0/0은설치8.0.31참조팩/외부offline설정·출력경로를사용했고제품csproj는무수정입니다. 로컬감사용조합commit은push하지않았습니다. GitHub의각실제merge tree/부모와최종alpha전체파일diff0을확인했습니다.

격리clone/소유테스트자원·코드병합/기존댓글·PR/LOG만갱신했습니다. 실제작업폴더동기화/.env·사용자DB/공유서버·012/013적용/옵션/통합ACK·실계정재발급·장비/물리두PC는이번에변경·실행하지않았습니다. 기존채택정책/미확정운영값을새로결정하지않으며코드제공과1·2·5번실제연결·운영검증을구분합니다.

<a id="pr52-merged-final-20261021"></a>
## PR52 최종 수정 검토·충돌 통합·병합, 2026-10-21

새cc84c1c를 수신하여 앞서 보류한R52-03을 재검증했습니다. **R52-01/02/03 모두 해결, PR52 병합 완료**입니다. [기존 위치 최종 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6287667210)을 따릅니다. 아래 현재 회차 중간의 미업로드/보류/충돌 안내도 당시 사실로 보존합니다.

- 작성자 수정head `cc84c1cd11a65f7af4a42e7f0e5752d29b6fd319`
- 최종실제alpha base `96734bcb29eead01369584c0968fa07361f7f7bc`
- 최종PR head `21faf3465e4aeedc275dc201a77a7070fbc7ef9a`
- **merge `cbda025ea9b2f83e8f63f5bca45bed74cec7ef77`**
- 실제merge tree **`68b87114ed9c84392195c8442173fdcdd3ac41a0`**, 사전검토tree와일치/원격alpha·merged=true확인

### R52-03 해결과 보존한 소유 접점

공유 참조3조회는FOR UPDATE/populate_existing을 사용합니다. 공식shared239(신규105포함) 및 이전독립snapshot 경합을 안전성기대로 바꾼2검사 통과: 인증snapshot에A100만 보여도B200의최종참조·ready·원문·다운로드200을 보존합니다. rollback/마지막참조·과거제출/현재권한도 기존239범위로 대조했습니다. 이 공유코드/results/UI·검사원본은 이후최종tree와blob동일하여 결과를 재사용했습니다. 반복/이전134/이전회차와 합산하지 않았습니다.

최신alpha를 두번 일반merge해 원작성자이력과1·2번 새코드를 보존했습니다. 첫번째는 PR59의task_web.index와results_web.index 메뉴를 함께 유지한da52462이고, 두번째는 최신업무웹96734bc의execute_start 전체를 보존한21faf34입니다. `_start(...,body=None)`에서 현재인증·권한 뒤 입력을validate_start_body로 검증해같은execute_start로 넘기며 KST/취소/W1/receipt/TX로직을 되돌리지 않았습니다. README도2번최신웹등록/수락/재작업과3번결과물설명을 함께보존했습니다. 테스트통과뒤 기존PR52branch에 일반push하고 expectedhead고정merge했으며 강제push/새PR은 없습니다.

### 최종 검증 구분

| 범위 | 직접 결과 |
|---|---|
| 새공유첨부·R52-03 |239통과+독립안전성2통과; 1차tree3aacbfe0 실행·최종68b87114 해당제품/검사blob동일성확인 |
| 최신alpha 결과물 |실제소유MySQL 서버191·쿠키/CSRF웹183·실제loopback HTTP/WinForms41; C#115소스0오류0경고 |
| 최신alpha 취소·연장·제출 |소유MySQL/HTTP258통과; 새execute_start수락/재작업소비·rollback포함 |
| 최신2번 업무웹 결합 |test_task_web.py+test_task_web_writes.py **81통과/171.46초**; 실제소유MySQL·물리DDL변경0/schema1·자원정리확인 |
| UI/기본off/빌드 |원본JS52·cold/구문/diffcheck, 전체솔루션Release0오류0경고 |

최신alpha의서비스가바뀐뒤에는191/183/41·258·81을 다시실행했습니다. shared239/독립2/UI기존race를 매번실행한것으로표시하지않고 동일원본을 근거로재사용했습니다. 외부offline참조팩8.0.31/산출물경로만사용했고 제품csproj는 무수정입니다. 소유테스트DB/HTTP/MySQL프로세스·runtime정리를 확인했습니다.

### 남은 작업·다른 PR

추가3테이블 실제검토/적용·최종조직자료는1번, 실제업무TX의첨부연결/해제·파일UI/업무웹결과물 이동·갱신은2번후속입니다.3번제공부병합이 실제2번소비/공유DB적용완료를 뜻하지 않습니다. 기본off/명시검토자/정식제출과통합ACK/변경503경계를새로변경하지 않았습니다. 실제제품폴더/.env/설정/사용자DB·실계정재발급·공유서버/DDL·Pi/센서/두PC는 변경·실행하지 않았습니다.

PR59는f0430b7로병합했고새alpha에도보존됐습니다. PR57head8613c947은R57-04알림중복/Draft유지, PR58후속head6886aa1은2910cbb대비메뉴3파일수신뿐/UI원본diff0으로R58-01미해결입니다. 각기존댓글에수정요청/후속확인을 남겼습니다. 기존문서만갱신하며별도회신MD/문서PR을 만들지않았습니다.

<a id="pr57-59-review-20261021"></a>
## PR57·58·59 검토와 PR52 대기 상태, 2026-10-21

사용자의 PR검토 요청과 문제없으면 병합 지시에 따라 현재 열린52/57/58 및 검토중 추가된59를 확인했습니다. 검토 시작의 실제alpha는 `44c85f322d22a3f48422353aedb45a09e8970e3c`이며 API에 남은57/52의 옛 base8ed25c6을 그대로 최신으로 쓰지 않았습니다. 새 정책·실제 환경 적용을 추정 승인하지 않습니다.

### PR59 — 병합 완료

- head `3877d0f78b0955acff35787c496f98de7e6e88d0`, 검토 tree `cc5a74993304cca1a9776ba9eca558a8a56045a1`.
- 3파일 전체diff 대조: 공통 web.py의 고정 /tasks redirect2줄, README, 기존 task_web검사. 로그인 후 등록된 endpoint만 이동하고 기본off 준비화면/직원·관리자 현재권한을 유지합니다.
- 소유MySQL8.2/RR에서 `python -m pytest -q --tb=short tests/test_task_web.py` **33 passed/skip0**, pytest92.65초. 홈링크/익명·on/off/역할/GET전후DB불변 포함. 소유MySQL·runtime정리true, diffcheck통과. 다른 전체서버/C# 검사를 실행했다고 보고하지 않습니다.
- expected head를 고정해 정상 merge **`f0430b7b2ec33eeac69b0ac78b83ddad6af90695`**. 실제 merge tree가 위 사전tree와 일치하고 alpha원격/PR merged=true를 확인했습니다.
- [통합 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/59#issuecomment-6309450248). 코드 접점 완료이며 실행중5000 배포/재시작·실제 제품폴더 동기화·공유DB/옵션/ACK/쓰기503·두PC는 미실행입니다.

### PR57 — 기존5건 해결, 새 R57-04/P2 수정 요청

- head `8613c9477a567ad445bf753a4f05baa0867f4d9c`, alpha44c85f3 결합 tree `47bfdd6fe05b207eadbbddedc5a707252704e8b5`.
- 팀장 R57-01 취소 CURRENT/02 비활성 상대 종료5종/03 확정409 재준비와1번 coldOFF 성공reader/FK·물리구조 지적을 해결 확인했습니다. 취소 반대편은 명시 소유DB 보호TX 대역이며 실제2번HTTP/배포ACK와 구분합니다.
- 소유MySQL **55 passed/skip0**,163.57초(협업24+추가21+W1 10);012물리8오류 거부/실제생성·dump→새 소유DB복원·이력당사자90일 포함. 소유MySQL/임시runtime정리true.
- 실제WinForms Directory25/6시나리오+Requests48=73 통과. 이전 독립R57-03은 unknown→not_recorded→명시POST409 뒤 pendingFalse/입력편집·refresh가능, 다음전송prepare2/현재version2/새key 확인. 전체솔루션 Release0경고0오류/diffcheck통과.
- **R57-04/P2:** `CollaborationNotificationCoordinator.cs:34~53`은 다른응답창/중요공지 검사를 await 전에만 합니다. 수신 ListAsync 대기 중 같은pending의 수동응답창을 열고 조회를 마치면 표시직전 재검사없이 자동창을 추가하여 **가시응답창1→2**가 됩니다. 한 공유scope에서 실제WinForms로 재현했고 중복타이머/MainForm배포/향후공지 역방향통합을 전제로 하지 않습니다. 이 안전성검사 실패는 정상73에 합산하지 않습니다.
- 표시직전 owner의 가시성/최소화·다른응답창/공지 조건을 재확인하고 양보시 후보를displayed에 기록하지 않는 회귀를 요청했습니다. [기존 댓글 보완](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308590391).
- 공유scope소유/지연표시/협업 이력 제공은 확인했지만 공통설치/012·history계약·MainForm조립/2번실제연속성reader·운영값/실제동시통합은 후속입니다. **Draft·미병합 유지**입니다.

### PR58 — R58-01/P2 장치 재선택과 실제 채널 수명 불일치

- head `2910cbb658979a793f036b4947cf8630d65fbda0`, alpha44c85f3 결합 tree `b8af8b49e44b5b42d16b91f154faa63e13628452`.
- 기존회원의 정확ID/LoginId/활성/역할/페이지·선택세대 재확인, 명시확인/recovery-only번호·모드 고정, 지연응답/로그아웃/종료 차단을 대조했습니다.
- 필수 enrollment-mysql-v1 모드의 실제 회원관리/등록 WinForms+명시 HTTP/channel **177**, 기존회원복구 **95**, 회원관리14시나리오 **46**, 지문WinForms **58** 통과. 모두 통제 HTTP/Pi대역이며 실제MySQL/센서 완료가 아닙니다. 전체솔루션Release0경고0오류/diffcheck통과.
- **R58-01/P2:** `FingerprintEnrollmentForm.cs:211~224,238~245`는 A의 정확409/NO_FREE·발급전 거절에서 attempt를 해제하고 장치재선택을 허용합니다. 실제 `CreatedUserEnrollmentLauncher.cs:49~58`의 EnrollmentDeviceChannel은 이미선택한A채널을 유지해B를 거절합니다.
- 원본Form+실제channel+합성HTTP/Pi 재현: A일반issue확정거절→B명시선택/B복구GET→시작에서 **재발급0/Pi0**, 정상주소임에도 주소설정오류. 처음부터B복구 대조는 **재발급1/Pi1/pi-b라우팅**까지 진행하며 전체물리완료 대조는 아닙니다. 정상58에 이 재현/대조를 합산하지 않습니다.
- A→B 변경에만 해당하고 같은A복구는 영향없음/창재열기로우회가능/오등록·저장우회없음으로 범위를 제한합니다.1·5번의 UI/channel수명을 맞추되 불명·발급후·장치송신후 attempt보존을 유지하도록 [PR58에 한묶음 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309407344)했습니다. **미병합·수정후재검토**입니다.

### PR52 — 새 수정 없음, 최신 공통 메뉴와 충돌 안내

head32fc711은 동일하며 R52-03/P2 첨부참조 경합 수정대기입니다. 기존R52-01/02 해결확인과 이전 전체검사결과는 지난 회차이고 이번에 재실행하지 않았습니다. 새alpha f0430b7과의 읽기전용 merge-tree에서 web.py feature메뉴 충돌을 확인해 [기존52댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6287667210)에 task_web.index와results_web.index를 함께 보존하도록 안내했습니다. 충돌해결이나3번 제품수정은 대신 실행하지 않았습니다.

### 실제 범위와 보존

이 검토는 isolated clone/소유MySQL·합성WinForms검사와 PR59 원본병합, 기존댓글/PR·LOG기록 취합입니다. 실제 작업폴더·.env/옵션·사용자DB/공유서버·DDL/ACK/쓰기503·실계정 재발급·Pi/센서·실행중 구창은 변경하지 않았습니다.1·3·4·5번이 다른곳에서 실행했다고 보고한 작업을 이 검토의 직접 검증으로 쓰지 않습니다. .NET은설치8.0.31/외부offline설정·출력경로로 검증했고 제품csproj는 무수정입니다. 기존기록의 모든줄을 보존하며 새 회신MD/문서PR은 만들지 않습니다.

<a id="pr52-full-recheck-20261021"></a>
## PR52 전체 재검토 — R52-01/02 해결, 새 R52-03/P2로 미병합, 2026-10-21

- PR head: `32fc71124ac9534fd34ddd00bf1cd99969c84a51`
- 실제 alpha: `44c85f322d22a3f48422353aedb45a09e8970e3c` (PR API의 오래된 base8ed25c6 대신 원격을 직접 확인)
- 충돌 없는 결합 tree: `c80e19d0acac1ed3856e23bffc813c56d6d732b8`
- [3번 새 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6308742417), [기존 위치에 보완한 팀장 검토 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6287667210)

### 새 수정 요청 R52-03/P2 — 새 참조를 놓쳐 공유 파일 접근이 막힘

`RemoteWork.Server/remotework/features/shared_attachments.py:94~96`의 `_links`는 일반 REPEATABLE READ 조회입니다. 인증 조회로 이미 A100만 보이는 snapshot이 생긴 요청에서 다른 요청의 B200 연결/commit을 기다린 다음 A100을 해제하면, `202~207`의 `_mark_unreferenced`가 현재 B 참조를 놓쳐 파일을 `deleting`으로 저장합니다. 공유 guard와 파일 행의 잠금 조회만으로 앞서 생긴 snapshot은 갱신되지 않습니다.

독립 실제 MySQL+원본 helper/API 재현: A 해제200, 실제 남은 참조 `[200]`, 물리파일 존재true인데 저장 state=deleting, 다운로드404/ATTACHMENT_NOT_FOUND. 대조군(B 연결이 요청 이전에 commit)은 ready/다운로드200입니다. 자동 cleanup은 새 TX에서 B 참조를 보아 물리파일을 삭제하지 않지만 deleting을 되돌리지도 않아 접근 불가가 유지됩니다. **실제 파일 삭제/이력 유실까지 발생했다고 보고하지 않습니다.**

참조 존재·제거·마지막 참조 판정이 현재 참조를 읽도록 보완하고, A/B·과거 제출 참조 보존·동일TX rollback·현재권한을 유지하면서 이 순서의 회귀를 추가해 주세요. 재현/대조2는 정상기능 검사134와 합산하지 않습니다.

### 직접 재검증과 해결된 지적

| 범위 | 이번 직접 결과 |
|---|---|
| 기존 R52-01 | 불변 task/submission/round 캡처와 선택 잠금 확인; 이전 독립 race에서 표시202/POST202 |
| 기존 R52-02 | 확정실패+not_recorded 최신문맥 재준비/입력·파일 보존, 불명결과 동일key/body 유지 |
| JS | 원본 handler/명시 DOM·HTTP 대역52; 세션변경/401/403 뒤 강제쓰기0 추가12 |
| 결과물 서버/웹/C# | 소유 실제 MySQL 서버191·쿠키/CSRF 웹183·실제 loopback HTTP/WinForms41; C#115소스0오류0경고 |
| 취소/연장 통합 | 실제2·3번 서비스/소유 MySQL·HTTP258; 현재 취소 FOR UPDATE·pending lock=True·파일IO전후·receipt/과거파일·rollback |
| 공용 첨부 | 소유 실제 MySQL/HTTP134; 실제2번 제품 소비가 아닌 명시 fixture 경계 유지 |
| 빌드/기본off | 전체솔루션 Release 오류0·경고0; cold/JS구문/diffcheck 통과 |

서버/웹/C# 원본 검사의 첫 실행은 검사 자체가 모두 통과한 뒤 Python3.14의 미회수 test response 파일 때문에 임시폴더 정리에 실패했습니다. 제품·검사 assertion을 바꾸지 않는 외부 runner에서 종료 직전 gc.collect만 수행해 원본 검사를 다시 실행했고 마지막 실행은 정리까지 정상 종료했습니다. 반복 실행 수치를 합산하지 않았습니다. 첫 실패의 테스트 전용 잔여 폴더는 사용자 자료가 없고 MySQL 프로세스 종료 후 남은 상태로 별도 감사 기록에 표시했습니다. Release는 외부 offline config/설치된8.0.31 참조팩/출력경로를 사용했고 제품csproj를 바꾸지 않았습니다.

### 담당별 후속과 실제 환경 경계

3번의 취소/실제 연장 소비·8행동 이력·공용 첨부/웹 진입 제공 코드는 확인했습니다. 1번은 추가3테이블 초안의 물리 스키마/적용을 검토하며 기존006 blob `d65a713e60a4d70071e474c26d3a9d884a05288e`는 유지됩니다. 새 공용 모델은 common Base를 상속하여 opt-in import 후 명시 init-db 생성 대상에 포함되지만 기본off/일반시작/HTTP의 자동DDL은 아닙니다.

2번은 실제 등록/교체/취소 TX에서 버전 증가 전 replace_task_files/release_task_files 호출, 파일 선택·목록·다운로드 UI와 업무 웹→결과물 이동/처리 후 GET 갱신을 연결해야 합니다. 현재 함수의 실제2번 제품 호출은 없고 테스트 fixture만 존재합니다. 기존 WinForms 제출 진입/성공 GET은 이미 제공되어 재구현하지 않습니다. 현재 명시 검토자 기준·기존 채택 정책을 새로 변경하지 않습니다.

**판정: 기존 R52-01/02 해결, 새 R52-03/P2 수정 후 재검토. PR52 미병합.** 실제 제품 폴더/사용자DB/공유서버·.env/DDL/배포ACK/쓰기503·장비를 변경하지 않았습니다. 별도 문서 회신 PR도 만들지 않았습니다.

<a id="pr57-review-20261021"></a>
## 최신 검토 — PR57 협업 Draft 수정 필요, 2026-10-21

[PR57](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57) headfe209ae를 alpha8ed25c6과 결합해 공식서버24·Directory25/6시나리오·Requests23·Release0/0을 확인했습니다. 별도검사에서 **R57-01/P2 취소 snapshot 혼합, R57-02/P2 상대 비활성화 시 종료 차단, R57-03/P2 미기록 확인 뒤409의 입력잠금**을 재현하여 미병합/Draft 유지입니다. [고정 코드·조건·수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/57#issuecomment-6308590391), [중앙 검토 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr57-review-20261021)를 따릅니다.

기존 B1~B7 채택은 유지합니다. 1번의 새 API/W1·미번호DDL/등록·MainForm 로그인수명,2·4번의 CURRENT취소/실제 중간이력 연속성, B4지연표시·B7상태이력 조회가 남아 있습니다. 협업10초/닫기/재로그인값은 로컬 제안이며 기한알림60초와 혼동하지 않습니다. [2번 기술 대조 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#pr57-member2-bridge-review-20261021)를 기록했습니다. 이번 검토는 제품/실제폴더·공유서버/DB·.env/DDL·장비를 변경하지 않았습니다.

### 검토 기준과 세 수정 사항

head `fe209ae237ad08d91a54f8d4970410860ecb2687`, 실제alpha base `8ed25c6e69c33c023267a2532c267fee71992a8f`, 결합tree `8dbac44707d1cd4590aae6578c305c23116609df`입니다. 23파일1918추가12삭제이며 협업 서버의 독립 metadata/명시설치·미번호SQL,4번화면/서비스/coordinator·소유회귀입니다. 공통auth/db/registry/run.py/MainForm과 기존2번 helper·3/5번제품은 수정하지 않았습니다. 무충돌/CLEAN과Draft의 준비조건/실제판정은 구분하며 ready전환·병합·공유적용은 하지 않았습니다.

| ID | 고정 코드 | 확인한 문제·수정 방향 |
|---|---|---|
|R57-01 P2|`collaboration/task_bridge.py:24–30`, 기존`tasks/lifecycle_support.py:116–122`|task/assignment는CURRENT잠금인데취소는plainRR snapshot. guard전 snapshot→별도취소commit 뒤context200/version2·목록pending; 새요청은403/ended. current취소원본읽기와2·4번bridge결합을보완해야 함|
|R57-02 P2|`collaboration/routes.py:261–264`|양측활성을모든전이에강제. 현재actor활성/상대만비활성인pending철회·거절/accepted철회·탈퇴·완료5경로409, ended_at없음. 신규요청·수락요건과현재당사자의종료권한을분리해야 함|
|R57-03 P2|`CollaborationRequestForm.cs:221–231`, `160–165`|unknown→lookup not_recorded→명시재전송409 CONTEXT_CHANGED 뒤pendingSend 유지로본문/업무/대상/refresh잠김. 다음조회/재전송도prepares1·옛version1 반복. 확정실패·미기록확인 뒤입력보존/현재문맥재준비경로필요|

R57-01의추가원본HTTP재현은 취소전W1 key, 클라이언트의명시expected_row_version2, 옛RR snapshot 조건에서 취소행+task버전2를commit하면 **생성201/pending이저장**됩니다. 보통UI의옛version1은버전검사로차단되므로구분합니다. 반대편취소는실제HTTP/통합ACK를해제한것이아니라새소유DB의공통guard/task잠금·취소행·버전+1동일TX를사용한명시대역입니다. 원업무상태/담당자/파일권한은변경하지않습니다. 읽기2경로와이조건부쓰기1경로를나눠기록합니다.

R57-02는상대접속을폐기/계정비활성화한뒤현재활성actor로5행동을호출했습니다. 모두RECIPIENT_UNAVAILABLE409/원status·row_version·ended_at유지입니다. accepted는희망일이후지연만되어자동만료하지않으므로탈퇴/철회를영구막을수있습니다. 상대활성검사를행동별로대조하되현재행위자권한·row_version·신규수락활성검사는유지합니다.

R57-03은실제WinForms에명시서비스대역을연결한안전성검사실패입니다. writes2시점pending=true/본문ReadOnly/refreshDisabled, 다시조회·재전송후writes3/expectedVersion1/prepares1입니다. 불명확한통신중원래key/body보존을제거하라는요청이아니며, 확정실패/미기록뒤현재문맥을준비할경로를추가해야합니다.

### 공식 검사와 별도 재현의 구분

Python3.12.14/pytest9.1.1·소유MySQL8.2/RR의공식 `tests/test_collaboration_requests.py` **24통과/생략0/경고0·61.09초**, thread warning을오류로처리했습니다. Directory실제WinForms/명시HTTP대역 **6시나리오25검사**, Requests실제WinForms/ApiClient·서비스대역 **23검사**, 제품솔루션Release 경고0/오류0·diff검사를확인했습니다. 명시install/bootstrap과FeatureBase독립metadata·rawSQL·제약누락503·동시반대응답·감사실패rollback은이공식범위입니다.

별도취소읽기2·버전입력조건의취소후생성1·비활성상대5는결함관찰재현입니다. root생성관찰은1검사2.63초이며기능정상통과로합산하지않습니다. UI409복구안전성은실패입니다. 취소읽기최초Flask hook준비오류는검사기수정후2재현으로분리했고제품결함수치에넣지않았습니다. 소유MySQL/프로세스·임시runtime정리를확인했습니다. 실제공유서버/DB·물리두PC검증은아닙니다.

증거: `remote_work_pr57_independent_20261021/audit/official.txt`, `official-summary.json`, `directory-ui.txt`, `requests-ui.txt`, `build.txt`; `remote_work_pr57_server_review_20261021/audit/cancel-race-preparation.json`, `cancel-race-listing.json`, `inactive-peer-*.json`, 정리summary; root `remote_work_pr57_review_20261021/audit/test_cancelled_create_race.py`, `cancelled-create-race.json`, `cancelled-create-owned.txt`, `cancelled-create-owned-summary.json`; `remote_work_pr57_ui47_20261021_a/audit/retry409.txt`, `CollaborationEdge/Program.cs`입니다.

### 담당별 실제 연결·완료 조건

2번검토에서는signedBIGINT/INTEGER타입·실효due_date/현재배정원본과같은보호TX의task/assignment현재잠금방향을대조했습니다. 취소CURRENT보완과버전구간전체의실제이력연속성reader는미제공/미연결입니다. 관리자변경만있는경우와제출→반려→재작업복귀를구분하고구간누락·불명은None/503으로유지해야합니다. 기존TaskReadSession에새버전/쓰기필드가이미있다고쓰지않으며시험용True/False주입을운영승인으로확대하지않습니다.

1번은새행동/DTO/경로·W1등록·미번호DDL/백업복구·실제적용과MainForm로그인당coordinator를대조해야합니다. 기존CHAT참여자GET재사용코드는확인했으며그것이새협업저장API/DDL승인을뜻하지않습니다.4번은세결함수정과실제제공접점결합을담당합니다. B4 is_late/IsLate는DTO에는있으나목록/응답창에서미표시, B7 CollaborationEvent는저장만하고공개API/화면은최신Row만제공하므로당사자상태이력조회가남습니다. 원업무/파일/own·managed권한을확장하는방법으로해결하지않습니다.

협업알림10초·닫은버전보류·재로그인pending표시는로컬제안입니다. 팀장이채택한기한알림A3의60초/실패backoff와별개이며B1~B7과90일조회/물리삭제미확정기준을새로뒤집지않습니다. 새전체ACK/쓰기503해제·공유DDL/서버재시작·계정자료·Pi/센서변경을하지않았습니다. 작성자본문의바탕화면23경로반영보고는원본병합/공유승인과구분합니다. 기존PR57에수정과회귀를모으도록1·4번에게댓글회신했고별도회신용MD/PR은만들지않았습니다.

<a id="pr55-56-merged-20261021"></a>
## 최신 재검토 — PR55·PR56 병합 완료, PR52 코드 수정 대기, 2026-10-21

[PR55](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55) headbc1751d의 R55-04 단일 종료 시각 수정과 회원관리 연결 fixture 보완을 최신 alpha864a2a6에서 검증하고 **`b37fe3a38babe1553fc6d78e39e4b8c07ce0d822`**로 선행 병합했습니다. 공식8모듈239통과·독립 초 경계4통과·Release0/0이며 기존R55-01~04는 모두 해결 확인입니다. [최종 검토/범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55#issuecomment-6306967504)를 따릅니다.

이어서 [PR56](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56) head0bf93bc를 **`395d70d84172f332d9f47e162aa64b9bb5adaa57`**로 병합했습니다. C#중계540·전용WinForms31·회원관리 기본173/필수177(각 기존14시나리오46)·Python168통과/Linux2생략·실제서버/소유MySQL 어댑터2가 통과했습니다. 두 병합의 실제tree가 사전 검토tree와 일치합니다. [최종 검토/미실행 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56#issuecomment-6306967787), [중앙 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr55-56-merged-20261021)를 따릅니다.

실제 작업 폴더에는 병합된46경로만 백업 후 동기화했고 DOCX·실제설정은 보존했습니다. 실행 서버 재시작·실제010/011·옵션/서비스 전환·운영자close/reissue·Pi/센서·실물 등록은 실행하지 않았습니다. 코드 병합과 실제 서버의 새 경로404 해소/실물 검증은 별도입니다. PR52 head0a7a57d는 동일하여 기존R52-01/P1·R52-02/P2 코드 수정 대기를 유지합니다.

### 고정된 최신 기준과 실제 병합

실제 alpha base `864a2a6a223d7dc6f4ab6759ed091b0d390b1565`에는 이전bb281e3 이후2번의 raw009 정합성/읽기 전용 업무 웹 커밋이 있습니다. 이번 검토는 이 기준을 보존해 결합했으며 PR 메타데이터의 예전basebb281e3만 기준으로 병합하지 않았습니다.

| PR | 고정 head | 검토/실제 tree | 실제 merge |
|---|---|---|---|
|55|`bc1751d560f4f407af18bae118c4ac0e39d4c091`|`eddf889e5a3ac656bdbce73f748c029b28ede4b3`|`b37fe3a38babe1553fc6d78e39e4b8c07ce0d822`|
|56|`0bf93bca7891c3ae73e00a5ceb3f3b9465f85dd6`|`f4d7e75b026ecbd6f8794288fb2cfc03f12d2c87`|`395d70d84172f332d9f47e162aa64b9bb5adaa57`|

PR55부터expected_head_sha를 지정해 병합하고 실제commit/tree/원격alpha를 확인한 뒤 PR56을 같은 방법으로 병합했습니다. 별도 audit commit은 로컬 검토용이며 원본에 push하지 않았습니다. 제품46경로 이외 최신2번 업무 웹/정책/다른 팀원의 코드는 병합 과정에서 보존했습니다.

### PR55 재검토와 이전 결함 해결

이전0871b2c 이후 델타는5파일이며 제품 로직 수정은 recovery.close의 단일 stamp입니다. `Recovery.closed_at`과 source `Job.updated_at`에 같은 값을 기록하고 `check_binding`의 정확한 동일성·현재권한·원래ticket/접속/만료·claim 보존을 그대로 유지합니다. 나머지는 초 경계2회귀와1번 소유 회원관리 fixture3파일 보완입니다.

원래 독립 재현을 안전성 기대로 바꾼 같은 초/매호출+1초2검사와 공개 초/소초 경계2검사가 **4통과/26.11초**입니다. 초가 바뀌어도 close commit 후 같은 확인서 재실행exit0, 상태GET200·새 로그인reissue200, claim2·old/new ID 분리·정확한 시각 결합을 확인했습니다. 공식239에 포함되는2개와 합산하지 않습니다. R55-01~03은 앞선 수정 확인을 유지하고 이번 공식 회귀에도 포함됩니다.

새 fixture는 GET/POST physical-result의15필드·같은job/revision·원문 선보관→complete·동일재전송/충돌과 창닫기/로그아웃 뒤 늦은쓰기0을 검사합니다. 명시 취소 버튼/서버 cancel API 검사를 새 fixture가 포함했다고 표시하지 않습니다. PR55만의 기본모드는 연결153+기존14시나리오46통과/exit0, 같은옛중계에 대한 필수모드는 선행physical조회 누락으로 예상대로exit1을 확인했습니다. fixture의 메모리 검사를 MySQL 영속성 검사로 확대하지 않습니다.

### 이번 직접 실행한 검증

Python3.12.14/pytest9.1.1·소유 MySQL8.2 InnoDB/RR에서 공식 durability/device/approval/protocol/task_creation_migration/task_write_support/recovery/durability_review **8모듈239통과·실패/skip/경고0·381.21초**입니다. WriteSupport HTTP DLL 선행 빌드로 실제C# HTTP검사를 생략하지 않았고 thread warning을 오류로 처리했습니다. WriteSupport 및 솔루션Release 경고0/오류0, diff검사 통과입니다. 작성자의Python3.14/151.27초와 이번 실행의 로그를 구분하고 과거237과 합산하지 않습니다.

최신55+56 결합본에서 C#중계·실제Python loopback **540통과/6.08초**, 전용WinForms **31통과/8.61초**, 원본UserManagement 기본 **연결173 + 기존14시나리오46/17.67초**, `--require-mysql-enrollment` **연결177 + 기존14시나리오46/11.39초**가 통과했습니다. 앞선 원본Modal registration driver timeout은 해결됐습니다. 테스트 입력/서버/장치는 통제된 대역이며 실제공유DB/센서 검사가 아닙니다.

Python전체unittest **170실행/168통과/Linuxflock2생략/실패0·4.750초**, 실제Flask/소유MySQL+Pi어댑터 독립 **2통과/11.02초**입니다. 후자는 합성키/가짜센서에서 기존11개 보존·slot12 단일store·정확BLOB·servercomplete후mapping·재시작동일읽기sensorIO0, intent응답 직후취소store0/mapping0/증거유지를 확인했습니다. 실제11개 지문을 검토자가 조작한 검사가 아닙니다. 소유MySQL/프로세스·임시runtime 정리를 확인했습니다.

증거: `remote_work_pr55_final_independent_20261021/audit/official.txt`, `official-summary.json`, 빌드로그; `remote_work_pr55_recovery_fixed_review_20261021/audit/recheck-review.json`, `clock-owned.txt`, UI모드로그; `remote_work_pr55_56_final47_20261021/audit/final-review.md`, `client-checks-summary.json`, `real-integration-summary.json`; root `remote_work_pr55_final_review_20261021/audit/merge-results.json`, `sync-result.json`입니다.

### 실제 작업 폴더 반영과 환경 적용 구분

최종 실제merge의tree를 검토tree와 대조한 뒤 base864a2a6 대비 PR55의22경로+PR56의24경로, 총46경로만 동기화했습니다. 기존파일은base일치 또는이미incoming일치일 때만 허용하고 각파일 쓰기 직전 재검사·이전내용 백업을 했습니다. DOCX·실제.env/설정과 범위외소스는 건드리지 않았습니다. 실제작업폴더의WinForms Release는 별도출력경로에서 경고0/오류0을 확인했습니다. 최초출력의한글인코딩문제로 child CLI언어만영문으로설정해 읽기쉬운로그를확보했으며 실제앱설정은바꾸지않았습니다. DB자료/스키마/서버프로세스나 센서작업을 동기화하지 않았습니다.

실행서버의 새physical-result/recovery404와010/011·옵션off는 작성자의 읽기확인 보고이며 이번 코드병합으로 실제서버를 재시작하거나 적용했다고 쓰지 않습니다. 새PC만 구형서버에 연결하면 계약을 소비할 수 없다는 기존 준비조건은 유지합니다. 실제DDL/설정·단일서비스 전환·운영자close/reissue·Pi/PC배포·실물지문과회원매핑·Linuxflock/두PC·전원차단 검증은 별도입니다.

### PR52와 정책 후속

PR52 `0a7a57df97767e81680484222587974747bf6dab`는 코드 변경이 없어 R52-01/P1 표시와POST판단대상 불일치·R52-02/P2 버전충돌 후 재준비 경로 수정 대기를 유지합니다. 1번의 공통3파일 수용 확인은 이 두 결함 해결이나 전체ACK/쓰기503 해제의 대체가 아닙니다. 같은head의 과거185/41/177을 이번에 재실행하지 않았습니다.

기존 팀장 채택A1~A6/B1~B7과90일 화면조회/물리삭제 미확정, 취소/pending의 실제쓰기TX 소비·경합,4번 PC/GUI와 새 정책서비스 후속은 유지합니다. 날짜LOG·기존PR/회신이력을 삭제하거나 별도 회신용MD/PR을 만들지 않았습니다.

<a id="pr55-56-recheck-20261021"></a>
## 최신 재검토 — PR55 추가 수정·PR56 연결 검사 보완·PR52 대기, 2026-10-21

[PR55](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55) head0871b2c의 기존 R55-01~03은 해결 확인했습니다. 공식237검사와 별도9표적·Release0/0이 통과했지만, **R55-04 P2: 운영자 종료 중 초가 바뀌면 종료 성공 후 조회·재실행·재발급이 계속409가 되는 결함**을 독립 재현해 미병합입니다. [고정 코드/재현/수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55#issuecomment-6306292731)을 따릅니다.

새 [PR56](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56) head0bf93bc는 PR55와 결합해 Python168통과/Linux2생략·중계540·전용WinForms31·실제55 서버/소유MySQL 연결2·Release0/0을 확인했습니다. 다만 기준 alpha에서 통과하는 기존 회원관리 연결 검사가 새 physical-result 대역 누락으로 timeout하며, **PR55 수정·선행 병합과 원본 통합 검사 갱신/재실행 전까지 보류**합니다. [담당1·5 연결 보완 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56#issuecomment-6306293058)과 [중앙 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr55-56-recheck-20261021)를 따릅니다.

PR52 head0a7a57d는 코드 변경 없이1번의 공통3파일 수용 확인 댓글만 추가됐습니다. 기존 R52-01/P1 표시·실제 판단 대상 불일치와 R52-02/P2 버전충돌 후 재준비 경로 수정을 기다립니다. 이번에는 어떤 제품 PR도 병합하지 않았으며 실제 작업 폴더·공유 서버/DB·010/011·옵션·Pi/센서를 변경하지 않았습니다.

### PR55 — 이전 세 지적 해결, 새 종료 시각 결합 결함

기준 alpha `bb281e317cdd27e2a4796ad39d8712bbddd9828b`, head `0871b2ce80725587e8c1960d0874f5dc191cf46e`, 검토 결합 tree `5e3869ff86b468383f74695a5594118532f39c1c`입니다. 현재 Ready/CLEAN/MERGEABLE과 실제 검토 판정을 구분합니다.

R55-01은 기존010 증거/011 재발급 이력을 현재 잠금 조회해 hot/cold flagOFF의 legacy 쓰기·미기록완료를 막습니다. R55-02는 capture가 로그인 RR snapshot 대신 현재 잠금 receipt를 읽습니다. R55-03은 첫 원문 및 운영자 보관에도 terminal Job.result_hash를 대조합니다. 기존 독립 재현2건과 공개 회귀6건·011 intent 전 OFF1건의 별도9표적은38.02초에 통과했습니다. 공식237에 포함되는7건은 합산하지 않습니다.

새 **R55-04/P2**는 `enrollment/recovery.py:223,228`의 서로 다른 `svc.now()` 호출과171행의 정확 동등 검사입니다. 원본 CLI/HTTP와 새 소유MySQL에서 clock만01:04:00→01:04:01 초 경계를 재현하면 최초 close는exit0·복구/감사 각1로 commit하지만 closed_at와 source.updated_at가1초 어긋납니다. 같은 확인서 재실행exit1, 상태GET와 새 로그인 reissue는 계속409 ENROLLMENT_RECOVERY_BINDING_CHANGED입니다. job은recovery_required·claim은quarantined로 남습니다. 동일 초 대조군은 재실행/조회/재발급 모두 정상입니다. 한 번 캡처한 stamp를 두 필드에 함께 쓰고, 초 경계를 지나는 회귀로 정상 재사용을 확인해야 합니다. 바인딩 검사를 없애거나 원래 티켓/만료를 변경하는 수정은 요청하지 않습니다.

별도 초 경계2검사는16.20초이며 하나는 결함 관찰 assertion, 다른 하나는 정상 대조군입니다. 이 숫자를 기능 정상 통과 수에 더하지 않습니다. 증거는 `remote_work_pr55_recovery_review_20261021/audit/recovery-finding.json`, `clock-boundary.json`, `clock-control.json`, `clock-owned.txt`입니다.

### 직접 실행한 공식 검사와 안전한 범위

Python3.12.14/pytest9.1.1·MySQL8.2 InnoDB/RR의 폐기 가능한 전용 DB에서 durability/device/approval/protocol/task_creation_migration/task_write_support/recovery/durability_review **8모듈237통과·실패/생략/경고0**입니다. `-W error::pytest.PytestUnhandledThreadExceptionWarning`을 유지하고 WriteSupport HTTP DLL을 선행 빌드했습니다. WriteSupport 및 전체 솔루션 Release는 각각 경고0/오류0, diff검사와 새 cold 프로세스의 기본off·모듈/metadata/route/CLI 미등록·DB미접속을 확인했습니다. 모든 소유MySQL 프로세스·임시runtime을 정리했습니다. 작성자 Python3.14/147.69초 보고와 이번 검토자의 실행을 구분합니다.

증거는 `remote_work_pr55_recheck_independent_20261021/audit/official.txt`, `official-summary.json`, `cold-off.json`, `build.txt`, `write-support-build.txt`; 별도 표적은 `remote_work_pr55_recheck47_20261021/audit/recheck-review.md`, `terminal-conflict.txt`, `terminal-conflict-summary.json`입니다.

### PR56 — 새 MySQL Pi/PC 소비와 미완료 연결 검사

head `0bf93bca7891c3ae73e00a5ceb3f3b9465f85dd6`, 실제 base `bb281e317cdd27e2a4796ad39d8712bbddd9828b`, PR55 0871b2c와의 결합 tree `cc82f2a559cf13bfa1897f196462ed949e17787a`입니다. 24파일2430추가43삭제이며 Python 어댑터/명시 시작·비공개 설정 검사·읽기 전용 복구 점검기, C#relay/form과 소유 회귀입니다. 실제 서버DDL/회원관리/launcher 변경은 포함하지 않습니다.

생성응답 회원·현재 접속·취소epoch·복구 선택·device/key epoch를 고정하고, GET 확인만으로 발급/채취하지 않으며 별도 시작에서 같은 요청UUID/old-new 링크/claim+1을 확인합니다. 응답 유실 뒤 일반issue나 새scan으로 우회하지 않고 현재receipt/고정 원문을 조회합니다. store는 최초 MySQL intent commit의 일회 승인 뒤에만 실행하며 원문 선보관과 servercomplete 이후에 완료를 표시합니다. 새 Python 경로의 SQLite 사용이나 별도 제품 차단 결함은 찾지 못했습니다.

직접 Python 전체 unittest는 **170실행/168통과/Linux flock2생략/실패0·5.273초**입니다. C#합성HTTP/실제Python loopback 중계540, 실제WinForms+합성HTTP31, 전체솔루션Release0/0도 통과했습니다. 추가 실제55 Flask/소유MySQL+56어댑터2검사는9.08초에 통과했습니다. 합성 키·가짜 센서에서 기존11개 보존/슬롯12 store1회/정확BLOB/매핑1·재시작 읽기sensorIO0과 intent commit 응답 직후 취소store0/매핑0/intent증거 보존을 확인했습니다. 실물11개를 검토자가 조작한 검사가 아닙니다. 소유DB/프로세스·runtime 정리를 확인했습니다.

원본 `dotnet run --project Tests/UserManagement.Ui.Tests/UserManagement.Ui.Tests.csproj -c Release`는 결합본에서 **FAIL after46: TimeoutException: Modal registration driver**, `EnrollmentConnectionChecks.cs:156`으로 실패했습니다. 기존 mock handler의241행까지 GET/POST physical-result가 없어서 새relay의 선행조회에서 중단됩니다. 별도 기준 alpha clone에서는 같은 원본 검사가 연결112 + 회원관리14시나리오46검사로 통과했습니다. 실제 서버 장애나 제품 등록 성공을 입증한 결과가 아니며, 원본 fixture를 새 계약에 맞추고 동일 원문 선보관/완료 순서를 검사한 뒤 원본 suite를 재실행해야 합니다. 작성자의 임시 복사본120통과를 원본 통과로 기록하지 않습니다. PR55 수정·선행 병합도 필요합니다.

증거는 `remote_work_pr56_review_20261021/audit/root-checks.json`, `remote_work_pr56_csharp_review_20261021/audit/static-review.json`, `remote_work_pr56_adapter47_20261021/audit/`의 Python/실제결합 로그와 회귀입니다. 이번 검토는 제품 소스를 수정하지 않았습니다. Linuxflock·실제 공유환경·센서·두PC·전원차단은 미검증이며 코드검사, PR병합, 실제환경 적용, 운영자close/reissue, 실물 지문저장/회원매핑을 구분합니다.

### PR52와 정책 기준 유지

PR52 head `0a7a57df97767e81680484222587974747bf6dab`는 동일합니다. [1번의 공통3파일 수용·조직 자료 현재 상태 확인](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6305999537)을 수신했지만 R52-01/02 코드 수정과 실제 취소/pending 소비·경합·검사경로 이식성의 대체가 아닙니다. 동일head의 과거185/41/177을 이번에 다시 실행했다고 표시하지 않습니다. [기존 팀장 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/52#issuecomment-6287667210)을 유지합니다.

팀장이 이미 채택한 A1~A6·B1~B7 기준과90일 화면조회/물리삭제 미확정 구분도 유지합니다. 전체ACK/실제쓰기503 해제, 지문010/011 적용·옵션/실제서버 재시작·Pi/PC 배포·실물 복구는 실행하지 않았습니다. 이번에는 원본alpha 제품/실제작업폴더에 반영하지 않고 기존 PR/LOG 문서에 재검토 결과만 추가합니다.

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
