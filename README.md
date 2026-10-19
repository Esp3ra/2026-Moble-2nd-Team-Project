# PR 수정 요청

## 현재 — PR #21 채팅 코드 수정 요청, 2026-10-19

[R21-01~03 수정 요청](4번/2026-10-19_PR21_채팅저장_수정요청.md)에서 같은 세션의 정보 갱신을 로그인 변경으로 오인하는 문제, 공통 초기화에 미확정 채팅 DDL이 포함되는 문제, 서버/C# 메시지 길이 불일치의 P2 3건을 확인합니다. 정확한 head `832478e`의 HTTP 대역·MySQL mock DDL 재현이며 실제 MySQL 저장 검증은 아닙니다. 코드 #21은 Draft·미병합입니다.

협의 문서 [PR #22](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/22)만 LOG `0339b29`로 병합했습니다. 수정 후 새 head 검토, 1번 API/DDL 회신, 저장 통합 검증과 Ready 확인이 남아 있습니다. 아래 과거 완료/대기 기록과 별개인 신규 요청입니다.

## 최신 검토자 확인 — PR #17·#18 병합

[공통 접속 범위·기능 계약 검토 결과](1번/2026-10-17_PR17_접속범위_기능계약_검토결과.md): 코드 #17은 alpha6f5f113, 문서 #18은 LOGdd75964로 병합했습니다. 검토 범위에 신규 수정 요청은 없으며 검토자 소유 MySQL168·접속125·빌드 경고/오류0을 확인했습니다. [2번의 후속 연결과 W3 남은 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_W1_후보_쓰기준비_화면연결.md)를 따릅니다. 아래 과거 대기 상태는 작성 당시 기록으로 보존합니다.

## 최신 검토자 확인 — PR #15·#16 병합, 2026-10-17

[W1 공통 지원 검토 결과](1번/2026-10-17_PR15_W1_공통지원_검토결과.md): 코드 #15는 alpha `5fd99a8`, 문서 #16은 LOG `54c6cd3`로 병합했습니다. 이번 범위에서 신규 수정 요청은 없습니다. 공통 지원 수신 완료와 제품 업무 쓰기·W3 미정 정책·공유 DDL/버전/두PC 미완료를 구분합니다. [실제 검토·검증 상세](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_PR15_PR16_공통지원_검토_병합.md)를 따르며 아래 이전 대기/미구현 표현은 당시 기록입니다.

## 최신 검토자 확인 — PR #7·#13·#14 병합, 2026-10-17

**코드 [PR #7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/7)은 재검토 후 alpha에 병합했습니다.** 제출 head는 `d58064ccd30e7e718f94d5e5556d0f19c6afd88c`, 병합 커밋은 [733b47a](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/733b47afca123ed3b161e6d96b492a8afc6e163f)이며 GitHub 확인 시각은 **2026-10-17 16:42:08 KST**입니다. Ready·README 충돌·해당 head의 독립 재검토 조건은 해소됐습니다. 최종 alpha 문서 기준은 `f486758d119080511a4bf27e1b3b328d144fe530`입니다.

문서 [#13](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/13)은 [c38ba6f](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/c38ba6fa8e55c898878853100900320252669695)(16:36:44 KST)에 LOG로, [#14](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/14)는 [96a0574](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/96a05740e072a7193bd1f19c6736ba3bdf013a83)(16:36:48 KST)에 PR로 병합했습니다. [이번 독립 재검토·문서/코드 병합 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_PR7_Ready_재검토_PR13_PR14_병합.md)에서 작성자 보고와 검토자 실제 실행을 구분합니다.

검토자는 #7 head에서 MySQL 회귀 **110통과·2선택제외**, 실제 C#→TCP→raw003 MySQL **37검증 통과**를 확인했습니다. 이 결과는 아래 작성자 MySQL112 보고와 다른 실행이며 합산하지 않습니다. 2번의 [업무 조회 MySQL 실제 HTTP 실행기](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_업무조회_MySQL_실제HTTP_실행기.md)는 별도 변경/검증 기록입니다. 두 변경을 합친 최종 alpha `f486758`에서 업무 HTTP Release 빌드 **경고0·오류0**, 실제 C#→TCP→raw002 MySQL **직원43·관리자64검증 / 실행기301검사(권한회수11포함)**를 통과했습니다. 공지/채팅 WinForms 회귀는 명시적 대역 서비스로 **5시나리오·85검증**을 통과했으며 DB/네트워크 통합 검증과 구분합니다. 검증 중 소스133개와 기존 공유 MySQL 프로세스를 보존했고 소유 DB·서버·임시 디렉터리 정리를 확인했습니다. 실제 로컬 작업공간에는 변경23파일을 반영해 추적179파일 일치·DOCX7개 보존·Release **경고0·오류0**를 확인했습니다. 공유 DB·두 PC·정식 버전/DDL 적용·제품 업무 쓰기/제출/승인·장치/수동 전체 GUI는 미실행입니다.

**코드 병합과 예약003의 정식 SQL 승인·공통 실제/기대 버전 지원·공유 적용은 별도입니다.** schema version1·독립 기본off를 유지합니다. 정식002/003 적용 순서·공유 백업/복구·실제 두 PC·삭제/감사 보존과 팝업/확인 정책은 담당 합의/지원 후 진행합니다. 1번 W1의 공통 쓰기 지원과 3번 W3 회신 대기도 유지하며 제출·검토 기능을 대신 구현하지 않습니다.

아래의 Draft·미병합·충돌·재검토 대기 표현과 과거 테스트 수치는 작성 당시의 원문으로 보존합니다. 현재 병합 상태는 이 절을 기준으로 확인합니다.

## 4번 후속 — 최신 결합본·003 검토 요청, 2026-10-17

코드 #7 head [`d58064c`](https://github.com/zx234x/jang1/commit/d58064ccd30e7e718f94d5e5556d0f19c6afd88c)에 alpha `c56f3c87e446adb30309c46287cd7c4aa3cff273`의 업무 제약/입력 준비를 보존하고 README 충돌을 해소했다. N4 수용 반영·003 SQL/검증 추가 후 **새 head·SQL 재검토 대기**, 원본 미병합이다. [현재 처리 상태](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/4번/2026-10-17_PR7_공지DB_검토상태.md)·[실제 검증/남은 확인 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/4번/2026-10-17_PR7_1번회신반영_003DDL_검증.md)·[LOG 검토 자료](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG/4번)을 따른다. 아래 원문은 당시 기록으로 보존하며 Ready 여부는 GitHub #7의 현재 상태를 확인한다.

> **후속 alpha 기준:** [c56f3c8](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/c56f3c87e446adb30309c46287cd7c4aa3cff273)에 W1-05 상태CHECK 보완을 반영했습니다. 실제DB/주요검증은 MySQL, SQLite는 DEMO 종료 후 삭제 예정입니다. [2번 변경·검증 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_W1-05_상태CHECK_수정_MySQL_검증.md)을 참조합니다. #7과 새alpha의 충돌은 README1개이며 새업무 제약은 보존됐습니다. #12는697a8e3으로 병합, N4 ID/API 수용·R1/R2 해결 확인 상태이고 Draft/README충돌·결합재검증/Ready 확인은 남습니다.

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

아래의 API/ID 회신 대기·작성자 보고·최초 오류 재현·독립 재검토는 각 작성 시점의 근거로 보존합니다. 최신 수신 상태는 이 절과 [2번 수신 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_1번_W1_N4_회신수신_PR12_병합.md)을 따릅니다. 공통 지원 구현·정식 DDL/버전·공유 DB 적용은 문서 수신과 별도 단계입니다. [W1 업무 쓰기 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_업무쓰기_공통규약_회신.md)도 별도로 수신됐으며 3번 상태/제출 합의를 대신하지 않습니다. 이번 수신 정리에서 프로그램·DB 테스트를 새로 실행한 것으로 기록하지 않습니다.

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

4번의 [이전 PR #1 필터 수정](4번/2026-10-16_PR1_공지필터_수정요청.md)은 해결·병합 완료 상태로 보존합니다. 신규 [코드 PR #7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/7)은 검토자 재현(head7478101) 뒤 수정 headc7282d1에서 R1/R2의 해결을 독립 확인했습니다. 코드 PR은 Draft·미병합입니다. PR #12의 N4 회신으로 1번 ID/오류·현재 API 수용은 확인됐으며, 최신 alpha의 루트 README 충돌 해소·결합 재검증·새 head/Ready 확인과 DDL/버전/공유 활성화 후속 조건은 남아 있습니다. [LOG PR #8](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/8)은 `066f25e`, [문서 PR #9](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/9)는 `9edcb08`로 병합 완료입니다. 문서 병합은 코드 승인·alpha 반영·API/DDL 합의 완료를 뜻하지 않습니다. [이번 독립 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_PR7_검토_및_PR8_PR9_문서병합.md)에서 기존 테스트 통과와 추가 결함을 구분합니다.

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

[이번 독립 검토·문서 병합 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_PR7_수정재검토_PR10_PR11_문서병합.md) · [1번 공지 계약 협의](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-17_4번_공지DB_등록_협의요청.md)

최종 게시 준비에서 최신 alpha [6ab86e4](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/6ab86e4efa80aba9d001dd5edbe007319ecaf18d)와도 README.md 1개 충돌을 재확인했습니다. 최신2번 로컬 확인값·실제HTTP 준비 창 안내를 함께 보존해야 합니다. 코드 #7 원본 head는 이번 작업에서 변경하지 않았습니다.
