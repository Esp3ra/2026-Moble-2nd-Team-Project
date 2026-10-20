# 3번 결과물 기능 — 1번 공통 연결 안내

2026-10-17. 이 문서는 3번의 LOG `3번/README.md` 요청에 대한 **공통 지원 사용법**이다. Results 폼·제출/검토 API·첨부 저장·업무 이력 조회를 구현한 문서가 아니다. 제품 제출 모델/API/상태와 검토 권한은 3번·2번·팀장이 확인하고 1번이 DB/FK·공통 접점을 검토한다.

## 로그인 정보와 화면 수명

실제 로그인에서는 `RemoteWork.WinForms/Infrastructure/AppServices.cs`의 `AppServices.Api` 하나를 재사용한다. 현재 사용자 타입은 `ApiModels.cs`의 `UserInfo`이며 `Id`는 정수, `DisplayName`은 표시명, `Role`은 `employee` 또는 `admin`이다. 이름으로 ID를 찾거나 미리보기 사용자를 로그인 사용자로 사용하지 않는다.

`ApiClient.CaptureSession()`은 로그인된 접속에 묶인 `ApiClient.Session`을 반환한다. 미로그인에서는 `InvalidOperationException`이다. 이 객체는 Bearer 원문을 공개하지 않으며, 캡처한 사용자 표시 정보·접속 취소 토큰·현재 접속 확인과 GET/POST/PATCH를 제공한다. **이 객체의 요청은 캡처한 접속을 사용한다.** 화면이 남은 채 다른 사람이 로그인해도 새 사람의 접속으로 이전 쓰기를 보내지 않는다.

```csharp
using RemoteWork.WinForms.Infrastructure;

// 실제 로그인 화면에서 사용. Designer/preview 생성자에서는 캡처하지 않는다.
ApiClient.Session session = AppServices.Api.CaptureSession();
int userId = session.User.Id;
string displayName = session.User.DisplayName;
bool showAdminMenu = session.User.Role == "admin";

// 다음 호출은 이미 있는 /auth/me 연결 예시이며 제출 API가 아니다.
using var screenClosed = new CancellationTokenSource();
IdentityResult identity = await session.GetAsync<IdentityResult>(
    "auth/me", screenClosed.Token);
session.ThrowIfChanged();
// 실제 화면은 이 시점에 IsDisposed, 로컬 요청 세대/선택 ID도 확인한 뒤 표시한다.
```

각 폼/컨트롤은 `CancellationTokenSource`와 요청 세대/선택 ID를 소유하고, 닫기/Dispose 때 Cancel·Dispose한다. 화면 종료로 공유 `AppServices.Api`를 Dispose하거나 ClearIdentity하지 않는다. 서버 반영 여부를 모르는 쓰기 취소는 ‘저장 실패 확정’으로 표시하지 않는다.

접속 전환·동일 계정 재로그인·로그아웃·현재401·ClearIdentity는 기존 접속의 `session.Cancellation`을 취소한다. 화면은 그 토큰에 정리 처리를 등록해 목록·상세·선택·제출 대기 파일·요청 키를 지운다. 등록은 화면 종료 때 Dispose한다. 취소 콜백은 작업 스레드에서 호출될 수 있으므로 UI 컨트롤을 직접 만지거나 동기 Invoke하지 말고 캡처한 UI `SynchronizationContext.Post`로 전달한다. 등록 직후에도 이미 취소됐는지 확인하며, 콜백에서는 예외를 내보내지 않는다.

```csharp
// UI 스레드에서, 실제 화면의 수명에 맞춰 필드로 보관할 값의 사용 예시.
SynchronizationContext ui = SynchronizationContext.Current
    ?? throw new InvalidOperationException("UI 스레드에서 연결하세요.");
CancellationTokenRegistration registration = session.Cancellation.Register(() =>
    ui.Post(_ =>
    {
        // 3번 폼에서 IsDisposed를 확인한 후 화면 자료와 진행 요청을 정리한다.
        // 이 예시는 실제 Results 컨트롤 이름/이벤트를 새로 정의하지 않는다.
    }, null));
// 폼 Dispose에서는 registration.Dispose(); screenClosed.Cancel(); screenClosed.Dispose();
```

