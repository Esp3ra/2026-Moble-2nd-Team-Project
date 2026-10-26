# 업무 관리자 = 결과 검토자: 서버 적용 안내

작성 5번 · 전달 1번 서버/2번 업무/3번 결과물/팀장. 2026-10-26 사용자가 **직원이 제출한 결과를 현재 지정된 업무 관리자가 검토·승인·반려하고, 별도 제출 검토자는 만들지 않도록** 지시했다. 현재 업무14·15도 새로 만들지 않고 같은 기준을 적용한다. 이 문서는 실제 서버 배포/DB 변경/상대 확인 완료 보고가 아니다.

## 달라지는 동작

- 결과 제출 시 현재 업무의 `task_managers`에 속한 활성 `admin`을 확인한다. 부서/상위 검토자 등록은 필요 없다.
- 여러 업무 관리자는 모두 결과 검토 목록과 받은 검토 요청을 조회하고, 요청 확인 및 결과 승인·반려를 처리할 수 있다. 먼저 확정된 판단 한 건만 저장한다.
- 제출자 본인, 관리자 관계에서 빠진 사람, 비활성/직원으로 변경된 사람은 판단할 수 없다. 기존 창·요청키도 현재 권한을 다시 확인한다.
- 마감 초과, 취소, 연장 대기, 현재 배정/버전, 필수 반려 사유와 멱등 검사는 유지한다.
- 기존 결과/요청의 당시 대표 검토자 스냅샷과 감사는 보존한다. 예전 부서/상위 검토자라는 이유만으로 권한을 주지 않는다. 공개 응답의 단일 `reviewer_id`는 모든 관리자 목록이 아니라 대표 표시이며, 판단 감사에는 실제 판단자가 남는다.

## 1번 서버 적용 순서

1. 실제 실행 중인 서버 폴더·커밋·실행 명령을 확인한다. 본 변경은 PR99·100을 포함한 alpha를 기준으로 하므로 예전 서버에 파일 하나만 복사하지 않는다. 로컬 변경과 기존 비공개 `.env`·지문 설정·실행 인자·파일 저장소를 보존하고 기존 배포/복구 절차로 백업한다.
2. 검토·병합된 본 변경을 실제 서버 소스에 반영한다. 서버 제품 변경은 `remotework/features/results.py`, `remotework/features/tasks/review_requests.py`, `remotework/templates/tasks/review_request.html`이다. 서버 시작 명령은 기존 것을 유지한다.
3. **본 변경의 신규 DDL/계정/업무/검토자 데이터 등록은 없다.** PR99 검토 요청함의 기존019가 미적용된 서버라면 `migrations/019_task_review_requests.md`에 따라 별도 백업/검토 후 적용해야 한다. 일반 시작·init-db가 대신 설치하지 않으며, 기존 업무/부서/상위 검토자 자료를 삭제하지 않는다. schema1을 유지한다.
4. 기존 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT`, `REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT`, `REMOTEWORK_ENABLE_RESULTS_DRAFT`와 현재 결과물/파일 저장소 설정을 유지한다. 기존 `REMOTEWORK_RESULTS_CONTEXT=local-development` 어댑터가 이제 업무 관리자를 조회한다. `results set-department-reviewer`/`set-upper-reviewer`를 새로 실행하지 않는다. PR98 취소/연장 통합 옵션도 기존 값을 유지한다.
5. 기존 지문 전용 인자/비공개 설정을 유지한 **실제 서버 프로세스**를 재시작한다. 이 PR은 지문·키·라파 서비스를 바꾸지 않는다. 서버 재시작에 따른 지문 회차 대조가 필요한 환경은 기존 1번↔5번 재연결 절차로 확인하며 임의로 키/회차를 초기화하지 않는다.
6. WinForms 변경 `Features/Tasks/ReviewRequestForm.cs`를 포함한 최신 소스로 다시 빌드해 열어 둔 이전 실행 파일을 교체한다. 클라이언트만 갱신해도 원격 서버의 제출 권한이 바뀌지는 않는다.

## 기존 업무14·15 및 검증용 업무 확인

- 업무14·15의 현재 지정 관리자에 사용할 계정이 포함돼 있고 활성 관리자 상태인지 기존 관리 화면/허용된 조회로 확인한다. 담당자 본인만 관리자로 있으면 다른 관리자가 필요하다. 계정·업무를 새로 생성하거나 DB를 직접 수정할 필요 없다.
- 해당 관리자로 `GET /api/v1/results/review-tasks`를 조회해 허용된 업무가 보이는지 확인한다. 직원은 진행중 본인 업무의 `GET /api/v1/results/tasks/{id}/context`에서 정상 제출 문맥을 확인한다. 이미 제출완료/반려/취소/마감 경과라면 해당 상태의 기존 제한을 구분한다.
- 검증용 업무로 직원 결과 제출 → 별도 검토 요청 → 지정 업무 관리자의 받은 요청 조회/확인 → 반려 사유 저장 → 직원 재작업/재제출 → 관리자 승인 순서를 확인한다. 관리자가 여럿이면 다른 지정 관리자도 검토할 수 있는지, 관계에서 빠진 관리자/별도 부서 검토자만인 계정은 거절되는지 확인한다.
- 실제14·15의 승인·반려는 사용자가 원하는 판단으로 수행한다. 자동 승인/반려·업무 상태 직접 수정·테스트를 위한 실제 자료 삭제는 하지 않는다.
- 서버 실제 SHA·019 준비 여부·직원 제출/관리자 검토 결과·실패 시 요청 번호를 한 번에 회신한다. 비밀번호·토큰·키·업무 본문은 공개 댓글에 넣지 않는다.

## 직접 검증 및 남은 항목

소유한 별도 MySQL에서 실제 C#·HTTP·WinForms147, 독립 SQL 효과32 통과. 서버/API/웹 전체82 통과 후 새로 추가한 과거 제출 스냅샷 검사1개를 선택 실행해 통과했다. 실제 WinForms·HTTP 대역127과 Release 경고0/오류0, 구문/diff 검사를 확인했다. 사용자/공유 DB·서버232·실업무14/15·두PC는 미실행이다. 이전 대표 이름/관리자 행 수 기대값을 새 정책에 맞춰 수정한 최초 검사 실패와 최종 통과는 구분한다.

재현은 저장소 루트에서 서버 의존성이 준비된 Python과 .NET8 이상 SDK, MySQL8.0.16 이상을 사용한다. 아래 MySQL 실행기는 새 소유 테스트 DB만 만들고 종료 시 정리하며 사용자 `.env`/공유 DB를 사용하지 않는다.

```powershell
dotnet run --project Tests/TaskReviewRequests.Tests -c Release
dotnet build Tests/TaskResults.Integration.Tests -c Release
python -X utf8 -B Tests/TaskResults.Integration.Tests/run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir '<MySQL 설치 폴더>/bin'
python -X utf8 -B Tests/TaskReviewRequests.Tests/run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir '<MySQL 설치 폴더>/bin' --report-dir '<소스 밖 새 검사 폴더>'
```

이미 실행한82+1 검사를 위 전체 실행의83개 재실행 결과로 바꾸어 보고하지 않는다. 공유 서버 배포/실제14·15 처리 완료와 코드 PR 제출/병합은 각각 구분해서 보고한다.
