# 관리자 계정 생성 → 새 지문 등록 연결안

1번 → 5번·팀장 · 2026-10-20 · C10 / D5-01.a·b, D5-02.a·b, D5-05.h

요청: [5번의 PR43 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6279730749).
최신 [LOG/2번 공통규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/공통규약.md)의 담당 경계와 기존 User/Bearer·HTTP12초·기본off 기준을 유지한다.
**이번 실제 코드는 계정 생성 응답의 ID 유지와 화면/접속 수명 보호다. 아래 서버·Pi 계약은 회신용 제안이며, 실제 API/테이블/등록 버튼은 아직 없다.**

2026-10-20 AS608 후속: [단일 슬롯·별도 enroll 원문/벡터·시간/복구 회신](ENROLLMENT_WIRE_DRAFT.md).
5번의 로컬 코어/실물 읽기 보고를 수신해 구체 대조안을 제공한다. API/DDL/버튼·실물 등록 구현이나 공동 합의 완료로 확대하지 않는다.

## 수용하는 흐름과 실제 제공물

관리자 기본 계정 생성 → 응답 `User.Id` 고정 → 허용 장치 선택 → 명시적 새 지문 등록 → 서버가 인증된 결과 검증·매핑 활성화 → 등록 완료 표시의 방향을 수용한다.
기존 템플릿 전체 삭제·이름표 연결·이미 등록된 슬롯 덮어쓰기는 이 흐름에 포함하지 않는다.
계정 생성 성공과 지문 등록 성공은 별개다. 지문 실패 때문에 계정을 자동 삭제하거나 `User.is_active`를 바꾸지 않는다.
미등록 계정의 로그인/출근 허용 범위는 팀장·1·5번의 협의사항이다. 기존 업무 조회/수락/제출에 지문 조건을 추가하지 않았다.

실제 변경 파일은 `Shell/UserManagementForm.cs`와 `Infrastructure/CreatedUserEnrollmentTarget.cs`다.
POST 응답의 `User.Id`를 편집 화면에 유지한다. 새 회원이 현재 목록 페이지에 없으면 첫 행으로 대체하지 않는다.
목록 갱신 실패도 이미 성공한 계정 생성과 구분한다. 응답 자체가 유실되면 새 ID를 추측하거나 생성 POST를 자동 재전송하지 않고 목록 확인을 안내한다.
기존 계정 수정은 새 지문 등록 대상으로 자동 승격하지 않는다.

새 `EnrollmentTarget`/`EnrollmentTargetChanged`는 5번 전용 중계기를 나중에 주입하기 위한 **빌려 쓰는 화면 접점**이다.
관리자 actor는 `target.AdministratorSession.User.Id`, 직원은 `target.UserId`다. 둘을 바꾸어 쓰지 않는다.
`User`는 표시용 스냅샷이며 현재 서버 권한/활성·등록 가능 여부를 보증하지 않는다.

```csharp
// 향후 장치 선택 + 명시적 시작 버튼에서 사용. 현재 제품에 이 버튼/서비스는 없음.
var target = form.EnrollmentTarget;
if (target is null) return;
target.ThrowIfChanged();
// 5번 전용 릴레이에 target + 서버가 허용한 device_id + target.Cancellation 전달.
// 서버 HTTP는 target.AdministratorSession 사용. Bearer/키를 Pi에 전달하지 않음.
// UI에 돌아온 뒤에도 target.IsCurrent와 현재 선택 device/generation을 재확인.
```

새 계정 입력·다른 회원 선택·새 목록에서 대상 재선택·창 닫기/Dispose·로그아웃/401·같은 계정 재로그인·서버 전환은 이전 접점을 무효화한다.
계정 생성 직후의 동일 회원 목록 갱신만 이 접점을 유지한다. 장치 선택/작업 세대는 향후 중계 연결 때 별도로 고정해야 한다.
접점 취소는 늦은 UI 반영을 막는 신호다. 서버 취소·센서 중단·물리 템플릿 삭제를 보장하지 않는다.
창을 다시 열거나 앱을 재시작한 뒤의 미완료 등록 복구는 아래 서버 조회 계약과 별도 복구 화면이 필요하다.

## 담당·제안 API

