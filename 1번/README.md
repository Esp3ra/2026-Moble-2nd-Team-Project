# 1번 — 공통 서버·DB·인증·사용자·통신·통합

작성일: 2026-10-16 · 상태: PR #3·#4 문서 LOG 병합 완료, PR #2 R1~R3 수정본 검증 완료·팀장 재검토 대기

팀장 LOG의 기존 루트 `2번/` 구조에 맞춰 루트 `1번/` 아래 날짜·주제별 Markdown을 작성합니다. 기존 2번 기록과 `LOG/2026-10-16/` 기록은 보존합니다.

| 구분 | 실제 공유 위치 |
|---|---|
| 공통 코드 PR | [PR #2 — alpha 대상](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/2) |
| 공통 코드 기준 | [`a35c972`](https://github.com/jinkyu1452-cmd/Sang1/tree/a35c972f39091fec477024167e5e3e1194999b1e) · 포크 `team1/common-foundation` |
| 코드에 포함된 규약 | [INTEGRATION.md](https://github.com/jinkyu1452-cmd/Sang1/blob/a35c972f39091fec477024167e5e3e1194999b1e/docs/INTEGRATION.md) |
| 실행·MySQL 설정 | [SETUP.md](https://github.com/jinkyu1452-cmd/Sang1/blob/a35c972f39091fec477024167e5e3e1194999b1e/docs/SETUP.md) |
| 최신 팀 코드 기준 | [alpha 8f09183](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/8f09183ef992606ec4c2b7e3da79ef07983ba019) |
| 문서 PR | [PR #3](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/3) LOG 병합 완료 · [6348997](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/6348997d78976ad5127116ccd606aaa36fc264b5) |
| 타 담당 전달 PR | [PR #4](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/4) LOG 병합 완료 · [831670e](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/831670e58df09d79b124d1cb0b19975bea6216b2) |
| 코드 수정 요청 | [PR 브랜치의 1번 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/PR/1번/2026-10-16_PR2_공통기반_수정요청.md) · R1~R3 수정본·회귀 검증 제출, 재검토 대기 |

| 날짜·주제 | 기록 |
|---|---|
| [PR #2 인증 결함 수정·검증](2026-10-16_PR2_인증결함_수정검증.md) | R1~R3 변경·원본 실패 재현·수정 후 26개 MySQL/88개 C# 검증·재검토 요청 |
| [공통 규약 확인 요청 회신](2026-10-16_공통규약_확인요청_회신.md) | 2번 A1~A4·B1~B5: 구현·등록 API와 미합의 구분 |
| [조회 인터페이스·DTO 전달 회신](2026-10-16_업무조회_인터페이스_DTO_회신.md) | 최신 실제 코드 확인과 2번의 6개 질문 답변·후속 담당 |
| [공동 합의 필요 항목](2026-10-16_공동합의_필요항목.md) | 상태·결정할 내용·담당·결정 시점 |
| [공통 구현·검증 기록](2026-10-16_공통서버_구현_검증기록.md) | 최초 2d3cc48 기준의 SQLite/MySQL/HTTP/빌드/기존 조회 기록. 최신 수정 검증은 위 별도 기록 |
| [4번 공지·채팅 타 담당 파일 수정 전달](2026-10-16_4번_공지채팅_타담당파일_수정전달.md) | PR #1의 공통 MainForm·결과물 문구 변경, 검증과 후속 확인·앞으로의 전달 기록 기준 |

공통 서버·인증·사용자 관리 구현과 검증은 위 문서에 기록된 코드 기준입니다. PR #2의 수정 전 소스에서 관리자 동시 강등·이전 접속 응답·비정상 본문 401을 재현하고 a35c972에서 수정·회귀 검증했습니다. MySQL 26개, SQLite 24개/2개 skip, C# 인증 18개 시나리오·88개 검증, 실제 HTTP 10개·기존 조회 47개가 통과했습니다. 팀장 재검토·코드 병합은 대기입니다. 업무 목록·상세 API·실제 Own/Managed 범위·업무 상태 변경·제출·승인·파일·두 PC/웹 업무 통합은 별도입니다. 업무 조회 인터페이스·DTO·대역·기존 테스트와 전체 Features 60개 파일은 원격 Git blob 해시를 비교해 보존했습니다.

‘구현 확인’은 위 코드에서 확인한 동작을 뜻합니다. ‘합의 필요’는 제안이며 팀 결정이 아닙니다. PR #3·#4의 문서 병합은 PR #1·#2의 코드 병합이나 결함 수정 완료를 뜻하지 않습니다. 비밀번호·접속 증표·실제 .env·DB 데이터는 이 기록에 싣지 않습니다.

## 타 담당 영역 수정 전달 기록

4번이 다른 팀원 담당 파일을 수정한 경우에도 사용자 요청에 따라 이 폴더에 날짜·주제별 전달 문서를 작성합니다. [최초 전달과 작성 기준](2026-10-16_4번_공지채팅_타담당파일_수정전달.md)에는 실제 수정 범위·기준 코드·이전 검증·미실행 항목을 구분했습니다. 해당 문서는 4번 작성의 전달 기록이며 1번의 구현·수정 완료나 승인으로 간주하지 않습니다.
