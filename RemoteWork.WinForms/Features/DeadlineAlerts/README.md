# 2026-10-22 PR70 실제 HTTP 소비

원본 alpha7135a29, 제공 구현2a50535/PR70 병합cca5b62를 소비한다.
`HttpDeadlineAlertSource`는 목록의 불변 event_id와 배정별 target을 구분한다.
화면의 기존 EventToken 전달 포트에는 서버 target을 그대로 넣고 새 EventId 속성에
실제 UUID를 보존한다. target은 서버 제공값을 검증할 뿐 UUID를 재발급/해시하지 않는다.
원 task/event/assignment·원 W1 key/body와 성공/불명 캐시는 target별이다.
동일UUID/본인배정1→타인2→본인3은 서로 다른 target이며 옛 확인을 승계하지 않는다.
같은UUID의 원 업무 변경, 잘못된 receipt/수치형 BIGINT·문자열 revision은 거절한다.
확인 성공 후 목록 실패는 성공 유지/GET 복구, not_recorded 후 명시 재시도는 원문 그대로다.
이전 raw-event operation을 새 target/키로 변환하지 않는다.

공지 화면은 실제 로그인에서 전체 HTTP snapshot을 먼저 검증하고 1번의 기존
`MainForm.ConnectDeadlineAlertsAsync`로 로그인당 scope를 연결한다. 이후 메뉴는 scope를
빌리고 메뉴 종료로 수신을 끝내지 않는다. 공통 MainForm/인증/CapturedSource는 수정하지
않았고 wrapper의 기존 string 전달로 원 target/EventId가 보존되는 것을 검사한다.
최초 서버 미준비/404/503이면 가짜 데이터나 scope를 설치하지 않고 실제 오류를 표시한다.
화면의 '마감 알림 새로고침'으로 다시 연결한다. preview/미로그인은 요청하지 않는다.

배포는 별도다. 1번의 실제 서버에 새3테이블/설정/2번 원 writer 연결·기존자료 준비가
필요하다. 클라이언트 복사만으로 DB/서버를 변경하지 않는다. 기존 두열PK/합성사건 초안은
설치하지 않는다. A1~A6/B1~B7·60초 조회/전날09KST·기존 상태/운영 협의 구분을 유지한다.
검증: `Tests/DeadlineConnection.Tests/run_binding_mysql_check.py`, 실제 소유 MySQL의
원본1번 API·실제 TCP·C# source/캡처 wrapper/scope/panel/MainForm 조립을 검사한다.
업무/기한 기록 호출은 fixture이며 2번 제품 hook/공유 서버/물리 두PC 검증은 아니다.

---

# 마감 알림 — 4번 독립 구현과 1·2번 연결 검토

현재 대조 기준: 원본 alpha9c5c07c / LOG0e0990e (2026-10-22).
최초 구현 기준 alpha388d50b / LOG f2d0c43,
[A1~A6 채택](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member4-policy-adopted-20261021),
기존4번 N4-08/F4-01과1번 기술 답변. 과거의 정책 제안값을 최신 채택 기준으로 대체해 읽는다.

**팝업/조회 수명/목록과 확인 성공·현재 목록 분리는 구현했다.**
1번 PR64의 MainForm 명시 source 연결·2번 실제 업무 탐색은 원본에 병합됐다.
실제 마감 source/API·기한 사건/초기자료·MySQL 확인 저장은 여전히1·2번 후속이다.
실제 서버에 마감 알림이 작동한다고 안내하지 않는다. 이 문서와 C# port는 기술 초안이며
새 JSON 필드/endpoint/DDL·migration 번호·공통 기능 옵션을 확정하거나 자동 설치하지 않는다.

## 반영한 기준

- 현재 본인 담당의 진행전/진행중/반려완료만 대상. 관리자도 본인 담당만 해당한다.
- 마감 전날09:00 KST부터 마감일 종료까지 보충 팝업. 서버 시각을 쓰며 클라이언트 시각으로 대체하지 않는다.
- 경과 건은 현재 관계/상태를 유지하는 미확인 목록에서만 표시한다.
- 소리 없는 작은 비모달1개.60초 조회, 실패2→4→5분, 성공 시60초 복귀.
- 닫기/실제 표시는 로그인 scope에서 중복 억제하며 메뉴 종료는 scope를 폐기하지 않는다.
- 확인은 source의 실제 서버 결과가 있어야 반영한다. 통신 실패는 미기록으로 추정하지 않는다.
  source가 같은 사건의 원래 공통 W1 key/body를 보존한다. 불명 상태의 다음 클릭은 결과 조회만,
  미기록 확인 뒤 별도 사용자 클릭으로 같은 동작을 명시 재시도한다. 자동 재전송은 없다.
- 현재 목록 뒤 표시 직전 recheck가 필요하다. 그 await 중 owner 숨김/최소화·다른 팝업/모달도 다시 검사한다.
- 현재 원본이 제거/취소/제출/승인/재배정/권한 회수·확인을 반영하면 표시 중 창도 닫는다.
- opaque EventToken은 서버의 실제 마감 변경 사건을 나타내야 한다. 날짜나 row_version만으로 만들지 않는다.
  같은 날짜/본문 수정은 같은 사건, A→B→A는 다른 사건이다.옛 사건 확인이 새 사건으로 전이되지 않는다.
- 과거90일 조회는 채택 기준이지만 실제 history 조회·물리 삭제/감사/백업 정책은 이 초안에 구현하지 않았다.
  폐기/이력 저장소와 현재 권한 계약을1·2번 회신 후 연결해야 한다. 실제 데이터 삭제를 실행하지 않는다.

## 파일과 조립 접점

