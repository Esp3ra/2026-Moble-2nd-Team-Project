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
