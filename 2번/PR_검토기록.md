# PR 검토 및 병합 기록

## 이번 독립 재검토 — 2026-10-19

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
| #21 채팅 코드 | **Draft·미병합**, head `52cc086` | 기존 R21-01~03 수정 확인. 새 R21-04 Init-Chat 환경 누출 P2, README 충돌·공동 계약 확인 필요 |
| #22 채팅 협의 문서 | LOG `0339b29` 병합 | 문서 수신이며 API/DDL 공동 수용·코드 승인 아님 |
| #23 손 검출 코드 | **미병합**, head `1592644` | R23-01 추론 지연 후 과거 영상·활동 상태를 최신으로 표시하는 P2 재현 |
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
