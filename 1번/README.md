## 최신 검토 — PR40·41 코드 / PR42 회신 병합, 2026-10-19

[PR40](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/40) 1번 공통 지원은 alpha `7073817`, [PR41](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/41) 5번 독립 감지 서비스/공유 view는 alpha `763f11e`에 검토 후 병합했습니다. 새 차단 수정 요청은 없습니다. 직접 검증은40번 Python107·C#31,41번 C#294/12시나리오·Python30, 결합 Release 경고0/오류0이며 실제 병합 tree와 일치합니다. [PR42](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/42) 4번24개 통합 회신은 기존2문서/원문·링크/앵커를 검토해 LOG `2e6a439`에 병합했습니다. 문서 검토 때문에 제품/DB 시험을 추가 실행한 것은 아닙니다.

40번은 독립 읽기 점검/바이트 초안/화면 요청 수명 도구이고41번은 소유자가 주입할 서비스 준비입니다. 실제 본인 확인 서버·근태 저장·출근~퇴근 MainForm 연결·실장비/공유DB/두PC는 아직 구현/검증 대상입니다. 회신 수신을 제안 정책의 확정으로 확대하지 않습니다. 아래의 이전 대기/미연결은 해당 시점 기록입니다.

[독립 검토 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#pr40-pr41-pr42-review)에 근거를 남겼습니다. 추가 차단 수정 요청 파일은 만들지 않습니다.

# 1번 수정 요청

## 최신 검토자 확인 — PR #17·#18 병합

[공통 접속 범위·기능 계약 검토 결과](2026-10-17_PR17_접속범위_기능계약_검토결과.md): 코드 #17은 alpha6f5f113, 문서 #18은 LOGdd75964로 병합했습니다. 검토 범위에 신규 수정 요청은 없으며 검토자 소유 MySQL168·접속125·빌드 경고/오류0을 확인했습니다. [2번의 후속 연결과 W3 남은 범위](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md#r025)를 따릅니다. 아래 과거 대기 상태는 작성 당시 기록으로 보존합니다.

## 최신 검토자 확인 — PR #15·#16 병합, 2026-10-17

[W1 공통 지원 검토 결과](2026-10-17_PR15_W1_공통지원_검토결과.md): 코드 #15는 alpha `5fd99a8`, 문서 #16은 LOG `54c6cd3`로 병합했습니다. 이번 범위에서 신규 수정 요청은 없습니다. 공통 지원 수신 완료와 제품 업무 쓰기·W3 미정 정책·공유 DDL/버전/두PC 미완료를 구분합니다. [실제 검토·검증 상세](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/PR_검토기록.md#r019)를 따르며 아래 이전 대기/미구현 표현은 당시 기록입니다.

담당: 공통 서버·DB·인증·사용자·통신 · 최초 요청: 2026-10-16 · 최신 검토: 2026-10-17

[전체 요청](../README.md) · [대상 PR #2](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/2)

| 문서 | 현재 상태 |
|---|---|
| [PR #2 공통 기반 수정 요청](2026-10-16_PR2_공통기반_수정요청.md) | R1 P1·R2/R3 P2 재검토 해결 · PR #2 alpha 병합 완료 |

최초 재현 기준은 `2d3cc480c4ab5eb9a3466c9034cfdaf256102df3`, 수정 재검토 기준은 [`a35c972`](https://github.com/jinkyu1452-cmd/Sang1/commit/a35c972f39091fec477024167e5e3e1194999b1e)입니다. 최초 요청·수정 전 재현과 1번 담당자의 회신은 요청 문서에 보존하고, 7절에 2026-10-17 검토자 확인을 추가했습니다. R2의 접속 구분과 R3의 401 처리 순서를 함께 재검증했습니다.

PR #2는 2026-10-17 10:11 KST에 [`d27213e`](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/d27213eea7d703c7c325b48c0f21b89c7f91cdf6)로 alpha에 병합됐습니다. PR #1도 반영된 통합 alpha는 [`9b8d00c`](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/9b8d00cebe3e8a2a5555e06bcc512a802cc8152b)입니다. PR #6의 수정·검증 회신 문서도 10:11 KST에 [`2e89714`](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/2e89714c2f726950a732f2308c87d42964937522)로 PR 브랜치에 병합됐습니다.

검토자가 직접 실행한 C# 인증 검증은 18개 시나리오·88개이며, 감사용 추가 5개 시나리오를 포함한 총 결과는 23개 시나리오·114개입니다. 총괄 검토자가 취합한 서버 결과는 SQLite 24개 통과·MySQL 전용 2개 skip 및 독립 MySQL 26개 통과이고, 공통 HTTP 10개 통과·폼 6개 생성·렌더도 확인했습니다. 병합 후 통합 alpha의 Release 빌드는 경고·오류 0개이며 ApiClient 88개·Collaboration 85개·TaskQueries 47개 검증이 각각 통과했습니다. 중복 실행·포함 관계가 있으므로 결과를 합산하지 않습니다.

실제 업무 API·Own/Managed 권한·타인/관리 범위 밖 상세 접근·업무 DB·두 PC/웹 업무 흐름은 미검증입니다. 업무 조회 계약·공통 소스 적용 범위의 합의는 계속 필요합니다. 상세 실행 주체·전후 비교·미검증 범위는 요청 문서의 7절을 참조합니다.
