# 1번 연차 서버·5번 연결 접점

이 구현은 2026-10-22 사용자 요청과 PR58의5번 전체 요청에 따른 **실행 가능한 개발 계약**이다. 팀장·5번의 계약 수용, 원본 병합, 실제 개인별 설정/공동 검증은 구분한다. 기존 지문/근태·업무 취소/연장 계약을 바꾸지 않는다. SQLite는 사용하지 않는다.

## 개발 정책과 준비

- 사용자 최신 지시: 기본 연차 **8일(16 half-day units)**, 공휴일 미차감. 앞서 승인한 협의안의 주말 제외/당일 반차0.5일, 신청 예약→승인 확정사용/반려 예약해제, 과거일·연도걸침 금지, 반려사유 필수를 구현했다.
- 계산은 서버 MySQL UTC를 KST 업무 DATE로 바꿔 수행한다. 1~12월, 연차 하루2units·반차1unit, 병가/공가0units. 부여-대기예약-승인사용=잔여이며 음수 상태는503이다. 시작/종료 양끝 날짜 포함, 주말/명시 휴일은 사용일수에서 제외한다. 근무일0이면409다.
- **협의 후 조정할 보수적 경계:** 대기/승인 기간이 날짜로 겹치면 오전/오후 반차끼리도409다. 서로 다른 반차를 같은 날 허용하는 최종 규칙은 아직 수용받지 않았다. 보존기간·철회/승인취소/수동 조정·자동 연도 부여/이월은 추가하지 않는다.
- 연도별 완전한 공휴일 목록과 직원별 담당관리자는 명시 설정한다. 외부 사이트 자동 수집, 모든 직원에게 기본값을 자동 지급, 임의 관리자 지정은 없다. 기본8일은 명시 준비 manifest에서 granted_units 생략 시16으로 쓰며 실제 계정 전체의 startup seed가 아니다.
- `REMOTEWORK_ENABLE_LEAVE_DRAFT=0` 기본값. API를 명시 설치해도 `REMOTEWORK_LEAVE_WRITE_POLICY`가 `calendar-year-reserve-v1`과 정확히 같아야 새 쓰기를 허용한다. 이 값은 **팀 정책·배포 확인**이며 테스트를 통과했다는 이유로 공유서버에 설치하지 않는다. 일치하는 연도 calendar/balance/reviewer도 모두 필요하다.

## 공통 요청/응답

기존 공통 Bearer와 `{ok,data,request_id}`,32KiB 상한·인증 만료·현재 권한을 재사용한다. 브라우저에 토큰을 넘기지 않는다. 아래는 C# JSON API이며 새 웹 UI/cookie 어댑터를 제공한 것은 아니다.

POST는 미지/중복 JSON 필드·중복 query/잘못된 UTF8·NaN을400으로 거절한다. UUID는 소문자 정규 D형 문자열, revision은 양의 signed BIGINT **JSON 문자열**, 사용자ID/일수units는 JSON 정수, DATE는 `YYYY-MM-DD`, UTC시각은 `...Z`다. 사유는 최대1,000문자이며 결정사유는 신청사유와 별개다.

|메서드/경로 (`/api/v1` 기준)|요청 / data|
|---|---|
|GET `/leave/readiness`|본인 신청 기준 `{schema_ready,write_ready,pending:[오류코드],year,server_time,policy_id}`. 담당관리자의 **본인 신청 준비**가 false여도 별도 직원 검토 권한을 뜻하지는 않는다|
|GET `/leave/balance?year=2026`|본인 `{year,granted_units,reserved_units,used_units,remaining_units,unit_days:"0.5",policy_id}`|
|GET `/leave/requests?scope=own\|review&limit=50&after=<uuid>`|기본own/limit50,최대100. 현재 본인 또는 현재 지정 관리자의 직원만 `{items,next_cursor,server_time}`. 각 페이지는 현재 자료이고 전체 페이지의 단일 snapshot을 보장하지 않는다|
|GET `/leave/requests/<id>`|현재 본인/지정관리자만 상세|
|POST `/leave/requests`|정확 `{intent_id,type,start_date,end_date,reason}` + Idempotency-Key. 본인 신청만 가능,201 최소 receipt|
|POST `/leave/requests/<id>/decision`|정확 `{intent_id,expected_revision,decision,decision_reason}` + Idempotency-Key. 현재 활성 지정 관리자·자기검토금지·pending/원revision 검사.200 최소 receipt|
|GET `/leave/operations/<intent_id>?action=...&target=...`|같은 actor의 원 의도 결과 복구. `completed/http_status/result/expires_at` 또는 `not_recorded`. action/target 및 현재 허가를 다시 검사|

type은 `annual/am_half/pm_half/sick/official`, decision/status는 `approved/rejected` 및 신청대기 `pending`이다.
상세 item은 `{request_id,revision,applicant_id,applicant_name,department,type,start_date,end_date,reason,status,charged_units,unit_days,can_review,decision_reason,decision_by,created_at,updated_at}`다.
최소 receipt는 `{request_id,revision,status,charged_units,updated_at}`만 보관한다. 사유/성명/파일/자격정보를 W1 receipt에 보관하지 않는다. GET 상세에서 별도 결정사유를 읽는다.