`Session.User`는 캡처 당시 표시용 스냅샷이다. 주기적 `/auth/me`에서 역할/표시명이 바뀌면 `ApiClient.CurrentUser`를 확인하고 메뉴 표시를 갱신한다. 스냅샷이나 메뉴 활성화는 서버 권한 증거가 아니다. 늦은 응답/파일 선택 결과에는 `session.IsCurrent`와 화면/로컬 요청 세대를 다시 확인한다. 단순 사용자 ID 비교는 같은 계정 재로그인을 구분하지 못한다.

기존 구현 예시는 `Shell/MainForm.cs`의 인증 만료·로그아웃·메뉴 전환 Dispose와 `Features/Tasks/TasksControl.cs`의 화면 요청 세대/선택/취소 처리를 참조한다. 이번 변경으로 Results 폼에 코드를 주입하거나 화면의 예시 데이터를 실제 DB 결과로 바꾸지는 않았다.

## 직원 제출을 위한 공통 저장 도구

서버 `remotework/auth.py`의 `authenticated_transaction()`을 사용한다. guard→현재 사용자→현재 접속을 잠금/재확인하고 같은 SQLAlchemy 세션을 전달한다. 3번 서비스는 그 안에서 현재 업무/배정/명시적 검토 관계를 잠그고 자신의 행동 권한을 검사한다. 관리자 전용 `admin_transaction()`이나 일반 `transaction()`만으로 직원 제출 권한을 대신하지 않는다.

공통 `write_contract.ExpectedVersions`는 엄격한 버전 입력과 충돌 검사를, `common.audit()`는 같은 세션의 성공 감사를 제공한다. 버전 검사·업무 상태 변경·제출본/차수/판단·성공 감사·receipt는 하나의 트랜잭션에 조립한다. 업무 상태만 나중에 별도 HTTP로 변경하지 않는다. 파일 I/O는 DB 트랜잭션에 넣지 않으며, 파일 저장 성공과 DB 성공은 자동으로 원자가 되지 않는다.

## 기능별 중복 요청 계약 등록

기존 [공통 쓰기 계약](WRITE_SUPPORT.md)의 기본 off, 현재 권한 재검사, 성공 결과24시간, 만료 키 재실행 금지, 자동 재시도 없음은 그대로다. `write_requests.register_write_action(app, action=..., authorize=..., result_fields=...)`을 추가했다. **3번의 실제 행동명/권한/최소 결과가 합의된 뒤 앱 초기화 때 등록하는 연결 도구**다. 현재 제품 제출 행동은 등록하지 않았다.

- 행동명은 최대80자의 `namespace.action` 소문자 형식이다. 기존 task 행동·이미 등록한 행동을 대체할 수 없고 첫 HTTP 요청 이후 등록할 수 없다. 다른 Flask 앱으로 등록이 새지 않는다.
- `authorize(db, target)`는 현재 대상/관계를 잠금 확인한 후 정확히 True를 반환한다. 키 발급·최초 실행·재응답·결과 조회가 같은 등록 함수를 사용한다. 서비스에서 더 느슨한 함수로 바꿔 호출하면 실패한다. 등록 자체는 접근 허가가 아니다.
- `result_fields`는 허용 결과 필드의 명시적 목록이다. 필수 필드/타입/식별자 범위는 행동 서비스가 별도로 검증한다. 본문·파일·토큰·사유·개인정보를 목록에 넣지 않는다. 미선언 필드가 반환되면 업무 변경·감사·receipt가 함께 rollback된다.
- 미등록 행동은400, 거절된 현재 권한은403, 같은 키/다른 본문은409다. 등록했어도 공통 opt-in이 꺼져 있으면 키/결과 경로는 생기지 않는다. 등록은 DDL이나 기능 라우트를 생성하지 않는다.

