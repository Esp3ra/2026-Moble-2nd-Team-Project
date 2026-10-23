<a id="pr86-88-current-20261023"></a>
## 최신 — PR86·87·88 검토와 현재 병합 상태

|PR|고정 head|현재 판정|
|---|---|---|
|[86](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/86#issuecomment-6359071539)|`3caa6c033b3b45f309dc2d663b3de23d84f48933`|기술 검토 통과·팀장 정책 확정 대기/미병합|
|[87](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/87#issuecomment-6359071873)|`0ecf6807f143987c2ae456a9fc48559993737ccd`|기술 검토 통과·팀장 정책 확정 대기/미병합|
|[88](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/88#issuecomment-6359006697)|`f365953df50edb33360ebd976461fa6ab02a64f2`|병합 e2ee34bf|

86은 퇴근 후 당일 일반 비밀번호 접속과 근태 종료 유지,87은 지문 템플릿을 보존한 관리자 계정 연결 이전,88은 재확인한 기존 회원의 명시적 지문 신규 등록 UI입니다. 86·87은 다른 대화에서 승인받았다는 PR 작성자의 보고와 본 채팅 팀장의 직접 결정을 구분해 정책을 확인 요청한 상태입니다. 코드 차단 결함은 없지만 정책 확정 전에는 병합하지 않습니다.

직접 독립 MySQL86의74PASS/1skip,87의239PASS/4skip(새transfer20포함),별도추가6,세PR결합32와88실제폼/통제HTTP·Pi대역341/95/58/14시나리오46·Release0경고0오류를 확인했습니다. 각 밴드의 포함/반복/생략은 합산하지 않습니다. [정확한 검증·한계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr86-88-reviewed-20261023)·[정책/담당 후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr86-88-policy-20261023). 실제서버/공유DB·env/키/슬롯·센서·운영flags/ACK·두PC는 이번 검토에서 변경/검증하지 않았습니다. 아래는 이전 기록입니다.

<a id="owner5-pr80-merged-20261023"></a>
## 5번 최신 — PR80 R80-01 해결·추가 Pi 관측 검사 병합 완료

[PR80](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/80#issuecomment-6354991039) **70d3d924→1b83e4f3** 병합 완료입니다. `workday:null`이면Pi topology를읽지않고,근무연결요청시기존필수설정검사를유지합니다. 실제파일Load/Main동일Run회귀를포함한 **Workday95(기존81포함)·LeaveUI144·IdentityUI179PASS**, Release0/0, 현재79adapter의실제연차소비4PASS/29.14초를직접확인했습니다.79의8730b9ea→dc823221수정병합을보존합니다.

추가Pi관측부 원266실행/263PASS/Linux3skip·5.997초와 별도합성bundle21PASS를 확인했습니다. 첫tzdata누락실패는검사환경만교정/원소스불변이며수치합산안함. Pi전체는기존임시SQLitefixture를포함하고실제연차MySQL4와다른범위입니다. `--restart-bundle`은check-config전용·bool금지·관측은ready권한아님, CLI독립sourcepin미제공한계를유지합니다.

이병합은실제Linux/private설치·trustedhost/cycle대조·서비스drain/동일lock/UART·새process·별도ON·실물/두PC완료가아닙니다. D1/D2와실제연차쓰기설정도별도입니다. [정확한 검사](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr80-83-reviewed-20261023)·[후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr80-83-boundaries-20261023). 실제설정/키/서비스/센서 변경없음,아래보류는이전이력입니다.

<a id="owner5-pr80-review-20261023"></a>
## 5번 최신 — PR80 연차 전용 실행 준비 P2 수정 요청

[80](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/80#issuecomment-6354991039) **11b216b2**는 R80-01/P2로 미병합입니다. `WorkspaceProfile.Load`가 `workday:null/leave_enabled:true`에도 `EnrollmentEndpoints.Load`를 호출해 지문 주소 설정이 없는 PC에서 로그인 전에 종료됩니다. 원 C# 소유설정 probe에서 Parse의 연차 전용 허용과 실제 Load 실패를 대조했습니다. 근무 연결을 명시한 경우에만 Pi 설정을 읽도록 분리하고 연차 전용 Load 회귀를 추가해 주세요.

PR79 고정88f78fa와 결합해 Release0/0, Workday81·LeaveUI144·IdentityLogin179, Pi255실행/252통과/Linux3생략 및 실제 MainForm/폼→adapter→Flask/W1→소유MySQL4 PASS를 확인했습니다. 이4개는 문제의 startup Load를 거치지 않아 반례를 부정하지 않습니다. [79](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/79#issuecomment-6354990801) 별도 서버 현재권한 P1/P2도 보완 대기입니다.1번 제공부/5번 소비가 미제공인 상태로 되돌리지는 않습니다.

[82](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/82#issuecomment-6354944328) 실행/관측 공급은 병합됐지만 실제 Pi ready(profile) 결합·private 전달 수신/서비스 drain/전환·새지문ON·실물/두PC는 별도입니다. D1/D2 연차 관계·계산정책도 새로 승인하지 않습니다. [중앙 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr78-82-reviewed-20261023)·[담당 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr78-82-boundaries-20261023). 아래는 이전 이력입니다.

<a id="pr58-owner5-leave-request-current-20261022"></a>
## 5번 최신 — PR74 소비 완료 유지 / PR58 연차 추가 계약 대기

PR58 [1번17:11:39](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309878130)는 PR74 독립 소비를 수신했고, [5번17:12:14](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309485288)는 연차 전체 서버권한·잔여일/원 UUID 결과복구 계약을 추가 요청했습니다. 그 새 연차 요청에1번 후속회신은 아직 없습니다. [AttendanceControl](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/36f1e55d44cd3fa2fd2c7f634fbcd0d059ce7a9d/RemoteWork.WinForms/Features/Attendance/AttendanceControl.cs)·신청/결정 폼의 기본은 [UnconnectedLeaveWorkflow](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/36f1e55d44cd3fa2fd2c7f634fbcd0d059ce7a9d/RemoteWork.WinForms/Features/Attendance/LeaveWorkflow.cs)이며 실제 신청·조회·승인을 저장하지 않습니다.

필요한1번 계약은 캡처접속·본인목록/배정관리자 권한·원 요청UUID/버전·한TX 차감/잔여일/감사·응답유실 결과복구입니다. 기존 병가·공가 미차감은 유지하고 부여기간·휴일/주말·반차·차감/반려복원·중복·과거/연도·반려사유 필수 여부를 새로 확정하지 않습니다. [미확정/후속](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member2-pr7576-pr58-boundaries-20261022)으로 취합합니다.

PR74 기본 공통 퇴근 factory/reader 소비 제공 상태를 유지합니다. [75](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/75#issuecomment-6336468832)/[76](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/76#issuecomment-6336589781) 병합은 새 근태조건·loader/startup/실장비·공유활성화가 아니며 새지문OFF·같은LAN 사설IP+PORT를 보존합니다. 작성자 운영보고와 이번 직접 검증은 [실행 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr7576-pr58-review-20261022)에서 구분합니다.

<a id="pr74-member5-default-connected-20261022"></a>
## 5번 최신 — 실제 기본 공통 퇴근 연결·복구 검증 완료

[PR74 최종4452996](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/74#issuecomment-6336089532)을 **652b21a20b08fd7c4e3d77332a7d219366dac779**에 병합했습니다. tree **d527263428820b2b39acef5e42053331b94b5ae4**는 검증본과 같습니다. `ConnectWorkdayActivityAsync(options,...)`가 실제 병합 reader를 사용하고, prepare factory 생략 시 제품의 `PrepareWorkdayClockOutAsync`가 PR73 공통 Prepare/Complete/Finish를 연결합니다. 초기 시험용 ClockOutPeer factory/컴파일 옵션은 제거했으므로 최신 연결을 ‘소비 미제공’으로 안내하지 않습니다.

verified 퇴근 후 원 근무/DATE·challenge/UUID로 UI 스레드에서 준비하고 monitor·진행 조회를 기다린 뒤 공통 drain/결과 소유권을 사용합니다. 응답유실·빌린 폼 재오픈은 원 operation/공통 소유자를 재사용하며 새 스캔·ordinary finalize로 바꾸지 않습니다. 사용자 확인·창 닫기 뒤 검증된 같은 receipt로만 Finish하며 auth/logout 추가 전송이 없습니다.

최신 실제 폼51(이전26 포함)·Owner11·솔루션 Release0/0과 원 runner의 C# MainForm/reader/폼/common/relay→원 서버·Pi HTTP·소유MySQL14 PASS/65.28초를 확인했습니다.14에 기존10+정오2+퇴근2가 포함됩니다. 초기031의179/115/35·12시나리오·이전14/64.38초와 합산하지 않습니다. [정확한 DLL/소스·대역·정리](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr74-reviewed-merged-20261022)·[연결 규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/652b21a20b08fd7c4e3d77332a7d219366dac779/RemoteWork.WinForms/Features/Attendance/CLOCK_OUT_CONNECTION.md)을 따릅니다.

기본 시작 자동 설치·장치 loader/카메라/실물 Pi·두PC·Linux 준비·공유 활성화/키·보존 정리는 후속입니다. 실제5000/.env·DB/운영 옵션·키/등록 지문/회원 매핑은 미변경이고 새지문OFF를 유지했습니다. 같은 LAN 사설IP+PORT 기준을 보존합니다. 코드 연결/시험 통과를 운영 readiness나 실물 로그인 완료로 바꾸지 않습니다.

<a id="pr73-member5-consumption-20261022"></a>
## 5번 인계 — 공통 퇴근 결과 소유권 제공·명시 소비 대기

[PR73](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/73#issuecomment-6335730169) 최종 head **5070911**(초기b442에 최신alpha 수신)를 Draft→Open 후 **f542902e3edfa551e33eb97ed81f4ef12eff27c3**에 병합했습니다. 최신7692ce4 결합 tree **0a6ee3ad17fb5ed1610088eb065c94957f791248**와 실제 병합본이 일치합니다. [공통 소유권 규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/f542902e3edfa551e33eb97ed81f4ef12eff27c3/docs/DEVICE_IDENTITY_V2.md)에서 `MainForm.PrepareClockOutFinalizationAsync`/`FinishConfirmedClockOut`와 로그인 소유 `ClockOutFinalization`의 수명을 확인합니다.

5번 연결 위치는 `FingerprintWorkdayOperation`의 **verified clock_out 이후 finalize 경계**입니다. 기존 원 work UUID/DATE·challenge·OriginalFinalizeId를 그대로 전달하고 monitor/근무 poll의 종료·대기 callback을 먼저 연결한 뒤, 직접 일반 Session finalize를 같은 소유자의 `CompleteAsync`로 바꿉니다. 불명일 때 새 작업/UUID를 만들지 않고, 검증 receipt 표시 후 같은 소유자로만 완료합니다. 정오 경로·단순 창닫기/Logout은 기존 의미를 유지합니다.

공통35검사/12시나리오·MainForm11·기존144/29·26/179/115·Release0/0, 실제 원 C#·Flask/AttendanceWorkflow·소유MySQL2 PASS/10.87초를 확인했습니다. 성공응답 유실 뒤 원 작업 복구와 AuthSession0/근무ended/receipt1을 대조했으며 서명·회원·매핑/스캔은 fixture입니다. [상세 검사·선행 실행·정리](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr73-reviewed-merged-20261022)를 구분합니다. 실제5번 제품 소비·센서/두PC·서버 운영 준비 완료를 의미하지 않고, 실제5000/.env·DB/키·새지문OFF는 변경하지 않았습니다. 정지 monitor의 임의 재시작·앱 재시작 뒤 RAM 복구를 제공하지 않습니다.

<a id="pr71-pr72-merged-20261022"></a>
## 근무일 조회·정오/근무 감시·연차 보완 병합, 2026-10-22

[PR71](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/71#issuecomment-6335251016) head81eb880의 현재 근무일 일반 인증 GET/C# 연결을 **30fcb30d**, [PR72](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/72#issuecomment-6335251407) head10f80ff의 정오 지문·근무 감시 수명·연차 화면 보완을 **7135a293**에 병합했습니다.71은 사용자 지시에 따라 Draft→Open 후 병합했으며 실제 merge tree가 선행70과 각 검토본 결합에 일치합니다.

71은 원 서버9 PASS/36.83초·독립 RR2 PASS/12.13초·C#39입니다.72는 WorkdayActivity26·로그인/근무179·ActivityPresence297/12시나리오·Leave115·Preview11 PASS이며 실제 C#+명시 reader/monitor/센서/HTTP/workflow 대역입니다. 고정71 reader를 외부 시험 Link로 연결한 실제 서버/Pi HTTP·소유MySQL·C#12 PASS/71.74초는 별도 범위입니다. 최종7135 결합본 Release0/0·26/39도 확인했으며 합산하지 않습니다. [전체 근거·초기3.12 시계 기반 실패와 원소스/DLL 그대로3.14.7 QPC 환경 재검증](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr707172-merged-20261022)을 보존합니다.

기본 자동 연결·실제5000/.env·공유DB/DDL/키·센서는 미변경입니다.71의 검증된 값을72의5필드 상태로 명시 매핑하는 wrapper,퇴근 finalize 중 drain/401 뒤 원 결과복구 수명,실제 monitor/센서·두PC·운영 loader/연차 API·권한/잔여일은 후속입니다. 정상 Logout을 필수 지문 퇴근 성공으로 표시하지 않습니다.

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

<a id="pr67-fixed-merged-status-20261022"></a>
## PR67 수정 재검토 — R67-01 해결·alpha 병합 완료, 2026-10-22

head **d9500fec6df9bc0e4797869bee92291345a5fa1f**에서 **R67-01/P2 해결**을 독립 확인했습니다. 검사 자식의 생성/Job 등록/재개 전부터 기존 공통 run_owned_client의 정리를 적용합니다. 공식 실패 주입7검사와 이전 main 경로의 독립 assign 실패 회귀에서 소유 자식 stop/wait·종료·출력 스트림 닫힘을 확인했습니다. 수정 runner의 실제 소유 MySQL/서버·Pi HTTP 검사도 **10 PASS/63.01초, exit0·소유 DB/프로세스/runtime 정리 완료**입니다. 추가 병합 차단 문제를 발견하지 못해 [alpha에 병합](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/5dcd66e291a0b12470c3347909653a64458ef70e)했습니다.

실제 merge의 tree **4fafbaa04286356832a3959eddc5d536d3cef99a**가 최신 base f534b68+검토 head의 결합 tree와 정확히 같습니다. Release 빌드 0오류/0경고입니다. [최종 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/67#issuecomment-6330831748)·[상세 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr67-fixed-merged-20261022)를 따르며 아래 PR67 보류 표시는 이전 head 당시 이력입니다. 센서·키/계정·준비상태는 대역이고 C# UI·실물/두PC·공유 서버 가동 검증은 후속입니다. 실제5000/.env·공유 DB/DDL·운영 키·Pi/센서는 변경하지 않았습니다.

<a id="pr66-pr67-status-20261022"></a>
## 추가 PR 검토 — PR66 병합·PR67 수정 요청, 2026-10-22

**PR66** head b8825dd의 마감 알림 확인 성공/목록 재조회 분리를 검토했습니다. 공식 마감162·업무 이동24·공지35·협업115검사와 화면 수명·경합 독립19검사 PASS, Release 0오류/0경고를 확인해 [alpha에 병합](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/8cc082155561ea060e3676c346aed1d87fb8e5e9)했습니다. 실제 병합 tree **8723d2a270dd03bd15f3d8bdc4f096c25b733ab5**는 검토본과 정확히 같습니다. source가 원 사건/계정·W1 key/body의 성공을 검증한 뒤 목록 실패와 분리하는 내부 계약이며, 실제 마감 API/DB/source 설치 완료는 아닙니다.

**PR67** head a4b9d58은 실제 서버·Pi HTTP/MySQL 회귀4파일 추가입니다. 독립 소유 MySQL/두 loopback HTTP 10PASS/55.61초, exit0·소유 DB/프로세스/runtime 정리를 확인했습니다. 정상 경로와 별도로 **R67-01/P2: Job 등록 실패 시 생성된 suspended 검사 자식 정리 누락**을 대역 재현해 [수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/67#issuecomment-6330831748)했습니다. 기존 PR67에 수정 후 재검토하며 현재 미병합입니다. 고정 source의 server/Pi와 합성 센서를 잇는 검사로 실물·C# UI·공유 서버 성공과 구분합니다.

[상세 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr66-pr67-review-20261022)에 정확한 SHA·검증 범위·후속을 취합했습니다. 실제5000/.env·공유 DB/DDL·운영 키·Pi/센서는 변경하지 않았습니다. 아래 이전 PR 상태는 각 당시의 기록입니다.

<a id="pr62-merged-status-20261022"></a>
## PR62 최종 재검토 — 두 지적 해결·alpha 병합 완료, 2026-10-22

최신 head **95ef89dfa8d9478e0cb6c3cb26282cd43c09a820**에서 **R62-01(FK 참조 DB)·R62-02(5분 뒤 동일 결과 복구) 해결**을 독립 확인했습니다. 추가 병합 차단 문제를 발견하지 못해 [PR62](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/62)를 alpha에 병합했습니다. 실제 merge [a6427ad](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/a6427adf221074d27df143c02f3386b2ed073642)의 tree **84c01fb8ec05cd59241cbaf1ccfe3367a79d4663**는 검토한 결합본과 정확히 일치합니다.

최신 pending 공식43검사·독립20검사, 동일 서버 소스의 FK HTTP1검사·인증/codec120검사를 확인했고 최종 Release 빌드는 0오류/0경고입니다. 서버39대상의 최초38PASS/정오1FAIL과 실행기 세션 설정 수정 후 해당1검사 PASS는 [상세 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr62-merged-20261022)에 구분했습니다. [기존 댓글의 최종 판정](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/62#issuecomment-6330262776)을 따르며, 아래 미병합/미해결 표시는 이전 head 당시 이력입니다.

**코드 병합과 실제 적용은 구분합니다.** 기본 OFF·검토용 SQL 초안6테이블 상태입니다. 공유 서버/DB·정식 migration·키/프로필/provider·LoginForm/MainForm/5번 relay·정상 토큰 UI 인수·Pi/두PC 검증은 후속이며 이번에 실제 환경을 변경하지 않았습니다. 만료 후 C# 복구는 기록된 GET/원 finalize로 한정됩니다. 보관 문맥의 명시 Revoke/폐기는 실제 UI 수명 연결 시 함께 대조합니다.

<a id="pr62-latest28-still-blocked-20261022"></a>
## 최신 PR62 추가 변경 확인 — R62-01 미해결·보류 유지, 2026-10-22

마무리 확인 중 PR62 head **28cd8d94108247ebc656be27e17a151da19795f5**가 추가됐습니다. 이는 외래키 수정이 아니라 근태 finalize·암호화 영수증·같은 날 근무 재로그인·전용 C# pending 연결 등15파일의 기능 확대입니다. **R62-01/P2는 그대로 남아 미병합을 유지**합니다. 최신 `features/identity/routes.py:76~82`의 FK 집합/기대값 AST는 실제 HTTP 결함 재현본 b71과 동일하고, storage()는 여전히 referred_schema를 비교하지 않습니다. [기존 댓글 한 곳의 최신 판정·정확 위치](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/62#issuecomment-6330262776)을 따릅니다.

추가로 **R62-02/P2**를 확인했습니다. 새 `IdentityPendingConnection.cs:21~22,55,91~106`은 최초5분 ExpiresAt으로 결과 복구까지 차단합니다. 서버 `attendance.py:148~159`는 확정 뒤 같은 문맥을 receipt_only/보관기한으로 연장하므로, commit 뒤 응답 유실·초기5분 경과 시 결과는 남아도 전용 연결은 HTTP 전에 취소됩니다. 원본 C#와 HTTP 대역에서 같은 원FinalizeID 복구 요청의 총HTTP1회·OperationCanceled를 재현했습니다. 신규 활동 권한과 이미 보낸 동일작업의 결과전용 복구 수명을 분리하고 현재접속/권한·취소를 유지하는 회귀를 요청했습니다. 공식 pending15검사는 직접통과했지만 이 경계는 포함하지 않습니다. 실제 근태DB 왕복/확대전체검증과 구분하며 **R62-01/02 수정 전 미병합**입니다.

63·64·65의 병합과 실제 merge tree 일치는 유지됩니다. 아래 b71의32PASS·인증/코덱 검토·finalize503 안내는 당시 범위입니다. 새28cd에는 finalize/정상토큰 전환 코드가 게시됐으므로 전체 미구현으로 부르지 않지만, 새 근태/C# 전체의 HTTP·DB·동시성 검증을 완료하거나 운영 정책/배포를 승인한 것은 아닙니다. 추가 auth/receipt 전이의 짧은 정적 대조와 기존 FK 미해결 확인만 수행했습니다. 실제 DB·키·서버/센서·환경은 변경하지 않았으며, 실제 MySQL의 현재/다른DB 동명 FK 회귀와 R62-01 수정을 기존 PR62에서 요청합니다.

## 최신 — PR63·65 병합과 PR62 서버 수정 대기, 2026-10-22

[PR63](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/63#issuecomment-6330234285) head4ca48de는 **2eeafe9**, [PR65](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/65#issuecomment-6330376932) headb0f3eb2는 **18a4855**로 병합했습니다.63직접 Python228실행/226통과·POSIX2생략, C#143·loopbackHTTP110 통과입니다. 새65직접 Python251실행/248통과·Linux3생략, 새runtime loopbackHTTP110·독립import/기본차단16을 통과했습니다.65의기존143은 동일blob의이전근거 재사용이며 재실행/합산하지 않습니다.

비공개 loader·키분리/슬롯/권한보호·read-only CLI, 명시factory/공유UART·단일pool·기본503/한번실패뒤재개금지·drain을 확인했습니다. 실제Linux owner/ACL/link·BOOTTIME·비공개키/회수키검사·서비스설치/실센서·재시작상태조정은 미실행입니다. 운영readiness를 상수True/health200/대기로 열지 않습니다.

[PR62](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/62#issuecomment-6330262776)는 실제HTTP/MySQL서비스를 게시했지만 R62-01/P2 외부DB FK준비검사 수정요청으로 미병합입니다. 미제공전체로 취급하지 않으며 실제공급/동일원문·현재관계와pending UI/근태finalize·정상토큰 전환 후속을 구분합니다. [중앙 검사와 실제환경 경계](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr62-65-review-20261022)를 따릅니다. 실제 키/설정/DB·Pi8080/센서·회원매핑은 이번에 읽거나 변경하지 않았습니다.

## 최신 재검토 — PR55·PR56 병합 완료, PR52 코드 수정 대기, 2026-10-21

[PR55](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55) headbc1751d의 R55-04 단일 종료 시각 수정과 회원관리 연결 fixture 보완을 최신 alpha864a2a6에서 검증하고 **`b37fe3a38babe1553fc6d78e39e4b8c07ce0d822`**로 선행 병합했습니다. 공식8모듈239통과·독립 초 경계4통과·Release0/0이며 기존R55-01~04는 모두 해결 확인입니다. [최종 검토/범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55#issuecomment-6306967504)를 따릅니다.

이어서 [PR56](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56) head0bf93bc를 **`395d70d84172f332d9f47e162aa64b9bb5adaa57`**로 병합했습니다. C#중계540·전용WinForms31·회원관리 기본173/필수177(각 기존14시나리오46)·Python168통과/Linux2생략·실제서버/소유MySQL 어댑터2가 통과했습니다. 두 병합의 실제tree가 사전 검토tree와 일치합니다. [최종 검토/미실행 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56#issuecomment-6306967787), [중앙 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr55-56-merged-20261021)를 따릅니다.

실제 작업 폴더에는 병합된46경로만 백업 후 동기화했고 DOCX·실제설정은 보존했습니다. 실행 서버 재시작·실제010/011·옵션/서비스 전환·운영자close/reissue·Pi/센서·실물 등록은 실행하지 않았습니다. 코드 병합과 실제 서버의 새 경로404 해소/실물 검증은 별도입니다. PR52 head0a7a57d는 동일하여 기존R52-01/P1·R52-02/P2 코드 수정 대기를 유지합니다.

## 최신 재검토 — PR55 추가 수정·PR56 연결 검사 보완·PR52 대기, 2026-10-21

[PR55](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55) head0871b2c의 기존 R55-01~03은 해결 확인했습니다. 공식237검사와 별도9표적·Release0/0이 통과했지만, **R55-04 P2: 운영자 종료 중 초가 바뀌면 종료 성공 후 조회·재실행·재발급이 계속409가 되는 결함**을 독립 재현해 미병합입니다. [고정 코드/재현/수정 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/55#issuecomment-6306292731)을 따릅니다.

새 [PR56](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56) head0bf93bc는 PR55와 결합해 Python168통과/Linux2생략·중계540·전용WinForms31·실제55 서버/소유MySQL 연결2·Release0/0을 확인했습니다. 다만 기준 alpha에서 통과하는 기존 회원관리 연결 검사가 새 physical-result 대역 누락으로 timeout하며, **PR55 수정·선행 병합과 원본 통합 검사 갱신/재실행 전까지 보류**합니다. [담당1·5 연결 보완 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56#issuecomment-6306293058)과 [중앙 근거](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr55-56-recheck-20261021)를 따릅니다.

PR52 head0a7a57d는 코드 변경 없이1번의 공통3파일 수용 확인 댓글만 추가됐습니다. 기존 R52-01/P1 표시·실제 판단 대상 불일치와 R52-02/P2 버전충돌 후 재준비 경로 수정을 기다립니다. 이번에는 어떤 제품 PR도 병합하지 않았으며 실제 작업 폴더·공유 서버/DB·010/011·옵션·Pi/센서를 변경하지 않았습니다.

## 최신 검토 — PR48 센서·OLED 공유 구성 병합, 2026-10-20

신규 [PR48](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/48) head `a4407f1`를 alpha `df2af6d`와 결합 검토해 추가 차단 결함 없이 `73b17e0`에 병합했습니다. 실제/검증 tree `a193994c2aa98bbb5a4e3d86bb082845b72c903f`가 일치합니다. 기존 단일 UART·센서 잠금·OLED를 빌려 명시 factory로 등록 runtime을 조립하며 **기본 실행은 등록 비활성**입니다. 전체Pi123개 중122통과/Windows POSIX권한1생략, 독립동시성3개 통과, Release0경고/0오류입니다. 45번 공지 삭제 정책과2번 결과 제출 연결·공통 코드는 보존합니다.

[고정 SHA·실제 검사·남은 연결](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr48-review-20261020)을 따릅니다.

## 최신 재검토 — PR44·46·47 병합 / PR45 Draft 유지, 2026-10-20

[PR44](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/44) R44-01과 [PR45](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/45) R45-01·02의 수정 업로드를 확인했습니다. **44번은 수정 재검증 후 병합, 45번은 기존 지적 해결·보존 구현 미완료로 Draft/미병합**입니다. 팀장의 이번 “46번은 수정됐으니 검토” 지시로 이전 보류를 해제하여 [PR46](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/46) 실제 제출·검토 코드를 검토했고, 신규 [PR47](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/47) 등록 중계도 검토했습니다. 44→47→46 순서로 alpha에 병합했으며 최종 코드 merge `1d2cb6b`/tree `0608974b29eb5996f67d34c14b63ae8fbf4fa51f`는 검증본과 일치합니다. 아래 이전 미해결·46보류·미병합은 작성 시점의 이력입니다.

47 등록 코어·온라인 승인·C# 중계/등록창은44 서버와 결합 검토 후 병합됐습니다. 실제 회원창 연결·센서/공유 환경 검증은 후속입니다. [검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr44-47-recheck-20261020).

## 최신 검토 — PR40·41 코드 / PR42 회신 병합, 2026-10-19

[PR40](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/40) 1번 공통 지원은 alpha `7073817`, [PR41](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/41) 5번 독립 감지 서비스/공유 view는 alpha `763f11e`에 검토 후 병합했습니다. 새 차단 수정 요청은 없습니다. 직접 검증은40번 Python107·C#31,41번 C#294/12시나리오·Python30, 결합 Release 경고0/오류0이며 실제 병합 tree와 일치합니다. [PR42](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/42) 4번24개 통합 회신은 기존2문서/원문·링크/앵커를 검토해 LOG `2e6a439`에 병합했습니다. 문서 검토 때문에 제품/DB 시험을 추가 실행한 것은 아닙니다.

40번은 독립 읽기 점검/바이트 초안/화면 요청 수명 도구이고41번은 소유자가 주입할 서비스 준비입니다. 실제 본인 확인 서버·근태 저장·출근~퇴근 MainForm 연결·실장비/공유DB/두PC는 아직 구현/검증 대상입니다. 회신 수신을 제안 정책의 확정으로 확대하지 않습니다. 아래의 이전 대기/미연결은 해당 시점 기록입니다.

[독립 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr40-pr41-pr42-review)에 근거를 남겼습니다. 추가 차단 수정 요청 파일은 만들지 않습니다.

# 5번 PR 수정 요청

## 최신 검토자 결과 — PR23·32·33 병합, 2026-10-19

코드 #23 head7af259d의 **R23-01~03 해결·새 수정 요청 없음**을 확인해 alpha `5d9602d`로 병합했습니다. 관련 문서 #32(LOG `54aeae5`)·#33(PR `e4955b7`)도 수신·병합했습니다. 이전에 실패한 환경에서 수정된 원본 C#264검증/11시나리오·Python30개·독립 지연 검사·Release 경고/오류0을 확인했고 병합 tree도 검증본과 일치합니다. [5번 최종 검토](2026-10-19_PR23_과거프레임_지연검사_수정요청.md)를 따릅니다. 실제 장비/공유 DB·회원/근태·백그라운드 통합은 별도입니다. 아래 과거 미병합/실패는 당시 기록입니다.

## 최신 — R23-03 게시 회신·재검토 대기

[R23-03 게시/검증 회신](2026-10-19_PR23_과거프레임_지연검사_수정요청.md): 사용자 승인 후 최신 원본alpha a8d368e 반영·fork alpha7af259d push 완료. 최종 결합에서 C# Release264검증/11시나리오·Python30개·Release 경고0/오류0. WM_CLOSE·소유 확인·실제 안내 소멸 검증이며 제품 코드 변경은 없다. 기존PR23 갱신·협업PR32/수정회신PR33 게시 완료, 검토자 재검토·원본 코드병합은 대기다. 아래 로컬/과거 결과는 보존한다.

## 로컬 5번 회신 — R23-03 수정·업로드 대기

[R23-03 로컬 수정 회신](2026-10-19_PR23_과거프레임_지연검사_수정요청.md): 코드e5a3f09, 소유 안내에 WM_CLOSE를 보내고 IsWindow로 실제 종료 후 로그인 복귀를 검사. C# Release264검증/11시나리오·Python30개·Release 경고0/오류0. 제품 코드 변경 없음, 검토 환경 재실행은 대기다. 코드/문서 새push·PR갱신은 하지 않았다. 아래 원문/검토 이력은 보존한다.

## 최신 독립 재검토 — 2026-10-19

문서 #30(LOG)·#31(PR)은 수신·병합했습니다. 코드 #23 head8191b43의 R23-01 지연 방어와 R23-02 종료 동작은 확인했습니다. 다만 원본 C# 회귀가 만료 안내 닫기에서 실패해 **R23-03 검사 안정성 P2 요청·코드 미병합**입니다. 안내 닫기만 조정한 검토용 복사본260/11 통과와 원본 실패를 구분합니다. [5번 최신 요청](2026-10-19_PR23_과거프레임_지연검사_수정요청.md)을 따릅니다. 아래 이전 R23-02 미해결 표시는 과거 검토 시점입니다.

## 최신 5번 회신 — R23-02 수정·재검토 대기

[작성자 수정 회신](2026-10-19_PR23_과거프레임_지연검사_수정요청.md): 최신 원본alpha abd219b를 반영한 코드8191b43, 부모 종료 재개·정리 공유. C#260검증/11시나리오·Python30개·Release 경고0/오류0, 실제 로그인 화면+loopback401 대역의 복귀와 응답 없는 Python 강제 정리를 확인했다. 사용자 승인으로 코드PR23 갱신·협업PR30·수정회신PR31 게시 완료, 검토자 재검토 대기다. 아래 검토 결과/원문은 이전 시점 기록으로 보존한다. 실서버/DB·장비통합 완료가 아니다.

## 최신 — R23-01 해결·R23-02 수정 필요, 2026-10-19

[PR23 검토자 결과](2026-10-19_PR23_과거프레임_지연검사_수정요청.md): head dd3b3d6의 과거 프레임 문제는 해결 확인했으나 카메라 정리 중 인증 만료 시 메인 종료가 취소되는 새 P2(R23-02)를 재현했습니다. 코드 #23은 미병합이며 수정 후 재검토가 필요합니다. 문서 #27(LOG a3f4aac)·#28(PR f5fa07e)은 작성자 보고 수신으로 병합했습니다. 실제 장비·근태 통합 승인이 아닙니다.

## 이전 검토 기록

2026-10-19 · [R23-01: 과거 프레임의 최신 활동 표시](2026-10-19_PR23_과거프레임_지연검사_수정요청.md)는 P2 재현·수정 요청 상태입니다. PR #23 head `1592644`는 미병합입니다. 문서 #24만 LOG `afd990b`로 병합했으며 코드 승인·실장비/근태 통합 완료가 아닙니다.
