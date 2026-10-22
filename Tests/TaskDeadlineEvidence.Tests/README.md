# 실제 2·3번 상태 경로와 2번 현재 기한 사실 대조

2026-10-22 `test_task_deadline_state_evidence.py`를 소유 MySQL 8.2에서 실행해 **10개 통과, 실패·오류·skip 0, 19.72초**를 확인했습니다. 원본 raw002/004/006/009를 사용했고 물리 DDL·전역 schema1 보존, 시험 DB·MySQL 프로세스·임시 runtime 정리를 확인했습니다. 기존 현재 facts 52건이나 변경 경계 17건에 합산하지 않는 별도 실행입니다.

```powershell
& <서버 Python> -X utf8 -B .\Tests\TaskDeadlineEvidence.Tests\run_mysql_check.py `
  --allow-disposable-mysql `
  --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin' `
  --report-dir <소스밖의새보고서폴더>
```

실행기는 기존 소유권·Windows Job·정리 도구와 원본 DDL 준비 함수를 재사용합니다. 새 MySQL만 시작하고 Flask는 테스트 클라이언트로 실제 등록된 HTTP handler를 호출합니다. 별도 HTTP 서버 프로세스나 제품에 없는 시험 API를 만들지 않습니다. 임의 하위 출력·토큰·비밀번호·키·본문은 보고서에 남기지 않으며 안전한 pytest 요약과 DDL/정리 결과를 `verification.json`에 기록합니다.

계정·개발용 명시 검토자 관계는 합성 자료입니다. 업무 날짜 경계를 고정하기 위해 2·3번의 업무 시각 함수는 시험에서 고정 UTC로 대체합니다. 업무 생성·수락·재배정·수정·제출·반려·재작업·재제출·승인·연장·취소는 실제 2·3번 API와 기존 W1으로 만듭니다. 취소·연장은 이 삭제 가능한 **TESTING 앱에만** 기존 `INTEGRATED_ACK`를 명시합니다. 운영 ACK·공유 DB·기존 서버·`.env`·SQLite·파일 저장소를 사용하지 않습니다. 첨부가 없는 흐름이므로 raw013과 파일 참조/전송 검증은 포함하지 않습니다.

| 검증한 원본 경로 | 실제 관찰한 사실 |
|---|---|
| 생성→수락→제출→반려→재작업→재제출→승인 | `not_started→in_progress→submitted→returned→in_progress→submitted→approved`, row_version 1→7. 기한·담당자·assignment_revision=1은 유지. 고정 시각에서 최종 accepted_at 값, 실제 제출 1·2차와 3번 ResultEvent 4건/제출 감사의 버전 3·4·6·7을 대조. 고정 시각이므로 수락시각 재기록 여부를 구별하는 검사는 아님. |
| 진행전 재배정 A→B→A | 현재 담당자는 다시 A가 되지만 배정 세대는 1→2→3, 업무 버전도 1→2→3. 각 과거 배정 행 보존, 이전 담당자의 own 전체 목록 제외/단건403, 재배정 감사의 전후 담당자와 새 세대 확인. 기한은 그대로. |
| 일반수정→수락→연장 신청→승인→제출 | 일반수정은 버전2에서 A→B, 신청 버전4는 B 유지, 승인 버전5에서 B→C, 실제 제출 버전6은 C 유지. 원래 `task.updated`와 승인 감사의 전후 기한·버전 순서 대조. 신청 대기는 실제 3번 제출 준비를409로 차단하며, 제출 이후 일반수정 거절은 저장 효과가 없음. |
| 진행중/반려완료 × 연장 대기 유무에서 취소 | 실제 취소 후 기존 업무5상태·기한·배정은 보존, row_version만 +1, `is_cancelled=true`. pending이 있으면 `cancelled`로 종결. 과거 제출·ResultEvent 보존, 실제3번 신규 제출/준비는 `TASK_CANCELLED`로 차단. 전체 facts에도 취소 사실을 포함. |
| 제출완료/승인완료의 취소 거절 | 실제3번 상태를 facts가 그대로 반환. 기존 확정 상태 규칙에 따라 취소409이며 업무·감사·receipt·결과 이력 변경 없음. |
| 같은 배정에서 제출→반려→재작업 및 감사 누락 | 시작·종료의 담당자/배정/기한/진행중 상태가 같아도 중간 제출·반려가 실제 존재. 현재 버전은2→5. 기존2번 continuity reader는3번 구간을 임의 완전 이력으로 읽지 않고 `None`. 시험에서 제출 감사2건만 명시 제거해도 현재 facts와 ResultEvent가 같으며, 현재 사실로 누락된 과거를 복원하지 않음. |

조회 전후 업무·감사·receipt·결과 테이블도 대조해 새 읽기 접점이 기한 사건이나 과거 이력을 만드는 것으로 해석하지 않도록 합니다. 감사 누락은 마지막 시험의 삭제 가능한 DB에만 적용한 보존 손실 조건이며 제품 정리/보정 기능이 아닙니다.

## 4번에 제공할 근거와 아직 확정하지 않은 것

기존 2번 감사는 일반수정의 `changed_fields/before_due_date/due_date`, 변경 전후 row_version·assignment_revision, 취소·연장의 `before/after`를 제공합니다. 실제 3번 제출 감사는 제출 자원에 `task_id/round_no/row_version`를, `ResultEvent`는 제출·배정·판단 종류와 원래 operation_id를 보존합니다. 이들은 **각 원본 행동의 증거**이며 공통 기한 사건ID나 공통 완전 이력 API로 전용하지 않습니다. 3번의 공개 history 응답과 내부 저장 필드도 구분합니다.

최초 생성 감사에는 원래 `due_date`가 없습니다. 이후 현재 기한/updated_at 또는 가장 오래 남은 변경을 최초 사건으로 간주할 수 없습니다. 원본 감사 누락·보존 범위·초기 자료 baseline 전환, 같은 배정에서 제출/반려/복귀한 이전 확인 유지 여부는 기존 협의사항입니다. 이 검사가 정책을 확정하지 않습니다.

1번의 실제 사건 저장 함수/API/DDL·현재 사건 연결·확인 저장은 이 검사에 존재하지 않습니다. 사건 identity·배정 세대·최초/누락 이력의 처리 계약이 제공된 뒤, 별도 합의된 경계에서 검증해야 합니다. 운영 배포·실계정·두PC, 실제 알림 source/확인 writer·확인 보존 기간과 사건 기록 원자성 검증은 이번 통과에 포함되지 않습니다.