1번: 사용자 화면 접점·서버 발급/완료/조회·현재 관리자/접속 검사·회원–장치–슬롯 매핑/감사·모델/DDL/장치 키 관리.
5번: C# 전용 비동기 릴레이/진행·취소 표시·장치 선택 상태, Pi의 센서 직렬화·빈 슬롯 확인·두 번 채취/같은 지문 비교·인증 결과/재시작 복구.
팀장·1·5번: 허용 장치·등록 승인·교체/삭제·부분 실패·등록 안 된 계정 권한·보관 정책 결정.
UserManagementForm의 실제 등록 버튼과 연결 소유권은 1번, 중계 서비스/DTO 구현은 5번이 대조한 뒤 주입한다. 기존 MainForm 근무 서비스와 독립한다.

**아래 경로/DTO/오류/상태는 새 제안이며 미등록이다. 이름·TTL·DDL을 확정했다고 읽지 않는다.**
경로 접두사는 `/api/v1`, 기존 공통 `{ok,data,error,request_id}`를 사용한다.

| 메서드·경로 | 입력/결과 제안 | 검사·효과 |
|---|---|---|
| GET `/device-enrollments/eligible-devices?target_user_id=...` | 직원 INTEGER JSON정수 → 현재 허용된 장치 목록 `device_id`, 표시명, 등록 가능 상태 | 현재 활성 관리자·직원과 장치의 허용 관계. 관리자 role만으로 모든 장치 접근 허용하지 않음. 정책 미설정은 거절 |
| POST `/device-enrollments` | `client_request_id` 소문자 UUID, `target_user_id` INTEGER JSON정수, `device_id` UUID → HTTP201, `enrollment_id`, `operation_revision`, `state`, `expires_at`, `ticket` | actor/session은 서버의 현재 인증에서 결정. 클라이언트가 actor/session ID를 지정하지 않음. 새 사용자 생성이 아니라 별도 등록 작업 발급 |
| GET `/device-enrollments/{enrollment_id}` | HTTP200, 아래 최소 결과 | 현재 관리자/접속·허용 관계 재검사. 조회로 재스캔/슬롯 할당/매핑 활성화하지 않음 |
| GET `/device-enrollments/by-request/{client_request_id}` | HTTP200, 같은 접속의 요청에 대한 최소 결과 | issue 응답 유실 복구. 다른 접속으로 자동 이관하거나 새 요청을 만들지 않음 |
| POST `/device-enrollments/{enrollment_id}/complete` | `operation_revision`, `result_envelope` 원문 → HTTP200, 최소 결과 | 서버 인증·현재 권한·작업/대상/장치/키·예약슬롯·만료·replay 검사 후 DB 매핑/감사/완료 원자 저장 |
| POST `/device-enrollments/{enrollment_id}/cancel` | `operation_revision` → HTTP200, 최소 결과 | 서버 작업의 새 완료를 막음. 장치 등록 중단/물리 삭제 완료 응답으로 사용하지 않음 |

최소 결과: `enrollment_id` UUID, `target_user_id` INTEGER JSON정수, `device_id` UUID, `operation_revision` 정규 양의 BIGINT 문자열,
`state` 고정 enum, `mapping_revision` 정규 양의 BIGINT 문자열 또는 null, `recovery_required` JSON bool, `updated_at` 서버 UTC.
클라이언트가 ID를 양의 정수로 추측하거나 BIGINT를 double로 변환하지 않는다. `slot`은 일반 목록/감사 응답에서 제외하고 필요한 관리 복구 화면에서만 제한 공개한다.
최초 issue TTL·완료 결과 조회 창·Pi 작업/중복 캐시 기간·poll 간격은 **5번 최악 채취/전송 시간 측정 후 확정**한다. 공통 HTTP12초를 늘리지 않는다.
같은 `client_request_id`와 동일 정규 본문은 같은 작업, 다른 본문은409로 제안한다. 위 by-request 조회도 최종 경로명/DTO 대조 대상이다.
기존 업무용 action/receipt에 등록 작업을 끼워 넣거나 업무의24시간 규칙을 자동 적용하지 않는다.
독립 `REMOTEWORK_ENABLE_DEVICE_ENROLLMENT_DRAFT=0`와 전용 명시 초기화 명령을 제안한다. **현재 이 설정/명령은 구현되지 않았고 등록 경로도 없다.** 기존 업무/공지/채팅 flag로 장치 기능을 켜지 않는다.

| HTTP / 새 error.code 제안 | 의미 |
|---|---|
| 400 / `ENROLLMENT_INVALID_INPUT` | UUID·ID·버전·필드/인코딩·장치 슬롯 범위 형식 오류 |
| 401 / 기존 인증 오류 | 현재 관리자 접속 없음/만료 |
| 403 / `ENROLLMENT_FORBIDDEN` | 관리자 권한 소실·허용 장치/대상 관계 없음 |
| 404 / `ENROLLMENT_NOT_FOUND` | 조회 가능한 대상 없음. 타인 작업/장치 존재 과다 노출 방지 |
| 409 / `ENROLLMENT_CONFLICT` | 다른 본문/결과 재사용·버전·슬롯 소유권 충돌·장치 busy |
| 410 / `ENROLLMENT_EXPIRED` | 미완료 작업 만료. 새 물리 스캔 자동 실행 금지 |
| 503 / `ENROLLMENT_NOT_READY` | 기능/스키마/키/장치 준비 미완료 |

