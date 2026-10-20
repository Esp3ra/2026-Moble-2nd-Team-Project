# 3번 결과물·검토·이력 개발 구현

PR #46은 직원/관리자 결과물 폼 4개와 실제 서버 제출·판단·파일·이력 기능을 alpha에 제안합니다. 현재 결과물 웹은 준비 중 화면으로 남으며 이번 코드에 웹 완료를 포함하지 않습니다.

## 화면과 저장

| 화면 | 사용자·메뉴 | 이번 코드 |
|---|---|---|
| ReviewsControl | 직원 04 결과물 / 관리자 04 결과물 검토·내 결과물 | 기존 2번 업무 조회, 실제 선택 상세·제출 회차·첨부·이력, 저장 성공 후 재조회 |
| SubmissionForm | 직원 또는 관리자 본인 업무의 결과 제출 | 설명·첨부, 실제 저장 확인, 최초 1차/성공 재제출만 +1차, 과거 보존 |
| ReviewDecisionForm | 관리자 04의 검토 대상 | 빈 의견/사유, 반려 사유 필수, 현재 최신 제출·현재 권한 재검사 후 승인/반려 |
| HistoryControl | 관리자 05 업무 이력 | 현재 허용 업무의 생성·수락/재작업·제출·판단 감사 조회와 필터/상세 |

기존 Designer/resx·생성자·공통 ApiClient·MainForm·2번 업무 서비스는 유지합니다. ResultsApi는 ReviewsControl.cs 하단의 일반 통신 클래스이며 별도 ResultsApi.cs나 새 폼은 추가하지 않습니다.

## 개발 활성화·초기화

기존 공통 로그인/schema1·002 업무·004 쓰기 저장소 준비가 선행되어야 합니다. 아래는 별도 개발 DB에서 운영자가 명시 실행할 안내이며 서버 시작/요청이 DDL이나 계정·업무를 자동 생성하지 않습니다.

```powershell
$env:REMOTEWORK_ENABLE_TASK_QUERY_DRAFT='1'
$env:REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT='1'
$env:REMOTEWORK_ENABLE_RESULTS_DRAFT='1'
$env:REMOTEWORK_RESULTS_CONTEXT='local-development'
$env:REMOTEWORK_RESULTS_STORAGE_ROOT='C:/RemoteWorkDevelopment/results-files'
python -m flask --app remotework:create_app results init-results
python -m flask --app remotework:create_app results set-department-reviewer --reviewer-login <실제_부서_최고직급_관리자>
python -m flask --app remotework:create_app results set-upper-reviewer --submitter-login <관리자_제출자> --reviewer-login <상위_검토자>
python run.py
```

환경 변수의 실제 공통 설정 이름과 DB 준비는 [기존 W1 안내](WRITE_SUPPORT.md)를 따릅니다. DB URL·비밀번호·SECRET_KEY는 각 환경의 비공개 설정이며 이 PR에 넣지 않습니다. 위 부서/상위 검토자 계정은 운영자가 실제 자료로 선택합니다. 이름·admin 역할·task_managers만으로 검토자를 추정하지 않습니다.

`006_results.mysql.sql`은 1번이 예약한 번호의 개발 검토안입니다. 결과물 8개 테이블만 다루고 공통 부모 ALTER·전역 schema version 변경·seed는 없습니다. 초기화 명령은 기존 테이블의 열 이름까지만 검사하므로 기존 DB의 FK/인덱스·타입·보존까지 검증했다는 의미가 아닙니다. 공유 DB 적용 전 1번의 물리 스키마/백업·복구 검토가 필요합니다.

## 유지하는 계약

- 진행중에서 제출. 반려완료에서는 2번의 재작업 시작을 거쳐 진행중으로 돌아간 뒤 재제출합니다. 최초 수락 시각·배정 차수와 이전 제출물/첨부는 보존합니다.
- 제출 성공만 1차부터 증가하고 업무상 최대 회차는 없습니다. signed BIGINT 소진만 기술 오류로 구분합니다. 회차/업무/제출 ID/row_version은 JSON 문자열, assignment_revision은 기존 JSON 정수입니다.
- 제출/판단·업무 상태/버전·감사·결과 이벤트·공통 receipt를 같은 트랜잭션에 저장합니다. 같은 접속의 원본 키/본문을 메모리 보존하고 응답 유실 결과를 조회합니다. 자동 새 키/자동 쓰기 재시도·앱 재시작 복원은 없습니다.
- 현재 담당자 또는 명시한 현재 검토자만 결과/과거 회차/첨부를 조회합니다. 최신 미판단 제출만 검토하고 자기 승인은 금지합니다. 관계 회수는 조회·저장·재응답에도 재검사합니다.
- 마감 후 직접 제출 및 연장 승인 대기는 차단합니다. local_facts/개발용 gate는 최종 조직·2번 연장 모델을 대신 확정한 계약이 아닙니다. 실제 승인/마감일 갱신 API는 2번 후속 연결 대상입니다.
- 첨부 형식은 모두 허용하며 현재 개발 크기 한도는 25 MiB/개, 제출당 10개입니다. 임시 업로드와 크기·SHA256 검사, 실제 바이트 다운로드를 제공하고 공통 JSON 크기를 유지하는 16 KiB 조각을 사용합니다. 다른 담당 전체 첨부에 쓸 공용 자원 API로 확장한 것은 아닙니다.

## 실제 API

`/api/v1/results`의 context/submissions/decisions/attachments/history-tasks/activity를 제공합니다. 업무/제출 대상은 ID로 전달하며 설명/사유/첨부 및 expected_row_version·expected_assignment_revision을 저장 직전 검사합니다. 파일 start/chunks/complete와 제출/판단의 공통 Idempotency-Key·결과 조회를 사용합니다. 실제 필드·경로·오류는 [서버 소스](../RemoteWork.Server/remotework/features/results.py)를 기준으로 검토합니다.

## 미연결·협의

최종 조직 상위 관계, C02 상태별 수정/재배정/취소, C07 연장 판단, 공용 첨부 재사용, MainForm/2번의 성공 이벤트 구독, 보존/삭제·당시 이름의 별도 스냅샷, 웹 화면은 남아 있습니다. 관리자 1명만으로는 자기 제출 검토를 시연할 수 없으며 실제 직원/상위 검토자 자료는 사용자가 입력해야 합니다.

검증 명령·결과와 실제 공유 DB/두 PC 미실행은 [결과물 검사 안내](../Tests/Results.Http.Tests/README.md)를 따릅니다. 사용자용 임시 로그인/localhost 안내는 사용자 요청에 따라 PR 본문에만 기록하고 DB 비밀번호·토큰·.env·실제 자료·빌드 산출물은 코드에 게시하지 않습니다.
