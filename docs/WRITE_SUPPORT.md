# 1번 W1 공통 쓰기 지원 — 2번 연결 안내

## 최신 보완: 기능 종료 후 성공 결과 조회 (2026-10-20)

`register_write_result_reader(app, action=..., authorize=..., read_only=False)`로 기능 소유자가 **조회 전용 현재권한 검사**를 앱 초기화 때 명시 등록할 수 있다. 기존 `register_write_action`과 쓰기 callback은 유지한다. 결과 조회도 같은 인증 transaction 안에서 현재 행위자·접속·업무 관계를 다시 검사한 뒤 서명 키의 사용자/action/target와 보관 기한을 확인한다. 성공 결과의8필드·24시간·단일 감사/receipt와 만료키 재실행 금지는 바뀌지 않는다. 조회는 DDL·새 결과/감사·새 요청 키를 만들지 않는다.

신규 등록은 앱별로 분리하며 동일 action 재등록·첫 요청 이후 등록을 거절한다. 등록하지 않은 기능은 기존 writer authorizer를 그대로 사용한다. `read_only=True`로 등록한 행동은 공통 키 발급/새 실행을 거절한다. 조회 callback을 쓰기의 권한 함수로 대체해서는 안 된다.

2번 취소·연장은 기존 현재 배정/지정관리자·자기 판단 금지를 재사용하는 읽기 callback을 별도로 등록한다. 실행 중 쓰기 flag를 끄거나 ACK를 제거해도 현재 권한이 유효한 기존 성공 결과는 보관 창 안에서 조회할 수 있다. 새 업무 실행은 기존 flag/ACK/schema gate로 계속 차단한다.

**재시작 후 읽기 전용 운영도 필요할 때만** 아래를 명시한다. 기본값은 모두 기존대로 off이며, 실제 사용자 `.env`/5000 서버에는 이번 보완을 자동 적용하지 않는다.

```text
REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT=1
REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1
REMOTEWORK_ENABLE_TASK_LIFECYCLE_DRAFT=0
REMOTEWORK_ENABLE_TASK_LIFECYCLE_RESULT_READS_DRAFT=1
```

이 모드는 취소·연장 업무 쓰기 경로/키를 열지 않으며 기존 공통 키/결과 경로에서 해당5개 행동의 저장 결과만 확인한다. 공통/업무/009·receipt 테이블과 같은 서버 서명 secret이 필요하다. 미준비 schema·권한 회수·변조 키·만료를 빈 성공으로 바꾸지 않는다. `not_recorded`도 새 업무 실행 지시가 아니다. 실제3번 소비·공유 배포를 검증했다거나 전체 integrated ACK를 설치했다는 뜻은 아니다.

아래2026-10-17 설명은 최초 공통 지원 이력이다. 현재 실제 제품 구현/합의 상태는 각 담당의 최신 안내와 [취소·연장](TASK_LIFECYCLE.md)을 함께 확인한다.

3번 제출·검토 연결을 위한 접속 범위와 기능별 receipt 등록 도구는 [RESULTS_INTEGRATION.md](RESULTS_INTEGRATION.md)를 참조한다. 기본 task 계약은 유지하며 실제 제품 제출 행동·검토 권한·파일 저장은 아직 연결하지 않았다.

2026-10-17. 기준 alpha c56f3c8의 기존 업무 GET/DTO/CheckedInput은 유지한다. 이 문서는 **공통 지원**이며 업무 등록·수락·연장·제출·승인 API 구현이 아니다.

## 적용 범위와 승인된 기준

사용자가 현재 대화에서 다음을 승인했다. 활성 admin은 새 업무를 생성할 수 있다. 기존 업무 수정/재배정/조회 관리자 변경의 역할 기준은 현재 admin이면서 해당 업무의 지정 관리자다. 직원과 관리자 본인의 수락/연장 신청은 현재 배정 당사자만이다. 조직별 추가 제한은 이번 기준에 없으며 후보는 이 권한을 가진 호출자에게 활성 계정만 제공한다.