중복 완료 응답도 현재 접근 권한을 먼저 확인한다. 같은 완료 결과는 최초 최소 결과를 돌려주며 새 감사/매핑 revision을 만들지 않는다.
권한 오류가 과거 성공을 취소하거나 재발급하는 동작은 아니다. 알 수 없는/변조된 결과는 다른 사람의 정상 작업을 소비하지 않는다.

## DB·상태·보안 제안

모델 초안은 `devices`/`device_keys`(장치/키 epoch·활성), `device_enrollments`(발급 관리자/접속·직원·장치·요청/티켓 digest·서버 만료·상태/버전),
`device_slot_claims`(device+slot 유일 예약/현재 소유), `user_device_mappings`(users FK·revision·활성/폐기), `device_enrollment_events`(최소 불변 이력)로 나눈다.
정확한 테이블명·열·인덱스/동시 등록 범위·SQL번호는 최종 모델 회신 후 고정한다. **SQL번호 미예약·실제 SQL/ORM 미작성·스키마1·기존002~006 불변·공유 DDL 미적용**이다.
users FK는 기존 INTEGER와 일치시킨다. 실제 AuthSession PK는 `token_hash` String(64)이며 정수 session_id를 새로 가정하지 않는다.
작업은 내부 접속 바인딩을 저장하고 현재 AuthSession을 다시 확인하는 안이다. 세션 삭제를 막는 강한 FK·이력 cascade를 만들지 않고, 세션 부재/만료 시 완료를 거절한다. 이 내부 바인딩/토큰 해시는 API·일반 감사에 노출하지 않는다.
과거 이력을 세션/receipt 정리 cascade로 삭제하지 않는다. 물리 슬롯은 비음수 JSON정수이나 실제 센서 모델·0 허용·최댓값/용량을 5번이 확인해야 한다. 이름표 파서 상한을 센서 용량으로 전용하지 않는다.

상태 제안: `issued` → `sensor_pending` → `completed`; 확정 실패 `failed`, 서버 무효화 `cancelled`/`expired`, 물리 저장 여부 불명은 `recovery_required`.
`sensor_pending`/물리 저장 보고가 `completed`를 뜻하지 않는다. **서버의 현재 매핑 활성화가 commit된 경우만 등록 완료**다.
장치당 센서 동작을 직렬화하고 서버의 슬롯 예약과 Pi 실제 빈 슬롯 확인을 모두 요구한다. 기존 점유 슬롯을 덮어쓰지 않는다.
센서를 기다리는 동안 DB 트랜잭션을 열지 않는다. 완료에서는 공통 인증 guard 순서 후 장치→작업→슬롯/매핑 잠금을 일관되게 잡는 안을 제안하며 기존 users lock 순서와 통합 검토한다.
등록/매핑 교체·장치/키 폐기·회원 비활성도 같은 잠금 규약에 참여해야 한다. UNIQUE만으로 권한/현재 관계 검사를 대체하지 않는다.

Pi로 보내는 ticket은 작업·대상 회원·장치·예약 슬롯/등록 범위·서버 접속 참조·nonce/만료·키 epoch에 묶는다.
enroll request/result는 기존 본인확인 request/result와 **별도 kind/MAC domain**이어야 한다. 현재 `device_protocol_draft.py`는 이를 지원하지 않으며 이름만 바꿔 재사용하지 않는다.
결과는 같은 작업/티켓 digest·고정 result_id·실제 저장 슬롯·두 채취 비교/저장 결과에 인증되어야 한다. 대상 회원을 PC가 결과에서 임의 지정하지 않는다.
키는 서버/Pi 비공개 설정에만, 일반 PC는 opaque 바이트 중계만 한다. PC/Pi 사이에 관리자 Bearer를 전달하지 않는다.
생체 이미지·템플릿·실명/점수·키·토큰·nonce/티켓/결과 원문은 공통 DB/일반 감사/Git에 남기지 않는다. MAC은 암호화가 아니므로 실제 양 구간 TLS/인증서 검증도 필요하다.
요청 digest/제한된 실패 분류·매핑 revision·최소 감사만 저장한다. **인증 기록1년**이라는 사용자 요구는 등록 작업 캐시/템플릿/매핑 보관 기간과 다르다.