- DeadlineAlertModels: 순수 정책·in-process port. task_id/row_version은 정규 signed BIGINT 문자열,
  assignment_revision은 signed INTEGER. EventToken은 미확정 기술 이름이며 wire DTO로 사용하지 않는다.
- DeadlineAlertScope: 로그인당1개, 현재 접속 취소/로그아웃/owner 종료에 폐기. source는 빌리며 Dispose하지 않는다.
  source의 Read/Recheck/Confirm/Resolve가 실제 현재 인증·관계·사건과 MySQL 저장을 제공해야 한다.
- DeadlineAlertForm/InboxForm: 작은 알림/미확인 목록. 업무 보기 callback은 현재 recheck 뒤 task_id만 전달한다.
  공통/2번 화면이 다시 현재 조회 권한을 검사해야 하며 이 callback이 조회 권한을 부여하지 않는다.
- DeadlineAlertPanel: 공지 화면은 scope의 읽기 view를 빌린다. 독립 타이머·샘플 업무·로컬 영속 저장은 없다.
- NoticesControl.Deadlines: 기존 가짜 주간 보고서 마감 표시를 제거하고 서버 준비/현재 알림 상태를 표시한다.

1번 PR64 조립 접점(이번4번 보완에서 MainForm은 변경하지 않음):

```csharp
// 실제 인증/사건/확인 어댑터가 합의·검증된 뒤만 설치한다.
await ConnectDeadlineAlertsAsync(session => reviewedSourceFactory(session));
// NoticesControl은 같은 owner chain의 scope를 찾아 빌린다.
// 명시 연결은 page.BindDeadlineAlerts(scope); 메뉴 종료는 scope를 Dispose하지 않는다.
```

## 1·2번에 필요한 기술 회신 — 같은 정책의 재승인 요청 아님

1. **2번:** 같은 보호 TX에서 현재 업무·실효 due_date·본인 배정·상태/취소를 제공할 최소 reader.
   기존 TaskReadSession의 공개 DTO에는 row_version/assignment_revision/현재 관계/서버 시각이 없으므로
   기존 목록만으로 권한·기한 사건을 증명하지 않는다. 원본 업무 DTO를4번이 바꾸지 않았다.
2. **2번:** 실제 기한 변경 이력에서 A→B→A/같은 날짜 저장/본문 수정과 재배정·중간 제출/복귀를 구별할 근거.
   과거 근거가 부족한 자료의 초기 사건 생성/전환 방식도 명시해야 한다. due_revision을 임의 추가하지 않는다.
3. **1번:** 현재 인증/서버 UTC 시각·계정 단위 확인·현재 사건 재검사의 같은TX/잠금 및 W1 행동/최소 결과.
   오래된 사건409/권한 회수/401403503·기능off 뒤 보관 결과 읽기를 기존 공통 계약에 맞춰 대조한다.
4. **4번→1번 DB 제안:** 업무/배정/기한 변경 원본은2번이 유지하고4번은 알림 사건/확인 상태만 별도 보관한다.
   사건/계정 UNIQUE, task/user FK 보존, 현재 활성 무효화 사유와90일 조회를 검토한다.
   실제 컬럼·물리 타입/인덱스·FK 동작·설정명/SQL번호는 회신 후 정식 초안으로 제공한다.
   다른 담당 테이블 ALTER/자동DDL/전체DB reset·SQLite 저장은 없다.
5. **1번:** 로그인당 scope1개·메뉴 수명/공유 소유·기존 중요공지/협업의 양방향 동시 표시 조정·모달 중 보류.
   현재 scope는 이미 보이는 창에 양보하지만 상대 기능이 나중에 표시하는 역방향 통합은 공통 조립 검사다.
6. **1·2번:** 현재 목록/사건 recheck/계정 확인·결과 조회/90일 과거 읽기의 최소 계약과 실제 공유 적용 가능 시점.
   개별 경로/필드/테이블 이름·번호를 이 C# 초안만으로 확정하지 않는다.

3번 파일/결과물·5번 장치/근태의 성공을 선행조건으로 붙이지 않는다. 현재 권한 경계를 유지하고
실제 결과 제출/업무 상태 변경·담당자 배정·파일 열람/제출을 알림이 대신 실행하지 않는다.

## 검증

`dotnet run --project Tests/DeadlineAlerts.Tests -c Release`.
실제 WinForms+명시 source 대역이며 실제 HTTP/DB·물리 두PC 검사가 아니다.
현재 서버 adapter/확인 테이블을 만들지 않았으므로 DB 검사와 실서버 왕복은 미실행이다.
MySQL만 허용하며 SQLite 테스트/대체 저장소는 사용하지 않는다.

API/DDL 합의 후 MySQL에서 같은 사건 중복 확인·동시 PC·변경/옛 확인 경합·보존/복원·물리 구조·
실제 현재 업무 상태 변경 TX와 경합을 추가 검증해야 한다. 과거90일 API/화면·실제 서버 적용도 남아 있다.

## PR61 R61-01~03 보완

- 업무 보기에서도401/403이면 공통 Dispose로 snapshot/팝업/타이머/owner 등록/탐색 callback을 종료한다.
- 조회·표시 전 재검사·업무 보기는 공통 요청 순서를 사용한다. 더 늦게 시작한 요청이나 확인 동작이
  이전 요청을 무효화하며, 예전 응답은 Apply/탐색을 하지 않는다. 오래된503도 최신 화면을 지우지 않는다.
  현재 로그인에 대한401/403은 응답 순서와 무관하게 종료한다. 업무 화면의 현재 권한 검사는 별도다.