위 기준은 상태·마감 조건을 대신하지 않는다. 3번 W3의 초기 저장·허용 상태/수락 예외·제출 원자성·마감 시각·연장 판단자·검토자·자기 판단 금지는 별도 확인한다. 자동 결과 제출은 계속 제외한다. 승인/반려 authorizer와 실제 업무 쓰기는 추가하지 않았다.

공통 지원은 `REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT=1`의 **명시적 개발 opt-in**이며 기본 off다. 업무 준비 API에는 기존 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`도 필요하다. 실행 중/공유 DB에 자동 DDL을 수행하지 않는다. 검토한 전용 MySQL·공통 테이블·002 업무 초안·[004 결과 저장소](../RemoteWork.Server/migrations/004_write_receipts.md)를 준비한 뒤 별도 개발 프로세스에서 연결한다. 기존 사용자 `.env`와 공유 서버를 이번 작업에서 바꾸지 않았다.

## 실제 제공한 API

공통 Bearer·`ok/data/request_id`·서버 사용자 문맥을 사용한다. 아래 경로는 두 flag와 DB 준비 후 등록된다. 후보 API/문맥은 업무 flag도 필요하며 공통 키/결과 API는 공통 flag만으로 등록된다.

| 메서드·경로 | 입력·결과 | 현재 권한·의미 |
|---|---|---|
| GET `/api/v1/tasks/assignment-candidates` | 선택 `task_id`, `q`, `page`, `limit` → `items/page/limit/total` | task_id 없으면 활성 admin의 새 업무 문맥. 있으면 admin+현재 해당 업무 지정 관리자. 활성 employee/admin만 반환 |
| GET `/api/v1/tasks/manager-candidates` | 같은 조건 | 같은 호출 권한, 활성 admin만 후보. 제출 검토자 목록이 아님 |
| GET `/api/v1/tasks/{task_id}/write-context` | `data.context`: task_id/row_version 문자열, assignee_id/assignment_revision 정수, manager_user_ids 정수 배열, task_status·UTC updated_at | 현재 배정 당사자 또는 admin+해당 업무 지정 관리자. 허용 상태나 저장 성공을 보증하지 않음 |
| POST `/api/v1/write-requests/keys` | body `action,target`만 →201 `request_key,expires_at` | 아래 action의 현재 권한 검사 후 서버 서명 키 발급. 키 발급은 업무 쓰기 승인/저장이 아님 |
| POST `/api/v1/write-requests/result` | body `action,target`, `Idempotency-Key` 헤더 → `state=completed,http_status,result,expires_at` 또는 `state=not_recorded` | 현재 대상 권한을 다시 검사. 응답 유실 후 결과 확인. not_recorded는 해당 시점 성공 기록 없음이며 자동 새 요청 지시가 아님 |

후보 필드는 `id`, `display_name`, `selection_label` 세 개만이다. label은 표시명·부서·역할 구분·서버 ID를 포함해 동명이인을 구분한다. login_id·전체 사번·계정 생성 시각·비활성 목록은 반환하지 않는다. 선택은 label이 아닌 id로 보존한다.

검색 q는 표시명/부서의 부분 일치이며 최대80자, `%`/`_` 등은 와일드카드로 해석하지 않는다. page 기본1·최대1000000, limit 기본20·1..100, 표시명→id 순서다. 중복/미지원 query key와 잘못된 숫자는400이다. 페이지는 요청별 현재 결과이며 여러 페이지의 불변 snapshot을 보증하지 않는다. 일부 페이지 실패를 완전한 목록으로 표시하지 않는다. 현재 구현은 정확한 items/total을 위해 후보 ID를 잠금 조회하므로 대규모 인명부 최적화는 별도 검토 대상이다.

키 action은 `task.created/updated/reassigned/managers_changed/accepted/deadline_extension_requested/deadline_extension_approved/deadline_extension_rejected` 중 하나다. created의 target은 `new`, 나머지 현재 연결된 업무 action의 target은 양의 정규 BIGINT task_id 문자열이다. 연장 승인/반려는 목록에 예약돼 있으나 authorizer 미연결이므로503 WRITE_ACTION_NOT_READY다. 확정되지 않은 판단 권한을 기본 admin으로 열지 않는다.

## 2번 서버 서비스 연결 순서

```python
from flask import request
from remotework.auth import authenticated_transaction
from remotework.common import success
from remotework.task_write_support import authorize_task
from remotework.write_contract import ExpectedVersions, lock_users
from remotework.write_requests import WriteResult, perform_once