### 목록의 현재 권한·페이지 경계

관리자 검토 목록은 신청·현재 담당관계·활성 신청인을 직접 JOIN해 같은 MySQL locking/current read로 조회한다. 인증이 만든 이전 RR snapshot의 서브쿼리로 담당자를 판단하지 않는다. 현재 관리자·활성 신청인 조건을 `after`/`limit+1` 전에 적용하므로 회수되거나 비활성화된 신청은 개인정보를 노출하거나 정상 페이지 자리를 차지하지 않는다. 각 행의 직렬화에서도 본인/현재 지정관리자 권한을 재검사하며 `can_review=false`는 열람 허가를 대신하지 않는다.

비활성 신청인 한 명 때문에 다른 활성 직원의 검토 목록을403으로 중단시키지 않는다. 비활성 신청인의 상세 권한을 확대하지 않으며, 비활성 본인의 인증은 기존401 검사를 따른다. 본인 목록에는 담당관계 JOIN을 적용하지 않아 관리자 해제·비활성 후에도 현재 활성 본인의 이력 읽기를 유지한다. 관리자도 신청인이 될 수 있으므로 신청인 역할을 직원으로 제한하지 않는다. 페이지마다 현재 권한을 검사하며 여러 페이지 전체의 단일 snapshot은 보장하지 않는다.

## W1·원 요청 복구

- 기존 POST `/write-requests/keys`의 `{action:"leave.submitted",target:"new"}` 또는 `{action:"leave.decided",target:<신청UUID>}`로 키를 발급한다. 신청/결정 POST에 같은 키를 `Idempotency-Key`로 전송한다.
- 클라이언트 intent_id는5번이 이미 가진 Guid를 정규화한 **요청 의도 ID**이고, 서버발급 W1 key와 구분한다. 같은 actor/intent는 action/target/원본문 해시로 영속 결합한다. 원UUID에 다른 내용409, 새 키로 같은 의도를 보내도 요청/예약/감사를 다시 만들지 않는다.
- 성공 의도 결과는 성공시점 최대24시간(기존 W1 보관 설정이 더 짧으면 그 값)이며 재조회/재시도가 늘리지 않는다. 의도 행은 만료 후 **tombstone으로 남겨** 새 키/같은UUID 재실행을410으로 거절한다. 별도 보존 합의 없이 이 행을 자동 삭제하지 않는다. 서명된 W1 키 만료·actor/action/target/body 검사는 기존 코드 그대로다.
- 응답 유실 뒤 **GET 원의도** 또는 같은 key의 POST `/write-requests/result`로 확인한다. completed만 확정 성공,410/503/403은 not_recorded가 아니다. key발급응답 유실은 mutation 미송신이므로 명시 재발급 가능, mutation 송신 이후에는 새intent/key로 자동 전환하지 않는다.
- 정책 확인값을 제거한 새 app에서도 API flag/W1이 켜져 있으면 기존 성공 결과와 자체 조회가 가능하다. 새 쓰기는503. API flag 자체OFF는 경로를 제거하므로 보관 결과를 읽을 기간에는 ON/쓰기정책빈값을 사용한다.
- UI 취소/창닫기/통신 취소는 DB rollback 증거가 아니다. 같은 actor의 재로그인 뒤 명시 원의도 조회는 서버가 허용하지만, 클라이언트는 이전 접속 작업을 새 로그인/다른 서버로 **자동 인수하지 않는다**.

## 같은 TX·동시성

기존 `authenticated_transaction`의 guard→현재 actor/AuthSession 재검사 뒤 연차 현재 관계/잔액/신청을 locking read한다. Core SELECT의 `FOR UPDATE`를 사용해 인증 때 생긴 오래된 RR snapshot이나 ORM 캐시를 쓰지 않는다. 기존 공통 guard가 협력 쓰기를 직렬화하므로 서로 다른 날짜라도 같은 직원의 마지막 잔여일을 이중 예약하지 않는다. 1회 신청/예약·결정/사용·최소 감사·의도결과·W1 receipt가 같은 TX에 commit 또는 전부 rollback한다. guard를 지키지 않는 직접 SQL 수정은 지원하지 않는다.

관리자 role만으로 전 직원 접근 불가. 현재 활성 명시 담당관계와 요청 상태/원revision을 매번 검사한다. 담당/권한 회수 뒤 옛 결정 receipt 조회·재실행도 거절한다. 검토자 비활성 뒤에도 본인 신청 이력/반려사유는 보존해서 읽을 수 있고 새 신청은 준비 오류다. 병가/공가 신청도 검토·이력/기간 중복은 유지하며 예약/사용량만0이다.

## 5번: 기존 화면에 연결

1번 새 `Infrastructure/HttpLeaveWorkflow.cs`는 기존 공개 `ILeaveWorkflow`를 구현한다. 기존 Designer/폼/시작 코드는 수정하지 않았다.