- snapshot의 중간 반영은 실패 횟수를 초기화하지 않는다. 표시 전 재검사가 필요하면 그 성공까지 포함한
  조회 한 회 전체가 성공한 뒤에만60초로 복귀한다. Read 성공/Recheck503 반복도2→4→5→5분이다.
- 실제 WinForms 회귀는401/403 각각, 늦은 업무보기/목록 양방향·업무보기끼리 양쪽 완료 순서,
  오래된503,Read503/Recheck503 대비와 정상 복귀를 포함한다. 기존78+새27=105검사다.

[2번 기술 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/61#issuecomment-6311013832)을 수신했다.
현재 원본/잠금과 업무 변경·연장 감사의 재사용 가능성은 확인했지만, 최소 reader·사건/초기 자료 완전성·
현재 확인 writer/API/DTO는 아직 제공 완료가 아니다. task_id 탐색 callback은 이후2번이 제공했고
1번 PR64에서 공통 소비를 연결했다. 별도 recheck가 확인 TX의
원자성을 대신하지 않으며 기존 기술 대기/None·미확정 사건/DB 계약을 그대로 유지한다.

## 2026-10-22 확인 성공과 목록 갱신 분리

[1번 최신 수용 회신](https://github.com/jinkyu1452-cmd/Sang1/pull/1#issuecomment-6312083676)에 따라
`DeadlineConfirmation`의 snapshot을 선택값으로 바꿨다. 이는 C# 내부 port이며 새 wire/endpoint/DDL이 아니다.

- `Confirmed`는 source가 원래 사건·현재 계정·action/target·원래 W1 key/body에 결합된 성공 receipt를
  검증한 결과다. source는 이 성공 증거와 원문을 유지한다. UI 상태만으로 성공을 제조하지 않는다.
- 확인 성공 뒤 전체 목록이 없거나 잘못됐으면 `Confirmed`를 유지하고 snapshot만 버린다.
  **확인 완료 · 목록 재조회 필요**를 표시하고 과거 팝업/현재 facts를 비우며 업무 보기를 막는다.
  빈 정상 목록이나 새 사건의 확인으로 만들지 않는다. 다음 복구는 `ReadAsync` GET이다.
- 성공 사건은 로그인 scope 수명에만 기억해 같은 사건의 다른 화면/중복 클릭이 다시 POST/Resolve하지 않게 한다.
  실제 계정별 DB 저장·W1 보관 결과를 대체하는 저장소가 아니다. 로그아웃/같은 계정 재로그인 뒤 승계하지 않는다.
  새 기한 사건의 token은 옛 성공과 별개다. A→B→A의 실제 사건 ID는 source가 제공해야 한다.
- snapshot은 언제나 권위 있는 **전체 현재 목록**이다. 확인된 동일 사건을 미확인으로 되돌리는 목록이나
  타입·관계·중복이 잘못된 snapshot은 사용하지 않는다. 정상 GET 복구 전에는 stale facts로 업무를 열지 않는다.
- Unknown/NotRecorded/Changed에는 전체 현재 snapshot이 필요하다. 잘못된 상태/응답·503·410/통신 실패는
  미기록으로 추정하지 않는다. 불명 결과는 source의 원래 작업으로 읽기 전용 결과 조회를 한다.
  유효한 미기록 판정 후 별도 사용자 명시 재시도만 가능하다. 자동 새 key/POST는 없다.
- source가 성공 receipt 이후 목록 실패를 예외/Unknown으로 바꾸면 성공을 전달할 수 없다.
  그 경우 `Confirmed`와 snapshot 없음으로 반환해야 한다. 현재401/403은 기존대로 예외/접속 폐기이며
  목록 실패를 핑계로 인증 경계를 통과시키지 않는다. source/W1 action 등록·OFF 독립 reader는1번 소유다.
- 1번의 `POST /api/v1/write-requests/result`는 기존 공통 접점이다. 실제 deadline action/target/최소 receipt는
  아직 후보이며 설치하지 않았다. 키 발급 후24시간과 성공 결과 보관24시간을 구분하고,
  OFF reader 미설치503/401403/410을 not_recorded로 바꾸지 않는다.

검사: 기존105 + 후속57 = **162개**. 실제 WinForms의 팝업/목록·명시 source/HTTP handler와
1번의 실제 `LoginDeadlineNotifications`·캡처 ApiClient.Session을 사용했다. 저장 성공/목록 누락·불량,
GET503/형식 실패와 회복, POST 응답 유실 뒤 결과 조회503/410/성공, 다른 업무·새 사건 보존,
늦은 목록·401/403·같은 계정 재로그인·borrowed source 소유를 검사한다.
제품 서버 HTTP/MySQL·실제 사건·두 PC 검사가 아니며 실제 마감 API/DB 완료로 표시하지 않는다.


<details>
<summary>이전 4번 연결 초안 기록 — 현재 PR70 계약보다 우선하지 않음</summary>

# 마감 알림 — 4번 독립 구현과 1·2번 연결 검토

현재 대조 기준: 원본 alpha9c5c07c / LOG0e0990e (2026-10-22).
최초 구현 기준 alpha388d50b / LOG f2d0c43,
[A1~A6 채택](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md#member4-policy-adopted-20261021),
기존4번 N4-08/F4-01과1번 기술 답변. 과거의 정책 제안값을 최신 채택 기준으로 대체해 읽는다.

**팝업/조회 수명/목록과 확인 성공·현재 목록 분리는 구현했다.**
1번 PR64의 MainForm 명시 source 연결·2번 실제 업무 탐색은 원본에 병합됐다.
실제 마감 source/API·기한 사건/초기자료·MySQL 확인 저장은 여전히1·2번 후속이다.
실제 서버에 마감 알림이 작동한다고 안내하지 않는다. 이 문서와 C# port는 기술 초안이며
새 JSON 필드/endpoint/DDL·migration 번호·공통 기능 옵션을 확정하거나 자동 설치하지 않는다.

## 반영한 기준

- 현재 본인 담당의 진행전/진행중/반려완료만 대상. 관리자도 본인 담당만 해당한다.
- 마감 전날09:00 KST부터 마감일 종료까지 보충 팝업. 서버 시각을 쓰며 클라이언트 시각으로 대체하지 않는다.
- 경과 건은 현재 관계/상태를 유지하는 미확인 목록에서만 표시한다.
- 소리 없는 작은 비모달1개.60초 조회, 실패2→4→5분, 성공 시60초 복귀.
- 닫기/실제 표시는 로그인 scope에서 중복 억제하며 메뉴 종료는 scope를 폐기하지 않는다.
- 확인은 source의 실제 서버 결과가 있어야 반영한다. 통신 실패는 미기록으로 추정하지 않는다.
  source가 같은 사건의 원래 공통 W1 key/body를 보존한다. 불명 상태의 다음 클릭은 결과 조회만,
  미기록 확인 뒤 별도 사용자 클릭으로 같은 동작을 명시 재시도한다. 자동 재전송은 없다.
- 현재 목록 뒤 표시 직전 recheck가 필요하다. 그 await 중 owner 숨김/최소화·다른 팝업/모달도 다시 검사한다.
- 현재 원본이 제거/취소/제출/승인/재배정/권한 회수·확인을 반영하면 표시 중 창도 닫는다.
- opaque EventToken은 서버의 실제 마감 변경 사건을 나타내야 한다. 날짜나 row_version만으로 만들지 않는다.
  같은 날짜/본문 수정은 같은 사건, A→B→A는 다른 사건이다.옛 사건 확인이 새 사건으로 전이되지 않는다.
- 과거90일 조회는 채택 기준이지만 실제 history 조회·물리 삭제/감사/백업 정책은 이 초안에 구현하지 않았다.
  폐기/이력 저장소와 현재 권한 계약을1·2번 회신 후 연결해야 한다. 실제 데이터 삭제를 실행하지 않는다.

## 파일과 조립 접점

- DeadlineAlertModels: 순수 정책·in-process port. task_id/row_version은 정규 signed BIGINT 문자열,
  assignment_revision은 signed INTEGER. EventToken은 미확정 기술 이름이며 wire DTO로 사용하지 않는다.
- DeadlineAlertScope: 로그인당1개, 현재 접속 취소/로그아웃/owner 종료에 폐기. source는 빌리며 Dispose하지 않는다.
  source의 Read/Recheck/Confirm/Resolve가 실제 현재 인증·관계·사건과 MySQL 저장을 제공해야 한다.
- DeadlineAlertForm/InboxForm: 작은 알림/미확인 목록. 업무 보기 callback은 현재 recheck 뒤 task_id만 전달한다.
  공통/2번 화면이 다시 현재 조회 권한을 검사해야 하며 이 callback이 조회 권한을 부여하지 않는다.
- DeadlineAlertPanel: 공지 화면은 scope의 읽기 view를 빌린다. 독립 타이머·샘플 업무·로컬 영속 저장은 없다.
- NoticesControl.Deadlines: 기존 가짜 주간 보고서 마감 표시를 제거하고 서버 준비/현재 알림 상태를 표시한다.

1번 PR64 조립 접점(이번4번 보완에서 MainForm은 변경하지 않음):

```csharp
// 실제 인증/사건/확인 어댑터가 합의·검증된 뒤만 설치한다.
await ConnectDeadlineAlertsAsync(session => reviewedSourceFactory(session));
// NoticesControl은 같은 owner chain의 scope를 찾아 빌린다.
// 명시 연결은 page.BindDeadlineAlerts(scope); 메뉴 종료는 scope를 Dispose하지 않는다.
```

## 1·2번에 필요한 기술 회신 — 같은 정책의 재승인 요청 아님

1. **2번:** 같은 보호 TX에서 현재 업무·실효 due_date·본인 배정·상태/취소를 제공할 최소 reader.
   기존 TaskReadSession의 공개 DTO에는 row_version/assignment_revision/현재 관계/서버 시각이 없으므로
   기존 목록만으로 권한·기한 사건을 증명하지 않는다. 원본 업무 DTO를4번이 바꾸지 않았다.
2. **2번:** 실제 기한 변경 이력에서 A→B→A/같은 날짜 저장/본문 수정과 재배정·중간 제출/복귀를 구별할 근거.
   과거 근거가 부족한 자료의 초기 사건 생성/전환 방식도 명시해야 한다. due_revision을 임의 추가하지 않는다.
3. **1번:** 현재 인증/서버 UTC 시각·계정 단위 확인·현재 사건 재검사의 같은TX/잠금 및 W1 행동/최소 결과.
   오래된 사건409/권한 회수/401403503·기능off 뒤 보관 결과 읽기를 기존 공통 계약에 맞춰 대조한다.
4. **4번→1번 DB 제안:** 업무/배정/기한 변경 원본은2번이 유지하고4번은 알림 사건/확인 상태만 별도 보관한다.
   사건/계정 UNIQUE, task/user FK 보존, 현재 활성 무효화 사유와90일 조회를 검토한다.
   실제 컬럼·물리 타입/인덱스·FK 동작·설정명/SQL번호는 회신 후 정식 초안으로 제공한다.
   다른 담당 테이블 ALTER/자동DDL/전체DB reset·SQLite 저장은 없다.
5. **1번:** 로그인당 scope1개·메뉴 수명/공유 소유·기존 중요공지/협업의 양방향 동시 표시 조정·모달 중 보류.
   현재 scope는 이미 보이는 창에 양보하지만 상대 기능이 나중에 표시하는 역방향 통합은 공통 조립 검사다.
6. **1·2번:** 현재 목록/사건 recheck/계정 확인·결과 조회/90일 과거 읽기의 최소 계약과 실제 공유 적용 가능 시점.
   개별 경로/필드/테이블 이름·번호를 이 C# 초안만으로 확정하지 않는다.

3번 파일/결과물·5번 장치/근태의 성공을 선행조건으로 붙이지 않는다. 현재 권한 경계를 유지하고
실제 결과 제출/업무 상태 변경·담당자 배정·파일 열람/제출을 알림이 대신 실행하지 않는다.

## 검증

`dotnet run --project Tests/DeadlineAlerts.Tests -c Release`.
실제 WinForms+명시 source 대역이며 실제 HTTP/DB·물리 두PC 검사가 아니다.
현재 서버 adapter/확인 테이블을 만들지 않았으므로 DB 검사와 실서버 왕복은 미실행이다.
MySQL만 허용하며 SQLite 테스트/대체 저장소는 사용하지 않는다.

API/DDL 합의 후 MySQL에서 같은 사건 중복 확인·동시 PC·변경/옛 확인 경합·보존/복원·물리 구조·
실제 현재 업무 상태 변경 TX와 경합을 추가 검증해야 한다. 과거90일 API/화면·실제 서버 적용도 남아 있다.

## PR61 R61-01~03 보완

- 업무 보기에서도401/403이면 공통 Dispose로 snapshot/팝업/타이머/owner 등록/탐색 callback을 종료한다.
- 조회·표시 전 재검사·업무 보기는 공통 요청 순서를 사용한다. 더 늦게 시작한 요청이나 확인 동작이
  이전 요청을 무효화하며, 예전 응답은 Apply/탐색을 하지 않는다. 오래된503도 최신 화면을 지우지 않는다.
  현재 로그인에 대한401/403은 응답 순서와 무관하게 종료한다. 업무 화면의 현재 권한 검사는 별도다.
- snapshot의 중간 반영은 실패 횟수를 초기화하지 않는다. 표시 전 재검사가 필요하면 그 성공까지 포함한
  조회 한 회 전체가 성공한 뒤에만60초로 복귀한다. Read 성공/Recheck503 반복도2→4→5→5분이다.
- 실제 WinForms 회귀는401/403 각각, 늦은 업무보기/목록 양방향·업무보기끼리 양쪽 완료 순서,
  오래된503,Read503/Recheck503 대비와 정상 복귀를 포함한다. 기존78+새27=105검사다.

[2번 기술 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/61#issuecomment-6311013832)을 수신했다.
현재 원본/잠금과 업무 변경·연장 감사의 재사용 가능성은 확인했지만, 최소 reader·사건/초기 자료 완전성·
현재 확인 writer/API/DTO는 아직 제공 완료가 아니다. task_id 탐색 callback은 이후2번이 제공했고
1번 PR64에서 공통 소비를 연결했다. 별도 recheck가 확인 TX의
원자성을 대신하지 않으며 기존 기술 대기/None·미확정 사건/DB 계약을 그대로 유지한다.

## 2026-10-22 확인 성공과 목록 갱신 분리

[1번 최신 수용 회신](https://github.com/jinkyu1452-cmd/Sang1/pull/1#issuecomment-6312083676)에 따라
`DeadlineConfirmation`의 snapshot을 선택값으로 바꿨다. 이는 C# 내부 port이며 새 wire/endpoint/DDL이 아니다.

- `Confirmed`는 source가 원래 사건·현재 계정·action/target·원래 W1 key/body에 결합된 성공 receipt를
  검증한 결과다. source는 이 성공 증거와 원문을 유지한다. UI 상태만으로 성공을 제조하지 않는다.
- 확인 성공 뒤 전체 목록이 없거나 잘못됐으면 `Confirmed`를 유지하고 snapshot만 버린다.
  **확인 완료 · 목록 재조회 필요**를 표시하고 과거 팝업/현재 facts를 비우며 업무 보기를 막는다.
  빈 정상 목록이나 새 사건의 확인으로 만들지 않는다. 다음 복구는 `ReadAsync` GET이다.
- 성공 사건은 로그인 scope 수명에만 기억해 같은 사건의 다른 화면/중복 클릭이 다시 POST/Resolve하지 않게 한다.
  실제 계정별 DB 저장·W1 보관 결과를 대체하는 저장소가 아니다. 로그아웃/같은 계정 재로그인 뒤 승계하지 않는다.
  새 기한 사건의 token은 옛 성공과 별개다. A→B→A의 실제 사건 ID는 source가 제공해야 한다.
- snapshot은 언제나 권위 있는 **전체 현재 목록**이다. 확인된 동일 사건을 미확인으로 되돌리는 목록이나
  타입·관계·중복이 잘못된 snapshot은 사용하지 않는다. 정상 GET 복구 전에는 stale facts로 업무를 열지 않는다.
- Unknown/NotRecorded/Changed에는 전체 현재 snapshot이 필요하다. 잘못된 상태/응답·503·410/통신 실패는
  미기록으로 추정하지 않는다. 불명 결과는 source의 원래 작업으로 읽기 전용 결과 조회를 한다.
  유효한 미기록 판정 후 별도 사용자 명시 재시도만 가능하다. 자동 새 key/POST는 없다.
- source가 성공 receipt 이후 목록 실패를 예외/Unknown으로 바꾸면 성공을 전달할 수 없다.
  그 경우 `Confirmed`와 snapshot 없음으로 반환해야 한다. 현재401/403은 기존대로 예외/접속 폐기이며
  목록 실패를 핑계로 인증 경계를 통과시키지 않는다. source/W1 action 등록·OFF 독립 reader는1번 소유다.
- 1번의 `POST /api/v1/write-requests/result`는 기존 공통 접점이다. 실제 deadline action/target/최소 receipt는
  아직 후보이며 설치하지 않았다. 키 발급 후24시간과 성공 결과 보관24시간을 구분하고,
  OFF reader 미설치503/401403/410을 not_recorded로 바꾸지 않는다.

검사: 기존105 + 후속57 = **162개**. 실제 WinForms의 팝업/목록·명시 source/HTTP handler와
1번의 실제 `LoginDeadlineNotifications`·캡처 ApiClient.Session을 사용했다. 저장 성공/목록 누락·불량,
GET503/형식 실패와 회복, POST 응답 유실 뒤 결과 조회503/410/성공, 다른 업무·새 사건 보존,
늦은 목록·401/403·같은 계정 재로그인·borrowed source 소유를 검사한다.
제품 서버 HTTP/MySQL·실제 사건·두 PC 검사가 아니며 실제 마감 API/DB 완료로 표시하지 않는다.

## 2026-10-22 실제 연결 검토용 코드 — 기본 설치·공유 배포 전

기준 alpha `cb2114113a7a7dea8709bc15376ce9e4d4d917fd`, LOG
`c5f06533d17a52c3450abb533044931ed9164420` 및 1번 댓글6312083676/2번 댓글6305519634를 대조했다.
A1~A6/B1~B7은 채택 기준으로 유지한다. 이번 코드는 **실행 가능한 연결 초안**이며 새 계약의 팀 승인이나
실제 사건 공급/공유 서버 설치를 뜻하지 않는다. MainForm/AppServices/create_app·기존 migrations는 변경하지 않았다.

- `HttpDeadlineAlertSource.cs`: 실제 캡처 `ApiClient.Session`으로 제안 목록/전체 recheck/확인 POST/원 키 결과 읽기를 소비한다.
  BIGINT 정규 JSON 문자열·assignment_revision signed INTEGER JSON 정수, UTC/DATE·전체 목록/중복/본인 관계를 검사한다.
  검증된 최소 receipt와 원 key/body를 로그인 수명에 보존한다. 불명 결과는 원 결과 조회만, 미기록 후 명시 재시도도 원 key/body를 사용한다.
  확정 성공 후 조회503/불량은 Confirmed+snapshot 없음이다.
  성공 사건 재POST·자동 새키 재시도는 없다. 401/403·410·OFF503을 not_recorded로 바꾸지 않는다.
- `RemoteWork.Server/remotework/features/deadline_alerts/connection.py`: 2번의 실제 `read_own_deadline_facts/fact`를 소비하고,
  1번 `authenticated_transaction`/현재 인증·공통 guard/`perform_once`/독립 결과 reader를 사용한다.
  현재 사건·배정·기한·상태/취소·두 버전 재검사, 계정 확인·감사·receipt를 같은 TX에 보관한다. 업무 row_version은 바꾸지 않는다.
  recheck는 단건을 전체로 위장하지 않고 전체 snapshot이다. 사건 미지/모순/중복은503 전체 실패다.
- `features/deadline_alerts/__init__.py`: `install_review_connection`은 명시적 setup 호출만 가능하다. 기본 기동에 등록되지 않는다.
  `current_event(db, fact)`·`find_event(db, event_id)`·`server_utc(db)` 세 콜백이 필수다. 같은 현재 인증 DB/TX에서 잠금된 사실을
  읽으며 commit/별도 세션/네트워크를 금지한다. 실제 사건 생성·기본 source·날짜/버전 hash·초기 baseline은 없다.
  current_event의 None/불완전은503이다. find_event의 None은 확인된 부재404이고 **자료 불명은 반드시503을 올려야 한다.**
  `server_utc`는 같은 TX에서 얻은 aware UTC datetime을 제공해야 한다. 시험만 MySQL UTC_TIMESTAMP(6)를 명시 주입했다.
- `features/deadline_alerts/confirmation.proposed.mysql.sql`: **번호 미예약·미승인 확인 테이블 초안**이다.
  별도 lightweight SQL 표현만 사용해 common Base.metadata/create_all에도 들어가지 않는다.
  사건/계정 복합 PK·signed BIGINT/INTEGER·정확한 비교·UTC DATETIME(6)·계정/시각 인덱스·배정/사용자 보존형 FK를 검사한다.
  알림 사건 테이블/영구 사건 FK·초기 전환·물리 삭제/백업 보존은 합의 전이며 이 SQL로 해결하지 않는다.
  공유 DB/.env/5000 서버를 변경하거나 SQL을 자동 실행하지 않는다. 검토 후1번이 번호/최종 구조·적용을 맡는다.

### 1·2·4번이 확정할 접점

1. **2번 제공 완료:** 현재 본인 전체 facts/단건·009 취소 사실·현재 잠금. 최신 reader는 그대로 소비한다.
   완전한 사건 공급은 아직 없으므로 현재 기한을 과거 최초 사건으로 소급하지 않는다.
2. **사건 근거/초기 전환 — 기술 협의 필요:** 실제 일반수정/승인 연장의 불변 사건과 배정별 현재 사건을2번이 제공하고1번이
   저장/TX를 검토한다. 본문/동일 날짜 저장은 동일 사건, A→B→A는 별도 사건이라는 채택 기준은 유지한다.
   검증된 구간만 제공/불명503, 또는 범위·기준시각·실행자·백업을 기록한 명시 baseline을 선택해야 한다.
   초안은 어떤 baseline도 실행하지 않았다. 누락/중복 감사·3번 제출 구간을 완전 이력으로 확대하지 않는다.
3. **옛 성공 접근 — 검토 제안:** 원 사건 수신자이면서 현재도 원 업무 own 권한이 있어야 보관 결과를 읽는다.
   상태/기한 변경 뒤 원래 성공은 읽되 새 확인 mutate는 현재 사건·배정·상태/취소·두 버전을 다시 검사한다.
   다른 사람 재배정은 결과 조회도403, 동일 계정 재배정은 새 사건/새 배정 확인을 승계하지 않는다.
   중간 제출→반려의 사건 유지/무효화와 옛 결과 접근의 최종 경계는1·2번 대조 전이다. 시험 통과를 승인으로 쓰지 않는다.
4. **1번:** 같은TX 시각 공급·제안 JSON/endpoint/action/최소 receipt·OFF result reader·W1 보존·DB 최종 검토/적용.
   명시 `writes=False`는 결과 reader만 설치하고 새 키/쓰기 경로를 열지 않는다. W1 키24h와 결과 보관24h는 서로 별개다.
   90일 화면 조회와 물리 보존·감사/백업은 별개이며 과거90일 API/화면은 이번 초안에 없다.
5. **4번:** 실제 사건/DB 계약이 공급되면 검토된 source를 기존 MainForm 명시 factory에 주입하고 같은 서버·실계정·두PC를 검사한다.
   협업 수락이 원 업무/첨부/결과물 권한을 주지 않는 원칙을 유지한다. 1번 MainForm·2번 업무 writer·3번/5번 코드는 수정하지 않았다.

### 직접 실행 검사와 제한

`dotnet run --project Tests/DeadlineConnection.Tests -c Release`: HTTP 대역109검사(콜론 사건ID·원 키/본문 명시 재시도·배정 별칭 차단 포함).
`Tests/DeadlineConnection.Tests/run_mysql_check.py`: 소유한 독립 MySQL8.2 프로세스/새 DB에만 원본002/004/006/009와
확인 SQL 초안을 적용한다. 기존 서비스/DB/.env/SQLite를 사용하지 않는다. 실제 auth/TX/W1·2번 facts와
**명시 합성 사건 콜백**을 결합하며, 그중 실제 loopback HTTP→C# source→WinForms scope→MySQL 저장/재로그인 조회도 수행한다.
MySQL 검사50건 통과(그중 실제 C# scope 검사1건 안에7개 확인). 두 PC 동시 요청은 두 시험 클라이언트로 경합을 확인했으며 물리 두PC와 구분한다.
사건 콜백은 실제 업무 이력 증거가 아니며 실제 운영 서버/공유 DB/두PC 검사는 미실행이다.
기존 마감162·탐색24 검사는 명시 source/HTTP 대역 회귀로 별도 실행한다.

## PR54 후속 대조 — 원래 작업 재시도 수정과 배정 세대 경계

추가 기준: alpha `6760edea42eb53e5d4fb8ef2edc1994c69a053b9`, LOG `b47d3420fb48e1359f6ef4d01152b65c6e848086`.
2번 `94a4035`의 실제 작성부/TX 경계와 `6760ede` 상태·재배정 증거를 정적으로 확인했다.17검사와 추가 원본 검사는2번 보고이며
4번 직접 실행 수에 합산하지 않는다. 현재 facts reader가 제공 완료인 점과 실제 사건/기록 함수가 없는 점을 구분한다.

- **수정 완료:** `not_recorded` 뒤 pending을 지우던 문제를 독립 구 adapter `8f401af`로 재현했다.
  이제 결과 조회는 POST/새키를 실행하지 않는다. 다음 사용자 명시 확인만 보존된 원 key·원 버전·원 배정 본문을 재전송한다.
  제목 변경으로 목록 버전이7→8이 돼도 원 본문7을 유지하고 서버의409 판정을 받는다. 확인503/410/불량은 미기록이 아니다.
- **방향 수용·최종 계약 필요:** 사건 UUID와 배정 세대는 서로 다른 식별이다. 같은 계정 A→B→A의 새 배정은 옛 확인을 승계하지 않는다.
  1번의 확인 UNIQUE(event,user,assignment_revision)와 최소 receipt의 원 배정 식별을4번 소비 요구에 수용한다.
  기존 번호 없는 SQL의 PK(event,user)는 이 최종 계약을 만족하지 못하는 **폐기 대상 이전 초안**이며 적용하면 안 된다.
  이 절이 새 DDL/endpoint/DTO 승인이 아니므로 SQL/서버 wire를 임의 변경·배포하지 않았다.
- **보수 방어 제공:** 현재 port의 표시/성공/불명 키와 행동 입력은 raw EventToken 기반이다. 검토된 배정별 행동/receipt 전달이
  제공되기 전까지 같은 raw token이 다른 task/assignment에 연결되면 source와 scope 모두 전체 목록을 거절하고 후보/팝업을 비운다.
  날짜/배정 hash나 가짜 사건 suffix로 대신하지 않는다. 이는 새 배정 정상 알림의 완성 구현이 아니라 **확인 승계 방지 차단**이다.
  기존 마감162+새 배정 차단4=166 검사. 새 source와1번 CapturedSource의 배정 식별 전달/재조회/결과 조회 접점을 확정해야 실제 지원할 수 있다.
- **4번 소비 의견:** 같은 배정·같은 실제 기한 사건에서 제출/승인/취소 시 활성 후보를 제거하되 기록을 보존하고, 같은 배정의
  반려→재작업 복귀에서는 기존 확인을 유지하는1번 안에 추가 이견 없다. 원 업무 현재 own 권한·원 actor·TTL을 만족할 때만
  옛 최소 성공을 읽고 새 사건/새 배정으로 승격하지 않는 방향에도 이견 없다. **4번 의견이며 공동/팀장 확정 전**임을 유지한다.
  관리자 변경·취소를 assignment_revision만으로 추정하지 않고 현재 관계/상태/취소/버전을 각각 재검사한다.
- **다음 제공 분담:**1번의 실제 같은-session 기록 함수(입출력·미설치/실패/중복/잠금), 배정별 현재 binding/행동/성공 receipt와
  최종 물리 SQL 검토 →2번의 검증된 mutation 안 연결 →4번의 generation-aware source/UI 연결. 원본 업무 writer·공통 코드는 수정하지 않았다.
  신규 최초 사건/기존 자료 baseline 범위·시각·실행자·이유·백업/unknown은 공동 결정이며 어떤 초기 전환도 실행하지 않았다.

## PR70 실제 사건 reader 소비 — 2026-10-22 추가

1번14:52 회신의 **PR70 Draft·미병합** 공급을 수신했다. 구현 `30424a3d5abb6a18f76fb2649031cc88c563835e`,
고정 제공 head `6302dbe89ad36a0a1833c0fb909fbe0dafde5652`의 `remotework/deadline_events.py`와
`docs/INTEGRATION.md`를 대조했다. 최신2번 기준 alpha `641bc718ca3adaff8e220fedebe63d741d1d11fd`, LOG
`2f98bbd`의 상태/배정 증거도 수신하며 같은 자료를 미제공으로 재요청하지 않는다.

- **4번 소비 코드 제공:** `RemoteWork.Server/remotework/features/deadline_alerts/journal_bridge.py`의
  `build_review_journal_callbacks(read_current=..., read_event=...)`는 실제1번 함수들을 명시 입력받는다.
  같은 인증TX의2번 own facts를 재검사한 뒤1번 현재 사건을 읽고 기한·원본 버전을 대조한다.
  사건 자체에 recipient/assignment를 넣지 않고 현재 facts와 결합한 투영만 기존 검토 port로 전달한다.
  재배정만으로 새 기한 UUID를 발급하지 않는다. 제공받은1번 공통 모듈은 복제/수정/자동 설치하지 않는다.
- **보관 성공 접근:** 본인 확인 행에서 원 업무/배정/기한을 비잠금 탐색 →현재 own 업무/배정 잠금·재인가
  →1번 보관 사건 reader →확인 행의 최종 잠금 순서로 소비한다. 현재 own 권한을 확인하기 전 보관 사건을 노출하지 않는다.
  다른 본인 업무의 초기 사건이 불명이어도 이미 보관한 원 성공은 읽을 수 있다. 이는 공통 guard를 따르는 변경의 범위다.
- **부족한 원 결합은 차단:** 확인 행이 없는 옛 미확정 작업은 원 actor/assignment/W1 결합이 아직 공급되지 않았다.
  현재 목록에서 UUID를 못 찾았다고 전역 부재404/미기록으로 바꾸지 않고 `DEADLINE_EVENT_BINDING_REQUIRED`503이다.
  날짜·현재 배정·감사PK·행버전으로 과거 수신자를 추정하지 않는다. 이 원 작업 결합은1번 최종 API/보관 계약에 필요하다.
- **독립 검증13 PASS:** 원본 PR70 함수/번호 없는 SQL을 Git blob 그대로 외부 시험 폴더로 추출·고정SHA/hash 대조한 뒤
  독립 MySQL에만 적용했다. 새 fixture 업무의 실제 `record_change`/`read_current`/`read_event`,2번 actual facts,
  auth/TX/W1/확인 초안을 함께 사용한다. A→B→A UUID·순서/미승계, 같은날짜·제목 비변경, head 없는 기존자료503/무baseline,
  전체 목록 불완전503, 옛 확인의 현재 own 회수403, 다른 초기 불명 업무와 독립된 원 성공 조회를 검사한다.
  이13건 중1건은 실제 loopback HTTP→C# source→WinForms scope→MySQL 저장/재로그인7확인이다.
  **새13건에서는 사건 공급도 실제 PR70 journal**이다. 앞선50건의 합성 사건 검사와 구분하며 수치를 합산하지 않는다.
  시험 업무/배정과 기록 함수의 호출은 fixture이며2번 제품 writer의 정식 hook이나 공유 배포 성공은 아니다.
- **초기 검사 오류:** 공급 SQL 주석의 세미콜론을 시험 splitter가 문장으로 나눠13개 setup 오류가 났다.
  시험에서 주석을 제외한 실행만 바로잡았으며1번 원 SQL/함수는 바꾸지 않았다. 재실행13건 통과·DB/프로세스/runtime 정리 확인.

재현은 C# 시험 프로젝트를 먼저 Release 빌드하고, `Tests/DeadlineConnection.Tests/run_mysql_check.py`
`--journal-provider-root <고정 PR70 deadline_events.py/SQL/manifest.json가 있는 절대경로>`로 수행한다.
기본 기동/공유 DB에는 어떤 provider/DDL/seed도 추가하지 않는다. 최종 확인 키3열/배정별 wire·receipt·owner wrapper,
미확정 원 작업 결합,1번 번호/물리 검토·2번 실제 writer 연결, baseline/과거90일·운영 백업/두PC는 여전히 별도다.

</details>
