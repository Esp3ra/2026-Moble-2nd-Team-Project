# 1번 — 공통 서버·DB·인증·사용자·통신·통합

작성일: 2026-10-16 · 상태: PR #3 문서 LOG 병합 완료, PR #2 코드 수정 후 재검토

팀장 LOG의 기존 루트 `2번/` 구조에 맞춰 루트 `1번/` 아래 날짜·주제별 Markdown을 작성합니다. 기존 2번 기록과 `LOG/2026-10-16/` 기록은 보존합니다.

| 구분 | 실제 공유 위치 |
|---|---|
| 공통 코드 PR | [PR #2 — alpha 대상](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/2) |
| 공통 코드 기준 | [`2d3cc48`](https://github.com/jinkyu1452-cmd/Sang1/tree/2d3cc480c4ab5eb9a3466c9034cfdaf256102df3) · 포크 `team1/common-foundation` |
| 코드에 포함된 규약 | [INTEGRATION.md](https://github.com/jinkyu1452-cmd/Sang1/blob/2d3cc480c4ab5eb9a3466c9034cfdaf256102df3/docs/INTEGRATION.md) |
| 실행·MySQL 설정 | [SETUP.md](https://github.com/jinkyu1452-cmd/Sang1/blob/2d3cc480c4ab5eb9a3466c9034cfdaf256102df3/docs/SETUP.md) |
| 최신 팀 코드 기준 | [alpha 8f09183](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/8f09183ef992606ec4c2b7e3da79ef07983ba019) |
| 문서 PR | [PR #3](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/3) LOG 병합 완료 · [6348997](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/6348997d78976ad5127116ccd606aaa36fc264b5) |
| 코드 수정 요청 | [PR 브랜치의 1번 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/1번/2026-10-16_PR2_공통기반_수정요청.md) · P1 1건·P2 2건, 수정본 미확인 |

| 날짜·주제 | 기록 |
|---|---|
| [공통 규약 확인 요청 회신](2026-10-16_공통규약_확인요청_회신.md) | 2번 A1~A4·B1~B5: 구현·등록 API와 미합의 구분 |
| [조회 인터페이스·DTO 전달 회신](2026-10-16_업무조회_인터페이스_DTO_회신.md) | 최신 실제 코드 확인과 2번의 6개 질문 답변·후속 담당 |
| [공동 합의 필요 항목](2026-10-16_공동합의_필요항목.md) | 상태·결정할 내용·담당·결정 시점 |
| [공통 구현·검증 기록](2026-10-16_공통서버_구현_검증기록.md) | PR #2 검토 코드 기준의 SQLite/MySQL/HTTP/빌드/기존 조회 검증 기록 |

공통 서버·인증·사용자 관리 구현과 검증은 위 문서에 기록된 코드 기준입니다. PR #2 검토에서 관리자 동시 강등·이전 접속 응답·비정상 본문 401 문제를 재현했으므로 수정 후 재검토하며 코드 PR은 아직 미병합입니다. 업무 목록·상세 API·실제 Own/Managed 범위·업무 상태 변경·제출·승인·파일·두 PC/웹 업무 통합은 별도입니다. 업무 조회 인터페이스·DTO·대역·기존 테스트와 전체 Features 60개 파일은 원격 Git blob 해시를 비교해 보존했습니다.

‘구현 확인’은 위 코드에서 확인한 동작을 뜻합니다. ‘합의 필요’는 제안이며 팀 결정이 아닙니다. PR #3의 문서 병합은 PR #2의 코드 병합이나 결함 수정 완료를 뜻하지 않습니다. 비밀번호·접속 증표·실제 .env·DB 데이터는 이 기록에 싣지 않습니다.
