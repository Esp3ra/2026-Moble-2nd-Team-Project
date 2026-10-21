# 2번 업무 수락·재작업 시작

2026-10-20 최신화. [PR34의1번 기술 수용 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/1번/2026-10-19_수락_재작업_API_기술수용회신.md)에 맞춰 수락·재작업 시작을 기존 업무 화면에 연결했습니다. 공통 인증/HTTP/쓰기 지원을 재사용하고1번 코드·3번 제출/판단/회차/파일/이력과 DB 구조를 대신 변경하지 않습니다. **기본off 개발 기능**이며 정식 공유 적용은 별도입니다. 수정·재배정·관리자 변경은 [확정 A/B/C 구현](TASK_MANAGEMENT.md), 취소·연장은 [확정 D/E 구현](TASK_LIFECYCLE.md)을 따릅니다. 새 취소/대기 연장 차단의 실제3번 소비 연결 전 lifecycle 변경503을 유지합니다.

## 화면 흐름과 조회 시점

실제 로그인한 내 업무의 진행전은 업무 수락, 반려완료는 재작업 시작이다. 직원과 관리자 본인에게 같은 기준을 적용하며 지정 관리자가 타인의 업무를 대리 시작할 수 없다.

1. 기존 상세와 별도 `GET /api/v1/tasks/{id}/write-context`를 비교해 현재 본인 배정·대상 상태·버전을 읽는다. 상세 제목/이름/날짜와 문맥은 다른 조회 시점이다. 상태가 달라졌거나 본인 배정이 아니면 대상을 확인하지 않고 목록 재조회를 안내한다.
2. `대상 확인`은 불변 CheckedTarget만 보관한다. 그 뒤 별도 `업무 수락`/`재작업 시작` 클릭에서 CheckedTarget으로 새 불변 TaskStartRequest를 만들고 공통 키 발급·실행을 요청한다. 창 열기·조회·첫 확인만으로 상태를 변경하지 않는다.
3. 확인된 성공 뒤 창을 닫으면 기존 범위/전체·5상태 필터를 유지해 목록·상세를 재조회한다. 유효한 목록 조회 후에만 성공 요청을 정리한다. 현재 필터 밖으로 이동한 업무를 억지로 추가하지 않는다.

기본/상세 미리보기와 기존3인수 생성자는 읽기 전용이다. 실제 HttpTaskQueryService만 새 optional ITaskStartSource를 제공한다. 공유 ITaskQueryService/DTO나 다른 담당의 화면 생성 규칙은 바꾸지 않았다. Designer의 기존 세 버튼을 유지하고 실행 상태에 맞춰 안내/버튼명을 표시한다.

## 웹 수락·재작업 시작 — 2026-10-21

기존 query/common/start 옵션을 명시한 서버에서 현재 본인 배정의 진행전·반려완료 상세에 확인 버튼을 제공합니다. cookie 인증·CSRF로 `/tasks/<id>/start/accept` 또는 `rework-start`에 POST하면,현재 잠근 제목/기한/배정·업무 버전과 동일 로그인에 묶인 서명 확인폼을 만듭니다. 확인만으로 상태를 바꾸지 않으며,별도 `/tasks/write/execute`에서 API와 같은 `execute_start`가 현재 본인 권한·버전·취소·KST 기간을 재검사합니다. 최초 수락 시각과 배정 차수를 보존하는 재작업 정책도 같습니다.

성공 재응답/결과 확인은 현재 권한을 재검사하되 이미 저장된 성공의 예전 상태·날짜·버전을 다시 검사하지 않습니다. 불확실한 결과는 같은 열린 확인폼에서 조회하며,`not_recorded` 뒤 사용자가 누를 때만 동일 키·본문을 재전송합니다. 자동 재시도·새 키 자동 발급이나 닫은 폼의 보류 요청 복원은 없고,웹 기능 off에서는 이 웹 결과 경로도 닫힙니다. 성공 후 기존 필터를 보존한 현재 상세/목록 재조회 링크를 제공합니다.

