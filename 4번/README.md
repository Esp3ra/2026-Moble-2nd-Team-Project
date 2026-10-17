# 4번 수정 요청 및 처리 기록

> **후속 alpha 기준:** [c56f3c8](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/c56f3c87e446adb30309c46287cd7c4aa3cff273)에 W1-05 상태CHECK 보완을 반영했습니다. 실제DB/주요검증은 MySQL, SQLite는 DEMO 종료 후 삭제 예정입니다. [2번 변경·검증 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_W1-05_상태CHECK_수정_MySQL_검증.md)을 참조합니다. #7과 새alpha의 충돌은 README1개이며 새업무 제약은 보존됐습니다. #12는697a8e3으로 병합, N4 ID/API 수용·R1/R2 해결 확인 상태이고 Draft/README충돌·결합재검증/Ready 확인은 남습니다.

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


## 최신 검토자 확인 — PR #7

수정 head `c7282d1e9e332daf3996aeb00f9a1b20c6ad2e61`의 **R1·R2 해결을 독립 확인**했습니다. Draft·미병합을 유지하며 1번 ID/오류·API/DDL 회신, 최신 alpha의 루트 README 충돌 해소·새 head 재확인이 필요합니다. [검토자 재검토 결과](2026-10-17_PR7_공지DB_수정요청.md#검토자-독립-재검토--2026-10-17)와 [최신 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_PR7_수정재검토_PR10_PR11_문서병합.md)을 먼저 확인합니다.

문서 PR #10은 LOG `530f171`, #11은 PR `120d855`로 병합했습니다. 아래의 작성자 보고·이전 검토 시점은 당시 기록으로 보존합니다.


## 이전 작성자 수정 회신 — PR #7

- 코드 head `c7282d1e9e332daf3996aeb00f9a1b20c6ad2e61`, 기준 원본 alpha8a652ad.
- **R1/R2 작성자 수정·격리 검증 완료·재검토 대기**, Draft·미병합. ID/오류 최종 계약·API/DDL은 1번 회신 대기.
- [구현·미합의·미실행 최신표](2026-10-17_PR7_공지DB_검토상태.md) · [검토자 요청과 4번 수정 회신](2026-10-17_PR7_공지DB_수정요청.md).
- 문서 PR #8/#9는 병합됐고 이번 수정의 코드/문서 검토·공유 적용과 구분한다.

## 이전 검토 시점과 PR #1 기록


담당: 공지·채팅 · 최초 기준일: 2026-10-16 · 최신화: 2026-10-17

[전체 요청](../README.md)

## 이전 최초 검토 — PR #7 공지 DB 연결

[코드 PR #7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/7) · [검토 상태](2026-10-17_PR7_공지DB_검토상태.md) · [별도 LOG PR #8](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/8)

- 상태: **수정 후 재검토**. [검토자 수정 요청](2026-10-17_PR7_공지DB_수정요청.md)의 P2 R1(동시 삭제 감사 중복)·R2(범위 초과 ID의 SQLite 500)를 직접 재현했습니다. 코드 PR은 Draft·미병합이며 공지 API/DDL·migration·정식 활성화의 1번 합의도 필요합니다.
- 기준: 원본 alpha `5164b0c223a05b716d0f78ad722c9024bf7d5333`, 포크 alpha `74781018af21d3fa9f30bc6c06515dd260335730`.
- 범위: 공지 DB 등록·목록/상세 조회·관리자 삭제. 팝업·확인 기록·실제 파일 전송·중지된 정렬 추가 조정은 제외합니다.
- 작성자의 앞선 보고와 검토자의 이번 독립 실행을 구분합니다. 서버 SQLite66통과/14생략·C#5종·공지 실제 HTTP/SQLite37검증·플래그4조합을 확인했고 별도 MySQL/SQLite 표적 재현에서 R1·R2를 확인했습니다. 상세·미실행 범위는 새 수정 요청을 따릅니다.
- 문서 PR #8(LOG066f25e)·#9(PR9edcb08)는 병합 완료입니다. 문서 병합을 코드 PR #7 승인으로 표시하지 않습니다.
- 아래 PR #1 본문은 당시 로컬 필터 수정·통합 검증 기록입니다. 당시 미연결 설명을 신규 PR #7의 구현 상태로 해석하지 않습니다.

## 이전 PR #1 — 확인 완료·alpha 병합

[이전 대상 PR #1](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/1)

| 문서 | 현재 상태 |
|---|---|
| [PR #1 공지 필터 수정 요청](2026-10-16_PR1_공지필터_수정요청.md) | P2 디자인팀 필터 누락 해결 · 검토자 재검토 완료 · alpha 병합 완료 |

최초 검토 head는 `249768732caf3357741d3aacbcfb4abdcb34bd80`이며, 당시 재현·수정 요청은 문서에 보존합니다. 새 head `837e411733b2e23bbb2a791b72334b5f242c3ecf`에서 작성 대상·목록 선택지·조회 조건이 일치하고 기존 공지·로컬 채팅 동작과 README 설명도 유지됨을 확인했습니다.

PR #1은 2026-10-17 10:12 KST에 alpha로 병합됐으며 병합 커밋은 [`9b8d00c`](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/9b8d00cebe3e8a2a5555e06bcc512a802cc8152b)입니다. 새 공지·채팅 회귀 5개 시나리오·85개 검증과 검토자 독립 추가 검증 32개가 통과했고, 같은 새 회귀를 이전 head에 적용하면 기존 대상 누락에서 실패함을 확인했습니다.

alpha 통합본에서도 Release 빌드 경고 0·오류 0, 공지·채팅 5개 시나리오·85개 검증과 업무 조회 6개 시나리오·47개 검증을 통과했습니다. 같은 회귀를 반복 실행한 결과는 합산하지 않습니다.

인증된 실제 사용자 매핑과 공지·채팅 서버·DB·권한·두 PC·파일 전송·직원 자동 팝업·확인 기록은 계속 미연결·미검증입니다. 이번 완료는 공지 필터 수정과 로컬 메모리 구현의 재검토·alpha 반영 범위입니다.

작성자 보고 당시 확장 협의는 [LOG PR #10](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/10)으로 전달돼 미병합·회신 대기였습니다. 후속 문서 병합 상태와 남은 회신은 위 최신 검토자 확인을 따릅니다. 규약 제안 게시를 1번 수용이나 코드 승인으로 표시하지 않습니다.

최종 게시 준비에서 최신 alpha [6ab86e4](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/6ab86e4efa80aba9d001dd5edbe007319ecaf18d)와도 README.md 1개 충돌을 재확인했습니다. 최신2번 로컬 확인값·실제HTTP 준비 창 안내를 함께 보존해야 합니다. 코드 #7 원본 head는 이번 작업에서 변경하지 않았습니다.