## 부분 실패·취소·재시작 복구

| 상황 | 제안 처리/검증 책임 |
|---|---|
| issue 응답 유실 | 같은 client_request_id 결과 조회. 새 작업/슬롯을 자동 발급하지 않음. 1번 서버 중복 조회·5번 릴레이 보관 |
| Pi 진행/결과 응답 유실 | 같은 enrollment_id/ticket의 진행/동일 인증 결과 조회. 새 채취·result_id 생성 금지.5번 재시작에도 식별/저장 결과 보존 방식 회신 |
| 센서 저장 성공 후 서버 저장 실패 | 계정 유지·매핑 비활성/연결 확인 필요. 동일 인증 결과로 완료 재검사. 슬롯을 새 직원에게 재할당하지 않음 |
| 서버 commit 후 응답 유실 | 같은 작업 조회/동일 결과 재전송 → 기존 완료. 이력/매핑 활성화를 중복 실행하지 않음 |
| 관리자 권한/접속 변경 | UI/릴레이 즉시 취소·서버 현재 권한 재검사. 새 관리자/접속에 자동 이관하지 않음. 별도 관리자 복구 조회·이관 정책 협의 |
| 취소/만료/앱·Pi 재시작 | 늦은 완료를 거절하되 센서 저장 여부를 재확인. 불명 슬롯은 orphan 후보로 격리, 빈 슬롯으로 자동 재사용하지 않음 |
| orphan/탈퇴/재등록 삭제 | 서버 매핑 무효화와 실물 템플릿 삭제를 분리. 승인된 장치/슬롯에 대해 소유/작업 증거 확인 후 삭제·결과 재확인. 일괄 삭제/무조건 rollback 삭제 금지 |

취소된 작업의 물리 슬롯이 안전하게 비어 있다는 증거가 없으면 새 등록을 허용하지 않는다. 장치가 오프라인이면 삭제 대기 상태를 남기고 물리 삭제 완료라고 표시하지 않는다.
관리자가 창을 닫았다는 사실만으로 원격 작업/계정을 삭제하지 않는다. 운영자가 처리할 복구 목록·권한·작업/결과 보관 기간은 팀장/1/5번 회신이 필요하다.

## 실제 검증과 다음 결정

`dotnet run --project Tests/UserManagement.Ui.Tests/UserManagement.Ui.Tests.csproj -c Release`:
실제 WinForms + 통제 HTTP 응답으로 생성 ID가 페이지 밖/안인 경우·목록 실패·기존 수정·중복 클릭·선택/새 입력/같은 계정 재로그인/창 닫기/Dispose 뒤 지연 응답·통신 유실·접점 취소·콜백 중 대상 변경·401·미로그인을 검사한다.
이 검사는 실제 서버/MySQL·센서 등록 API를 실행하지 않는다.
2026-10-20 실행 결과: UI14시나리오/46검증, 기존 ApiClient25시나리오/125검증, IntegrationSupport31검증 통과. Release 빌드 경고0/오류0. 반복 결과는 합산하지 않는다.

후속 합의 후 1번 서버/5번 Pi 공동 검증은 다음을 포함한다. **아직 미실행이다.**

- 같은 지문 두 채취 성공·다른 지문 거절·센서 busy/timeout/full·범위 밖 슬롯·기존 점유 슬롯 보존.
- 관리자 권한 회수·대상 비활성·장치/키/매핑 교체·같은/다른 접속 전환·중복 issue/complete·다른 본문/결과 충돌.
- 센서 성공/DB 실패·서버 commit/응답 유실·취소/만료 후 지연 결과·PC/Pi 재시작·오프라인 orphan/삭제 복구.
- 소유 MySQL에서 FK/예약 UNIQUE·잠금 경합·rollback/단일 감사·dump 새 DB 복원, 실제 두 PC/TLS/장치 결과 대조.

5번에게는 장치 허용 범위/센서 용량·실제 새 등록/빈 슬롯 검사 API·enroll 별도 메시지/키 domain·시간 계측·재시작 결과/슬롯 복구 방식을 요청한다.
팀장에게는 등록 승인/복구·이관 권한·재등록/교체/탈퇴 삭제·미등록 계정 권한·보관 기간 결정을 요청한다.
**결정 시점은 실제 등록 API/DDL·버튼을 활성화하기 전**이다. 이 문서/코드 PR 게시가 상대 합의·물리 등록·공유 배포 완료를 뜻하지 않는다.
