# 1번 수정 요청

담당: 공통 서버·DB·인증·사용자·통신 · 최초 요청: 2026-10-16 · 최신 검토: 2026-10-17

[전체 요청](../README.md) · [대상 PR #2](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/2)

| 문서 | 현재 상태 |
|---|---|
| [PR #2 공통 기반 수정 요청](2026-10-16_PR2_공통기반_수정요청.md) | R1 P1·R2/R3 P2 재검토 해결 · PR #2 alpha 병합 완료 |

최초 재현 기준은 `2d3cc480c4ab5eb9a3466c9034cfdaf256102df3`, 수정 재검토 기준은 [`a35c972`](https://github.com/jinkyu1452-cmd/Sang1/commit/a35c972f39091fec477024167e5e3e1194999b1e)입니다. 최초 요청·수정 전 재현과 1번 담당자의 회신은 요청 문서에 보존하고, 7절에 2026-10-17 검토자 확인을 추가했습니다. R2의 접속 구분과 R3의 401 처리 순서를 함께 재검증했습니다.

PR #2는 2026-10-17 10:11 KST에 [`d27213e`](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/d27213eea7d703c7c325b48c0f21b89c7f91cdf6)로 alpha에 병합됐습니다. PR #1도 반영된 통합 alpha는 [`9b8d00c`](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/9b8d00cebe3e8a2a5555e06bcc512a802cc8152b)입니다. PR #6의 수정·검증 회신 문서도 10:11 KST에 [`2e89714`](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/2e89714c2f726950a732f2308c87d42964937522)로 PR 브랜치에 병합됐습니다.

검토자가 직접 실행한 C# 인증 검증은 18개 시나리오·88개이며, 감사용 추가 5개 시나리오를 포함한 총 결과는 23개 시나리오·114개입니다. 총괄 검토자가 취합한 서버 결과는 SQLite 24개 통과·MySQL 전용 2개 skip 및 독립 MySQL 26개 통과이고, 공통 HTTP 10개 통과·폼 6개 생성·렌더도 확인했습니다. 병합 후 통합 alpha의 Release 빌드는 경고·오류 0개이며 ApiClient 88개·Collaboration 85개·TaskQueries 47개 검증이 각각 통과했습니다. 중복 실행·포함 관계가 있으므로 결과를 합산하지 않습니다.

실제 업무 API·Own/Managed 권한·타인/관리 범위 밖 상세 접근·업무 DB·두 PC/웹 업무 흐름은 미검증입니다. 업무 조회 계약·공통 소스 적용 범위의 합의는 계속 필요합니다. 상세 실행 주체·전후 비교·미검증 범위는 요청 문서의 7절을 참조합니다.
