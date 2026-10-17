# PR 수정 요청

`PR`은 검토에서 발견한 수정 요청과 재검토 조건을 담당 번호별로 관리하는 문서 브랜치입니다. 기준일은 **2026-10-17**입니다. 구현·변경 이력은 [LOG](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/tree/LOG)에 남기며, 이곳의 요청 문서에서 수정 상태를 확인합니다.

## 이전 병합 기록

| PR | 담당·내용 | 상태 |
|---|---|---|
| [#1](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/1) | 4번 공지·로컬 채팅 | 재검토 통과·alpha 병합 · [9b8d00c](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/9b8d00cebe3e8a2a5555e06bcc512a802cc8152b) |
| [#2](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/2) | 1번 공통 서버·DB·인증·통신 | R1~R3 재검토 통과·alpha 병합 · [d27213e](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/d27213eea7d703c7c325b48c0f21b89c7f91cdf6) |
| [#3](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/3) | 1번 협의·검증 문서 | LOG 병합 완료 · [6348997](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/6348997d78976ad5127116ccd606aaa36fc264b5) |
| [#4](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/4) | 4번의 타 담당 파일 수정 전달 문서 | LOG 병합 완료 · [831670e](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/831670e58df09d79b124d1cb0b19975bea6216b2) |
| [#5](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/5) | 1번 수정·검증 기록 | LOG 병합 완료 · [6ca6d8f](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/6ca6d8faafa12ff0774f93724bdb6c9957fc647b) |
| [#6](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/6) | 1번 수정 요청 회신 | PR 병합 완료 · [2e89714](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/2e89714c2f726950a732f2308c87d42964937522) |

2026-10-17에 기존 결함 4건을 재검토하고 코드 PR #1·#2를 병합했습니다. 당시 통합 alpha는 `9b8d00c`입니다. 이번 확인 시 원본 alpha는 [`5164b0c`](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/commit/5164b0c223a05b716d0f78ad722c9024bf7d5333)이며 0.0.2V는 이전 `8f09183`으로 보존합니다. 아래 신규 PR의 검토 대기와 이전 병합 완료를 구분합니다.

## 담당별 수정 요청

| 담당 | 대상 | 재검토 결과 | 문서 |
|---|---|---|---|
| [1번](1번/README.md) | PR #2 | R1~R3 해결 확인(a35c972)·alpha 병합 | [공통 기반 수정 요청](1번/2026-10-16_PR2_공통기반_수정요청.md) |
| [4번](4번/README.md) | [PR #7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/7) · 공지 DB 연결 | **재검토 대기** · Draft · API/DDL 협의 대기 · head7478101 | [공지 DB 검토 상태](4번/2026-10-17_PR7_공지DB_검토상태.md) |

4번의 [이전 PR #1 필터 수정](4번/2026-10-16_PR1_공지필터_수정요청.md)은 해결·병합 완료 상태로 보존합니다. 신규 [코드 PR #7](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/7)은 아직 미병합이며, [LOG PR #8](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/8)도 별도 문서 검토 중입니다. 담당자 테스트 보고를 검토자 확인이나 API/DDL 합의 완료로 표시하지 않습니다.

## 수정 후 재검토

각 담당은 기존 코드 PR에 수정본과 최신 README를 푸시하고, 새 head SHA·변경 파일·각 재현 절차의 수정 후 결과·기존 검증 결과를 알려 주세요. 기존 테스트 통과와 추가 결함 해소를 구분합니다. 검토자는 새 커밋에서 재현이 사라지고 기존 동작이 유지되는지 확인한 뒤 병합 여부를 판단합니다.

새 요청 문서는 `담당번호/YYYY-MM-DD_PR번호_수정내용.md`로 작성합니다. 수정 여부·확인일·근거 커밋·실행한 검증·미실행 항목을 기록하고 푸시할 때 해당 담당 목차와 이 README를 함께 갱신합니다. 1번 a35c972와 4번 837e411은 재검토를 통과해 alpha에 병합됐습니다. 각 담당 문서에 최초 요청·담당자 보고·검토자 확인을 구분해 보존합니다. 업무 API·두 PC 등 미검증 기능까지 완료로 표시하지 않습니다.

3번의 제출·재제출·제출 차수·승인/반려·파일과 5번 기능은 각 담당 범위를 유지합니다. 자동 결과 제출은 제외하고 업무 API·권한 범위·상태·날짜·페이지·자동 검토 요청은 합의 후 연결합니다.