아래는 **테스트 전용 계약**의 조립 예시다. 실제 제출 API/검토 권한/제출 ID 계약으로 채택하지 않는다.

```python
from remotework.write_requests import register_write_action

# feature_authorize는 소유 기능이 제공할 현재 대상 권한 검사 함수.
register_write_action(app, action="submission.test_completed",
    authorize=feature_authorize,
    result_fields={"task_id", "submission_id", "row_version"})
```

행동 서비스는 등록한 함수 자체를 `perform_once(..., authorize=feature_authorize, mutate=...)`에 전달한다. 버전·상태 검사는 첫 mutate 안에 둔다. 같은 키 재응답 전에 이전 expected 버전을 검사하면 정상 재응답을 막을 수 있다. 결과 조회는 공통 `/write-requests/result`를 사용한다. C#에서는 캡처한 `session.PostIdempotentAsync<T>`/`PatchIdempotentAsync<T>`로 같은 작업 키를 전달한다. 아직 정의되지 않은 제출 API 경로를 임의 호출하지 않는다.

## 3번이 제공하면 1번이 검토·연결할 자료

| 기존 항목 | 3번/관련 담당 자료 | 1번 처리와 연결 시점 |
|---|---|---|
| W3-02·04 | 제출/판단 모델·행동별 요청/응답·제출/업무/배정 FK·잠금 순서·버전 증가·최초/재제출 회차 규칙 | 2번 업무 접점 확인 후 DB 타입/FK·공통 도구·receipt 계약 검토. 새 migration 번호/공유 DDL은 검토 후 결정 |
| W3-05 | 검토자 지정/변경 주체·자기 판단 거절·관리자 본인 제출·부재/비활성·과거 제출에 대한 관계 변경 영향·최소 조회 범위 | 사용자 승인된 ‘협의 제안’과 공동 회신 대조 후 서버 권한 연결. task_managers를 승인권자로 전용하거나 전체 GET 권한을 풀지 않음 |
| 첨부 | 저장소 선택·크기/개수/확장자·업로드 소유/만료·다운로드 권한·제출 연결·중간 실패/정리/복구·보존 정책 | 파일은3번 구현, 공통 설정/인증·DB 메타데이터·권한 접점은1번 검토. 실제 서버 경로나 공유 파일 저장은 아직 없음 |
| 업무 이력 | 화면 필요 필드·업무/배정/제출 참조·이벤트 기록 주체·정렬/필터·읽기 범위·보존 | 2·3번 행동과 같은 트랜잭션 기록안 검토. AuditEvent 원문을 화면 API로 그대로 노출하지 않음 |
| W3-01·03·06·07 | 상태별 수정/재배정/수락·반려 재제출·마감/연장/제출 경합·필수 검토 연결 | 2·3번 공동 행동 정책 확인 뒤 공통 검증 시나리오 조립. 자동 결과 제출 제외 유지 |

검토자 협의 제안: **현재 업무 지정 관리자가 활성 관리자1명을 명시적으로 지정하며, 제출자는 자기 제출물을 승인·반려하지 않고 관리자 본인 제출도 다른 지정 검토자에게 받는다.** 사용자는 이 안으로 협의를 준비하도록 승인했다. 공동 회신이나 제품 권한 구현 승인은 아니다. 지정자가 현재 admin 역할도 필요한지, 관리자 ‘상위’ 관계를 어떻게 확인하는지, 변경/과거 제출·부재 처리는 공동 결정 전이다.

다음 단계는 3번이 모델/API/파일안과 위 미정 항목을 기존 W3 번호로 회신하는 것이다. 1번은 그 회신을 확인한 뒤 필요한 공통 구현·MySQL 검증을 이어간다. 일정·담당 회신 없이 ‘모두 연결 완료’로 표시하지 않는다.
