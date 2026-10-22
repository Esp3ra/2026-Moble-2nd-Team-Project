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
