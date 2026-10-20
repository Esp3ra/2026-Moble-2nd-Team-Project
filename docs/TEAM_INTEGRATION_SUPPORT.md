# 팀원 병행 개발용 공통 지원

1번 제공 · 2026-10-19 · **독립 준비 도구**. 기존 로그인/DB/HTTP 규약은 그대로 사용한다.
팀원 기능을 완성하기 전 준비할 수 있는 환경 점검·가상 메시지 대조·화면 요청 취소 도구다.
새 본인 확인 API/DB·MainForm/실물 센서·출퇴근 연결을 완료한 자료는 아니다.

## 2·3·4번: 서버 설정과 테이블 준비를 읽기만 하기

기존 [서버 실행 안내](SETUP.md)의 Python 가상환경·비공개 `.env`를 준비한 뒤
`RemoteWork.Server`에서 실행한다. 이 도구는 flags를 켜거나 서버/계정/테이블을 만들지 않는다.

```powershell
# DB에 접속하지 않고 이 명령 프로세스의 경로 등록만 확인
.\.venv\Scripts\python.exe -m remotework.integration_readiness

# 명시적으로 선택한 기능의 제한된 MySQL 선행 조건 확인
.\.venv\Scripts\python.exe -m remotework.integration_readiness --database --features common tasks writes
.\.venv\Scripts\python.exe -m remotework.integration_readiness --database --features common notices chat
```

| 결과 code | 의미/다음 확인 |
|---|---|
| `DATABASE_NOT_CHECKED` | 구성만 확인. DB/실제 서버 준비 완료가 아님. 종료코드0도 DB 성공이 아님 |
| `FEATURE_ROUTES_REQUIRED` | 선택한 기능의 경로가 이 프로세스에 미등록. 기존 담당의 opt-in/설정을 확인하며 DB는 조회하지 않음 |
| `MYSQL_REQUIRED` | MySQL 전용 점검. SQLite DEMO를 실제 통합 환경으로 판단하지 않음 |
| `SCHEMA_PREPARATION_REQUIRED` | 누락 테이블/필수 열 또는 공통version1 기록 확인 필요. 자동 DDL/보정 없음 |
| `LOCAL_PREREQUISITES_OK` | 이 프로세스의 경로·선택된 테이블/일부 열·version1 기록만 확인. 실제HTTP/계정별 권한/전체DDL/두PC는 별도 |
| `DATABASE_UNAVAILABLE` / `CONFIGURATION_ERROR` | DB/설정 준비 확인 필요. 실제 URL·비밀번호·driver 원문 오류는 출력하지 않음 |

점검할 DB는 기존 설정의 `DATABASE_URL`/MySQL 설정을 그대로 따른다. 서버 주소를 출력하거나 원격 장비를 스캔하지 않는다.
`--database`일 때 MySQL `SET TRANSACTION READ ONLY` 뒤 고정된 테이블·열만 조회하고 연결 종료 시 rollback한다.
전체 PK/FK/타입·DDL 호환/현재 역할·데이터 관계·backup/restore까지 보증하지 않는다.
2번의 기존 `python -m remotework.features.tasks.readiness`는 **업무 참조 누락까지 확인하는 별도 명령**이며 그대로 사용한다.

## 5번: 같은 가상 지문 메시지를 대조하기

- [가상 fixture](../Tests/Fixtures/device_protocol_draft.json): 기존 LOG/1번의 두 공개 벡터를 실행 가능한 JSON으로 옮겼다.
  `public_test_key_hex`는 누구나 아는 시험값이며 제품/장치에 설치하면 안 된다.
- Python 검사 도구: [`device_protocol_draft.py`](../RemoteWork.Server/remotework/device_protocol_draft.py).
  서버/Pi 측 개발 검사 코드다. `sign_payload`/`verify_payload`는 원문·MAC/DTO의 초안 검사를 한다.
  PC 중계기는 키를 보유하거나 MAC을 만들지 않고 티켓/결과 바이트를 변경 없이 전달한다.
- `Tests/IntegrationSupport.Tests`의 C# MAC 비교는 **테스트 전용 독립 대조기**다.
  제품 PC에 키 검증/서명 기능을 설치한 것이 아니다.

검사 대상은 canonical base64url, UTF-8, 중복/미지 필드·NaN/잘못된 타입, 방향/장치/키ID·MAC 변조,
기존 request/result 필드다. v1/길이 제한·UTC초 단위·result outcome(`matched/no_match/sensor_error`)는
**개발 초안**이며5번과 최종 대조해야 한다. matched의 슬롯 정수 상한은 파서의 방어 한도일 뿐 실제 센서 용량이 아니다.