소유 합성 브라우저에서 직원의 신규 업무 수락과 반려 업무의 재작업 시작→진행중을 확인했습니다. 서버 API/조회 DTO·공통 인증·3번 제품은 변경하지 않았으며 실제 취소·연장 쓰기503와 integrated ACK 미설치를 유지합니다. 최종 MySQL/API 실행 결과와 미실행은 [전용 검증](../Tests/TaskWeb.Tests/README.md),자세한 수명·담당 경계는 [웹 계약](TASK_WEB.md#task-web-create-start)을 따릅니다.

## API와 개발 활성화

기존 공통 설정과 검토된 개발 DB의 공통·002 업무·004 receipt 테이블을 준비한 운영 담당이 명시적으로 켠다. 요청 중 DDL/seed/초기화·전역 버전 변경을 하지 않는다.

```powershell
$env:REMOTEWORK_ENABLE_TASK_QUERY_DRAFT='1'
$env:REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT='1'
$env:REMOTEWORK_ENABLE_TASK_START_DRAFT='1'
```

| 요청 | 공통 action | 성공 |
|---|---|---|
| `POST /api/v1/tasks/{task_id}/accept` | 기존 `task.accepted` | 진행전→진행중·최초 수락 시각 저장 |
| `POST /api/v1/tasks/{task_id}/rework-start` | 새 `task.rework_started` | 반려완료→진행중·최초 수락 시각 보존 |

두 요청은 `Idempotency-Key` 헤더와 아래 두 필드만 받는다. 정규 양의 BIGINT 문자열·양의 JSON INTEGER를 사용하며 중복/미지원 필드·조회 조건·잘못된 JSON을 거절한다. actor·상태·수락 시각은 클라이언트가 지정하지 않는다.

```json
{"expected_row_version":"7","expected_assignment_revision":3}
```

성공은 공통 envelope의 HTTP200·최소5필드다. 새 성공에서만 업무 버전+1, 배정 번호 유지·서버 UTC updated_at을 저장한다. 원본 MySQL DATETIME 정밀도로 결과를 다시 읽는다.

```json
{"task_id":"101","row_version":"8","assignment_revision":3,"task_status":"in_progress","updated_at":"2026-10-19T00:00:00Z"}
```

시작 flag off이면 경로/새 action은 미등록이다. query off이면 업무 Blueprint 자체가 미등록이다. query+start on/common off는 경로만 등록하며 인증 후503 `WRITE_SUPPORT_DISABLED`로 닫고 receipt 모델/새 action을 추가하지 않는다. common on일 때만 초기화 중 새 rework action을 동일 authorizer 함수 객체·5필드로 등록한다. 기존 task.accepted를 다시 등록하거나 공통 whitelist를 바꾸지 않는다.

## 서버 권한·상태·트랜잭션

현재 인증/접속·본인 배정을 공통 guard/부모 업무 우선 잠금으로 검사한다. strict 입력 뒤 같은 Session/트랜잭션의 perform_once를 사용한다. 최초 mutate에서 배정 버전→업무 버전→대상 상태→수락 기록 정합성→버전 상한→KST 기간 순서로 확인한다. 갱신·최소 감사·성공 receipt는 함께 저장하고 실패하면 함께 rollback한다.

수락·재작업은 **KST 시작일00:00 이상~마감일 다음날00:00 미만**에만 허용한다. 사건 시각은 서버 UTC이며 DB의 naive UTC와 순수 함수의 UTC-aware 시각을 명시적으로 변환한다. 재작업은 기존 accepted_at·배정·과거 결과를 초기화하지 않는다. 반려인데 수락 기록이 없거나 진행전인데 이미 수락된 자료는 자동 복구하지 않는다.

| 응답 | 의미 |
|---|---|
| 401 / 403 / 업무404 | 인증 필요·만료 / 현재 본인 권한 없음 / 업무 없음 |
| 409 `TASK_ASSIGNMENT_CHANGED` / `TASK_VERSION_CONFLICT` | 배정 / 업무 버전 변경 |
| 409 `TASK_STATE_CONFLICT` / `TASK_START_DATA_INCONSISTENT` | 대상 상태 아님 / 모순된 수락 자료 |
| 409 `TASK_VERSION_EXHAUSTED` | signed BIGINT 버전 상한 |
| 409 `TASK_START_BEFORE_START_DATE` / `TASK_START_AFTER_DUE_DATE` | KST 시작 전 / 마감 후 |
| 공통 키409/410·503 | 키 충돌/만료·DB/기능/receipt 준비 필요 |

동일 키의 이미 성공한 결과는 예전 상태·expected 버전·날짜를 다시 검사하지 않는다. **재응답과 결과 조회에서도 현재 역할/배정 권한은 재검사**한다. 같은 키·다른 본문은 충돌이며 자동 재전송하지 않는다.

### 서버 내부 전이 계획

기존 start_dates.py·start_transition.py는 명시 UTC 시각과 불변 snapshot으로 계획만 계산한다. 실제 starting.py 어댑터가 현재 잠금/버전·HTTP 검사 순서를 적용한 뒤 이 계획을 사용한다. 순수 계획 자체가 인증·잠금·저장이 아니며 외부 HTTP 오류로 자동 변환되지 않는다. 두 동작은 제출/판단/회차/첨부/화면용 이력 테이블을 읽거나 수정하지 않는다.

## 응답 유실·창/접속 수명

Starting의 HTTP 서비스와 workflow는 하나의 captured ApiClient.Session을 사용한다. 불명확 결과는 원본 요청과 키를 같은 로그인 세션의 메모리에 보관한다. 창/메뉴를 다시 열어도 복원되며 목록0건·선택 없음·필터 밖에서도 `수락·재작업 결과 확인`을 열 수 있다. 복구창은 실제 원본 요청ID만 표시하고 가짜 TaskDetail을 만들지 않는다.

명시적인 `처리 결과 확인`에서 not_recorded를 받은 뒤에만 `같은 요청 재전송`을 허용한다. 동일 키/본문을 유지하며 새 키·자동 재전송은 없다. POST410은 보관 결과를 확인할 수 있지만 결과 조회410·키 충돌은 수동 처리 내역 확인으로 남긴다. direct 성공 응답의 HTTP 상태는 기존 ApiClient가 노출하지 않으므로 서버/실제 HTTP 검사에서 별도로 확인한다. 보관 결과는 http_status=200과 원본ID·버전+1·배정·상태/UTC를 엄격히 확인한다.

처리 중 일반 닫기는 기다리도록 하고, 결과 불명확 단계의 닫기는 세션 메모리 요청을 유지한다. 401/403·로그인 교체는 이전 확인값/요청을 지우며 늦은 응답이 새 접속을 바꾸지 못한다. 403은 권한 없음으로 안내하고 같은 로그인에서 메뉴 재진입으로 우회하지 못한다. 폼은 빌린 workflow/HTTP를 Dispose하지 않는다. 앱 종료·재로그인 후 요청 복원은 없으므로 다시 시작하기 전 서버 상태/처리 내역을 확인해야 한다.

## 검증과 남은 협의사항

- `Tests/TaskStart.Tests`: captured HTTP/명시 대역·계약·불확실 결과/같은 키·접속 수명, 선택적 실제 HTTP 모드.
- `Tests/TaskStart.Ui.Tests`: 실제 WinForms·명시 서비스 대역, 두 단계 실행·pending/필터 밖 복구·처리 중 닫기·접속/403·성공 재조회/ack.
- 서버 `test_task_start_registration.py`: cold 등록/비활성 metadata·strict 입력·HTTP 어댑터 순서. `test_task_start_api.py`: 명시 별도 MySQL DB의 실제 상태/권한·키·실패 rollback/경합.
- 기존 TaskStartPreparation.Tests의 읽기 전용 생성자·조회 회귀는 유지한다. MySQL/HTTP·대역·사용자 수동 GUI·3번 제출 통합은 각각 구분한다.

실제 실행 수치·미실행은 [LOG/2번 구현 기록](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/업무_구현검증.md), 남은 담당별 접점은 [공통규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md)을 따릅니다. 업무→기존3번 제출 폼 연결과 소유 환경의 실제 HTTP/UI 검증은 존재합니다. 새 lifecycle과 제출/판단/첨부의 충돌·과거 자료 보존은3번의 실제 소비 연결 후 검증해야 하며, 공용 이벤트/성공 구독과 공유 DB/서버·두 PC는 별도입니다. 수정·재배정·취소·연장 정책은2026-10-20 A~E로 확정됐습니다. 자동 결과 제출은 제외하고 별도 자동 검토 요청은 협의사항으로 유지합니다.