```csharp
var captured = AppServices.Api.CaptureSession();
var workflow = new HttpLeaveWorkflow(captured); // 같은 로그인에서 하나를 공유
// 5번 기존 생성자에 captured, workflow를 전달한다.
var form = new LeaveApplicationForm(captured, workflow);
// 목록/상세/검토도 같은 workflow를 사용한다.
var preparation = await workflow.ReadinessAsync(cancellation);
var balance = await workflow.BalanceAsync(preparation.Year, cancellation);
var remainingDays = balance.RemainingUnits / 2m;
```

캡처 Session 외 토큰/HttpClient를 받지 않는다. 늦은 응답/401/다른 로그인/서버는 공통 규약을 따른다. 각 의도는 RAM에 원본문/key를 유지하고 동시 호출을 직렬화한다. 첫 POST 실패 직후 자동 재시도0. 다음 명시 호출에서는 원의도→필요시 원 W1 조회→둘 다 not_recorded인 경우에만 **같은key/body**를 한 번 재송신한다. 완료 receipt는 검증 후 캐시하며 재조회/목록 실패로 성공을 지우지 않는다. 신청 receipt를 나중에 승인된 GET 상태로 바꾸지 않고 원 pending 영수증/원 draft를 보존한다. 타입 한글/영문·DecisionReason을 매핑한다. 24시간/64의도 RAM한도·최대각scope20페이지를 넘으면 명시 오류이며 조용히 데이터를 누락하지 않는다. 새 로그인마다 새 instance를 만들고 일반 화면 dispose에서 다른 로그인 작업을 복원하지 않는다.

5번 남은 연결: 기존 메뉴/폼에 위 instance를 주입하고 잔여일/준비항목을 표시, 기존 원UUID 보관/불명 결과 확인 버튼을 이 adapter에 연결·실계정/두PC 검증한다. 기본 startup 자동 설치나5번 화면 전체를 교체하지 않았다.

## 016 SQL·명시 운영 준비와 복구

`migrations/016_leave.mysql.sql`은 별도5테이블 leave_years/balances/reviewers/requests/operations만 CREATE한다. 016 번호는 이번1번 제안/구현이며 기존 schema_versions[1]을 유지하고 다른 SQL을 대체하지 않는다. 먼저 최신 번호 충돌 여부와 부모 users.id를 확인한다. 부분 존재 시 재실행/자동ALTER/DROP하지 않는다.

전체 기존 DB와 외부 파일을 비공개 백업→**다른 새 소유 MySQL**에 실제 dump 복원/모든행·DDL·파일 해시 대조→016 리허설·엄격 storage/원API 검사→정확 실행SHA/기능설정 기록→공유 적용 순서다. startup/init-db가 연차 DDL을 자동 생성하지 않는다. 기존키/장치/근태/업무ACK를 변경하지 않는다.

초기 개인별 준비는 비공개 manifest로만 한다. 아래ID는 예시 자리표시이며 실제 계정에 자동 적용하지 않는다. holidays는 해당연도 **완전한 확정 목록**이어야 하며, 예시빈배열을 실제 한국 공휴일0개라는 뜻으로 사용하지 않는다.

```json
{"year":2026,"policy_id":"calendar-year-reserve-v1","holidays":[],"members":[{"applicant_id":123,"manager_id":456}]}
```

```powershell
python -m remotework.leave_setup --manifest <비공개JSON절대경로> --operator-login <활성관리자로그인> --ack-policy calendar-year-reserve-v1
```

이 로컬 운영 도구는 DB접근권한이 있는 운영자 전용이며 웹 관리 API가 아니다. 현재 활성 관리자·직원·명시 담당관계/guard를 확인하고 동일 설정 재실행은 무변경, 기존 달력/지급/담당자 덮어쓰기는 거절한다. 기본16units를 명시 멤버에만 추가한다. 전체manifest검증·추가·최소감사를 한 TX에서 수행한다. 실제 자료 확인과 팀 정책 합의 전 공유 쓰기 확인값을 설정하지 않는다.
장애 시 쓰기정책값 제거/같은서버 재시작으로 읽기·원결과 복구 유지, 새참조/예약/감사/tombstone 보존. 백업을 실사용 DB에 덮어쓰거나 테이블을 DROP해 예약을 지우지 않는다. 승인 취소·일괄 원복은 별도 검토다.

오류: 입력400/현재권한403/없는신청404, 중복기간·잔여부족·과거·버전·의도충돌409, 원키/의도 만료410, 정책/달력/부여/담당/물리스키마미준비503. 500/네트워크 불명은 원결과 조회 전 새 신청으로 바꾸지 않는다.

## 검증

`Tests/LeaveService.Tests/README.md`의 소유 MySQL/raw016·실제 HTTP/C# 검사와 실제공유설치를 구분한다. 고정시험 회원/휴일/관리자관계·시각은 fixture이고 실제 사용자에 지급하거나 승인한 결과가 아니다. 날짜LOG는 팀장 게시 대상이며1번은 로컬 전달 요약만 준비한다.