**유효 MAC = 현재 회원의 본인 확인 성공이 아니다.** 실제 서버는 현재 로그인/actor·키 활성·매핑revision·실제슬롯,
서버 시각 만료·nonce/단일소비·동일결과 재응답/다른결과충돌·감사/rollback을 추가 구현해야 한다.
이 도구는 과거 가상 티켓도 바이트가 맞으면 통과시키므로 인증 API에 그대로 연결하면 안 된다.
새 route/테이블/마이그레이션 번호·스키마 버전·장치 설정을 등록하지 않았다.

저장소 루트에서 아래 명령으로 개발자가 반복 실행한다.

```powershell
cd RemoteWork.Server
.\.venv\Scripts\python.exe -m pytest -q tests/test_device_protocol_draft.py
cd ..
dotnet run --project Tests/IntegrationSupport.Tests/IntegrationSupport.Tests.csproj -c Release
```

## 전원: 이전 화면/선택/로그인의 응답을 새 화면에 반영하지 않기

[`FeatureUiScope`](../RemoteWork.WinForms/Infrastructure/FeatureUiScope.cs)는 선택적으로 쓸 수 있는 새 도구다.
기존 화면은 수정하지 않았다. 아래는 **채택 전 검토용 사용 예시**이며 자동으로 기능에 주입되지 않는다.

```csharp
// 실제 로그인 후, UI 스레드의 화면 생성 시 한 번.
var session = AppServices.Api.CaptureSession();
var ui = SynchronizationContext.Current
    ?? throw new InvalidOperationException("UI 스레드에서 연결하세요.");
var updates = new FeatureUiScope(session,
    callback => ui.Post(_ => callback(), null),
    () => !IsDisposed && !Disposing);

// 새 목록/상세/근무 세션을 선택할 때 이전 작업을 취소.
var operation = updates.BeginOperation();
try
{
    var data = await session.GetAsync<MyDto>("feature/path", operation.Cancellation);
    updates.TryPost(operation, () => Render(data));
}
catch (OperationCanceledException) { /* 이전 선택/접속/화면의 결과를 버림 */ }

// 화면 Dispose에서 updates.Dispose(). 센서 서비스는 별도 소유자가 정리.
```

전달받은 `Operation`의 취소 토큰을 모든 관련 요청에 전달한다. 새 선택뿐 아니라 같은 화면에서 새 근무 세션으로 바뀌어도
`BeginOperation()`을 호출한다. UI 큐에 넣기 전과 실제 콜백 실행 시 operation/generation·현재 `CaptureSession`을 다시 확인한다.
로그아웃·동일계정 재로그인·401·Dispose·다른 scope·폐기된 view의 이전 응답은 반영하지 않는다.
정상 `auth/me` 갱신은 로그인 변경이 아니므로 작업을 취소하지 않는다.

`TryPost`의 true는 **큐 등록**이며 실제 화면 반영 성공이 아니다. UI 선택 변경/Dispose와 큐 실행은 UI 스레드에서 처리한다.
dispatcher는 UI 큐에 게시하고 view 생존 predicate는 빠르고 예외 없이 동작해야 한다.
취소 콜백은 예외를 던지거나 UI 동기 Invoke/`.Wait()`/`.Result`를 사용하면 안 된다.
이 도구는 서버 권한·같은 로그인 중 강등/부서 변경을 판정하지 않는다. 실제 API의 현재 관계 검사와 오류 처리는 각 담당 책임이다.
서비스·카메라/파이프 시작/중지·출근 성공·근무 복구·MainForm 최종 소유 수명도 대신 구현하지 않는다.

## 담당과 다음 연결

| 항목 | 준비 완료 | 이후 공동 연결 |
|---|---|---|
| 공통 환경 | 읽기 점검 명령·범위/오류 안내 | 운영 환경/서버 주소·계정 비공개 전달·정식DDL/복구·실제 두PC |
| 본인 확인 | 가상 바이트/DTO fixture·엄격 검사·언어 간 대조 | 1번: 매핑/challenge/완료 서버·DB·키. 5번: Pi/PC 중계·실물 슬롯/서비스. 공동 정책은 별도 |
| 화면 수명 | 독립 FeatureUiScope·대역 회귀 | 5번 감지 인터페이스 수신 후1번 MainForm 연결. 기존 MainForm/다른 팀원 소스는 이번 미수정 |

규약/책임 근거는 원본 LOG/1번 통합 질문서·LOG/2번 공통규약·LOG/5번 PR36/39 회신이다.
최신 팀장 지침에 따라 협의 회신은 기존 ID별 **한 묶음의 텍스트**로 전달하고 팀장이 기존 LOG에 취합한다.
이 문서는 alpha 개발 도구 사용법이며 LOG/날짜 일지나 새 공동 정책 확정본이 아니다.