# 실제 행동별 라우트는 2번이 합의된 API/상태/입력 검사를 구현한다.
with authenticated_transaction() as db:
    def authorize(session, target):
        # 현재 task/배정/관리 관계를 locking read로 재확인한다.
        return authorize_task(session, target)  # 본인 수락/연장 신청의 역할 기준

    def mutate(session):
        # 입력은 이미 엄격하게 검증한 body. 현재 잠금 업무 row를 사용한다.
        ExpectedVersions.from_body(body).check(
            row_version=task.row_version,
            assignment_revision=task.current_assignment_revision)
        # 여기에서 W3가 확인된 상태/날짜/중복·초기값 정책을 검사한다.
        # 실제 업무 저장 + 버전/UTC updated_at + 공통 audit를 같은 session에 기록한다.
        # 새/변경 후보는 lock_users(db, ids)로 잠근 현재 active/role/범위를 재검사한다.
        return WriteResult(minimal_result, 200)

    result = perform_once(db, key=request.headers.get("Idempotency-Key"),
        action="task.accepted", target=task_id, body=body,
        authorize=authorize, mutate=mutate)
return success(result.data, result.status)
```

위 코드는 조립 순서 설명이며 task/body/minimal_result·상태 정책이 정의된 제품 API가 아니다. 2번은 같은 DB session을 유지한다. `authenticated_transaction()`이 guard→수행자→접속을 현재 locking read와 ORM refresh로 검사한다. 이후 공통 authorizer는 task→현재 배정→정렬된 관리자 관계를 확인하고, 후보 사용자 잠금은 ID 오름차순이다. 모든 협력 쓰기가 같은 guard를 사용해야 하며 2·3번은 부모/자식 순서를 맞춘다. 현재 방식은 전역 guard로 협력 쓰기를 직렬화한다. 외부 DB 수동 변경까지 동시성 안전하다고 보증하지 않는다. 트랜잭션 안에 네트워크/파일 전송을 넣거나 중첩 commit하지 않는다.

**authorize는 명시적으로 True를 반환해야 한다.** None/False는403이다. replay·결과 조회에도 현재 권한을 검사하며, 버전·상태 검사는 최초 mutate에 둔다. 재응답 전에 옛 expected 버전을 검사해 정상 replay를 충돌로 바꾸지 않는다. 업무/감사/receipt는 한 commit/rollback이며 실패/rollback에는 성공 receipt·감사가 남지 않는다. 같은 키/같은 본문은 기존 결과, 같은 키/다른 본문은409다. body 지문에는 expected 값도 포함한다. 저장소에는 본문/키 원문을 저장하지 않는다.

최소 result 허용 필드는 task_id·row_version·assignment_revision·task_status·updated_at·extension_id·requested_due_date·due_date다. 제목/설명/사유·계정/파일 내용은 저장 결과나 감사에 넣지 않는다. action별 필수 필드·버전 증가 시점·초기값은 2·3번의 서비스 계약에서 확정한다. 감사 이름은 2번 제안 `task.created/updated/reassigned/managers_changed/accepted/deadline_extension_requested/approved/rejected`, resource=`task`, resource_id=task_id 형식을 1번 범위에서 수용한다.

## 버전·오류·보관 기준

- 업무 ID/expected_row_version: 부호·공백·앞0 없는 양의 10진 문자열, 최댓값9223372036854775807. expected_assignment_revision/사용자 ID: bool/소수/문자열이 아닌 양의 JSON INTEGER, 최댓값2147483647.
- 현재 인증401 AUTH_REQUIRED → 현재 행동 권한403 FORBIDDEN → 허용 입력/현재 배정/버전/상태 검사 순서를 유지한다. `ExpectedVersions.check()`는 배정 불일치409 TASK_ASSIGNMENT_CHANGED를 먼저, 버전 불일치409 TASK_VERSION_CONFLICT를 다음으로 반환한다. 역할/관계 검사를 이 함수가 대신하지 않는다.
- 업무 없음404 TASK_NOT_FOUND, 입력400 INVALID_INPUT, 같은 키 다른 본문409 IDEMPOTENCY_KEY_REUSED, 만료410 IDEMPOTENCY_KEY_EXPIRED, 미준비503을 구분한다. 일반 DB 오류는 기존 DATABASE_UNAVAILABLE503으로 처리하며404/빈 성공으로 숨기지 않는다.
- 사용자 승인 기본값 `WRITE_REQUEST_TTL_SECONDS=86400`: 키는 발급 후24시간 동안 새 실행 가능, **성공 결과는 최초 성공 저장 시점부터24시간 조회 가능**. 키 실행 창이 끝났어도 성공 결과 보관이 남아 있으면 현재 권한으로 결과 조회만 가능하다. 만료 키는 receipt 삭제 뒤에도 서명된 만료 시각으로 새 실행을 거절한다. 테스트에서는 짧은 TTL을 명시할 수 있다.
- 만료 결과는 API에서 더 이상 반환하지 않는다. 물리 정리는 공통 flag를 켠 해당 전용/승인 환경에서 `python -m flask --app remotework cleanup-write-requests`로 수행한다. 공유 환경의 정리 실행자/주기·백업 보존은 팀장 적용 계획에 포함한다. SECRET_KEY는 재시작에 유지하고 키/토큰/비밀번호를 Git/로그에 넣지 않는다.

## C# 연결

`ApiClient.PostIdempotentAsync<T>(path, body, requestKey, ct)`와 `PatchIdempotentAsync<T>`를 제공했다. 키는 서버 발급값을 한 작업 동안 유지하고 새 전송에서 임의 재생성하지 않는다. 일반 Post/Patch/Get/로그인에는 키가 자동으로 붙지 않는다. 잘못된 헤더 문자를 전송 전에 거절하고, 기존 세대 취소·현재401 만료·이전 응답 차단을 유지한다. 자동 쓰기 재시도는 없다.

응답 유실 시 같은 action/target/key로 `/write-requests/result`를 확인하고, 현재 데이터 재조회·사용자 의사에 따라 같은 키 재전송 여부를 판단한다. 만료 후 새 키로 이전 성공 작업을 무조건 재실행하지 않는다. 2번의 GET C# DTO·CheckedInput은 이번에 변경하지 않았으며 새 문맥 DTO/실제 사용자 선택 UI·행동 호출은 2번이 연결한다.

## 검증과 남은 연결

MySQL의 실제 RR 권한 회수/배정·관리 회수, 중복/본문 충돌/응답 유실·만료/rollback·raw004를 검증한다. C# 제어 전송 회귀와 [실제 HTTP→MySQL 검증](../Tests/WriteSupport.Http.Tests/README.md)은 별도다. 해당 테스트의 mutation은 시험 프로세스 전용 경로이며 제품 업무 쓰기 구현으로 세지 않는다.

`db.schema_status(required_versions=...)`와 `check_schema(required_versions=...)`는 적용 기록의 실제/요구 버전을 비교한다. version 행은 물리 DDL/호환성 증명이 아니며 공통 현재 버전/health는1 유지다. 002의 목표2·003 공지·004 공통 결과 테이블의 실제 공유 적용/글로벌 버전 변경은 별도 검토한다. 새 요청 API는 테이블/열이 없으면503 WRITE_SCHEMA_REQUIRED이며 HTTP 요청 중 자동 생성하지 않는다.

공식 PC/IP·계정·공유 백업/복구·정식 버전/DDL 적용·두PC·업무 저장 UI·제출/승인/파일·W3 미정 정책은 아직 완료하지 않았다. 협업 회신과 실제 테스트/커밋/PR은 LOG 브랜치 루트1번 대표 문서와2번 전달 링크에서 추적한다.
