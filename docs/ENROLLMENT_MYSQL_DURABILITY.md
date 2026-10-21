# 지문 등록의 MySQL 영속 기록 — 1번 서버 접점

2026-10-20,1번 → 5번·팀장. 사용자 기준은 **정식 서버와 장치 등록 영속 기록에 MySQL 사용**이다. Pi에 다른 DB를 설치하거나 SQLite를 이름만 바꿔 유지하는 설계가 아니다. 이 문서는 서버 구현과5번이 연결할 계약이며 실제 Pi 전환/기존 기록 이관 완료를 뜻하지 않는다.

## 활성화와 담당

공통 schema1·007 등록 테이블·[010 증거 테이블](../RemoteWork.Server/migrations/010_enrollment_durability.md)을 검토한 MySQL에 준비한 뒤 `REMOTEWORK_ENABLE_DEVICE_ENROLLMENT_DRAFT=1`과 **`REMOTEWORK_ENABLE_ENROLLMENT_DURABILITY_DRAFT=1`**을 명시한다. 기본은 off다.010 준비는 독립 CLI `init-enrollment-durability-draft`이며 일반init-db/007초기화·HTTP 요청이 대신 만들지 않는다. feature만 켰다고 기존 SQLite 서비스/파일이 전환되는 것은 아니다.

- 1번: 저장 전 commit/원문 결과 보관·권한/키 검증·DB/마이그레이션·운영 증거 보관 CLI.
- 5번: Pi의 RAM 실행 상태·최초/단계별 승인·센서 직렬화·새 서버 API 중계/수신 검증·서명 결과 전달, 기존 SQLite 상태/결과의 보존·검증·이관과 의존 제거.
- 실제 활성화 전: 기존 쓰기 작업 차단/정지·소유자와 전환 SHA/프로필·이관 근거·서버/Pi 설정을 맞춘다. 구버전runtime과 새runtime을 동시에 쓰지 않는다. 이번 서버코드 제공만으로 이 조건이 끝났다고 쓰지 않는다.

Pi에는 공통 MySQL 계정·직접SQL 권한을 배포하지 않는다. 기존 PC의 관리자 Bearer 중계와 서버↔Pi 방향키를 유지한다. 키 설정 파일은 생체정보·DB 저장소와 구분해 비공개로 보존한다.

**R55-01/02 보완:** durability 옵션을 꺼도 이미 MySQL intent/결과가 있는 작업과011에서 명시 재발급된 작업은 legacy `sensor_write`나 미기록 complete로 우회할 수 없다. 새 OFF 프로세스도 기존 테이블을 현재 잠금 조회해 이 제한을 유지한다. OFF 시작 시 새 모델/route/CLI를 등록하거나 DB에 접속하는 것은 아니며, 기존 등록 HTTP 요청에서 필요한 증거 검증만 지연 실행한다. 채취 승인도 MySQL RR snapshot의 과거 값 대신 현재 잠금 조회로 intent/결과 존재를 검사한다.

## 승인 scope와 순서

기존 승인 바이트/domain·공통5필드·ticket_digest·probe_nonce·server_time/valid_until을 재사용한다. [ApprovalProbe/FreshApproval](../RemoteWork.Server/remotework/enrollment_approval_draft.py)은 일회용 RAM probe·최대2초 RTT·최대5초 승인과 보수적 UTC 상한을 그대로 적용한다. PC가 probe/서명을 만들거나 승인 응답을 바꾸면 안 된다.

| scope | 새 모드의 의미 |
|---|---|
| sensor_capture | 최초/채취 단계의 현재 승인. 물리 store 허가는 아님. intent/결과가 이미 있으면 거절 |
| sensor_store | **storage-intent 최초 commit 응답에서만** 받는 저장 직전 승인. 일반approval 경로에서는 발급 불가 |
| result_read | 기존 작업/결과 읽기. 새 채취/store 권한이 아님 |
| sensor_write | 구버전 전체쓰기 scope. 새 모드에서는409 ENROLLMENT_MYSQL_PROTOCOL_REQUIRED |

5번은 새 `sensor_capture`로 최초 승인/신뢰 UTC를 확보하고 두 채취·현재성·슬롯 빈칸을 확인한다. **store 직전에 새 sensor_store probe를 만들고 아래 storage-intent를 호출**한다. `may_store=true`와 원문 승인 MAC/공통바인딩·scope/nonce·RTT/만료를 검증하고 MySQL commit 응답을 확인한 뒤에만 정확히 한 번 store한다. 이 permit을 RAM에서 소비하고 통신 실패/취소/시각 불확실·재시작 뒤 재사용하지 않는다.

네트워크와 물리 센서는 원자적이지 않다. intent 뒤 결과가 없으면 저장 여부가 불명하므로 자동 다시 채취/저장하지 않는다. 같은 요청 재전송도 새 store 승인을 발급하지 않는다. 이는 일부 응답 유실에서 운영자 복구가 필요한 보수적 계약이다.

## API

접두사 `/api/v1/device-enrollments/{enrollment_id}`. 기존 공통 envelope와 관리자 인증을 사용한다. **현재 활성 관리자·명시 장치grant·원래 발급한 사용자/접속·대상활성·장치/키/프로필·claim/revision·결과조회 기한**을 요청마다 검사한다. 다른 접속/다른 관리자의 자동 인수는 없다.

### POST `/storage-intent`

입력은 기존 approval의7필드이며 scope만 sensor_store다.

```json
{
  "operation_revision": "1",
  "reserved_slot": 12,
  "claim_revision": "1",
  "key_epoch": "1",
  "ticket_digest": "64자리 소문자 SHA256",
  "probe_nonce": "Pi가 RAM에서 새로 만든16~64바이트의정규base64url",
  "scope": "sensor_store"
}
```

예시는 프로필/빈 슬롯 승인이 아니다. BIGINT revision은 정규 문자열, 슬롯은 JSON정수다. 최초만 MySQL에 intent/hash/바인딩·감사를 같은TX로 commit하고 `may_store=true,approval_envelope`를 반환한다. **동일 probe 재전송은 may_store=false/approval_envelope=null**인 현재 증거만 반환한다. 새 probe로 같은 작업에 다시 요청하면409 ENROLLMENT_PHYSICAL_RECOVERY_REQUIRED다. 새 intent가 필요한 원래 작업이 만료·취소됐으면 승인하지 않는다.

### POST `/physical-result`

기존 complete와 같은 입력2필드 `operation_revision,result_envelope`. result_envelope는 Pi가 만든 **원문 signed envelope bytes의 정규base64url**이다. Pi가 RAM에서 고정한 result_id·elapsed/결과를 재서명/재생성하지 않고 동일 원문을 전달한다. 지문 이미지/템플릿은 이 payload에 포함하지 않는다.

서버는 방향별 MAC·장치/키·공통필드/원래 ticket_digest를 검증하고 원문 BLOB·SHA256·result_id·outcome/failure를 MySQL에 commit한다. 같은 원문은 같은 결과, 다른 원문409다. result_id는 테이블 전체에서 유일하다. 성공 sensor_stored는 먼저 commit된 intent가 있어야 한다. store 전 실패는 intent 없이도 서명된 결과를 기록할 수 있다. **이 API 자체는 회원 매핑·job/claim 상태를 바꾸지 않는다.**

**R55-03 보완:** 이미 legacy complete로 확정된 job.result_hash가 있으면 최초 원문 보관과 운영자 이관도 그 hash와 비교해 충돌을409로 거절한다. 확정된 **동일 원문**은 intent가 없던 과거 성공도 증거로 보관할 수 있으며 새 매핑/재저장 승인이 아니다. 아직 확정되지 않은 성공의 intent 선행 조건은 유지한다.

Pi가 store 후 응답을 잃었으나 같은 실행의 RAM 원문이 남으면 같은 bytes를 재전송한다. Pi가 원문 전달 전에 재시작해 RAM을 잃으면, intent만 있는 서버 상태를 읽어 확인 필요로 중단한다. 원문을 임의 재생성하거나 센서를 다시 저장해서 메우지 않는다. 서버가 원문을 받은 뒤 Pi/PC가 재시작했다면 아래 읽기로 동일 bytes를 받는다.

### GET `/physical-result`

최소 공통바인딩과 `contract=enrollment-mysql-v1`, `phase=not_recorded|store_intent|result_recorded`, result_envelope/result_id/outcome/failure_code·intent_at/reported_at를 반환한다. 조회의 may_store는항상false,approval_envelope는null이다. 원문은 저장 SHA256/MAC/메타데이터를 다시 확인한 뒤 반환한다. 손상된 내용을 덮어쓰거나 성공으로 숨기지 않는다.

### 기존 POST `/complete`

새 모드에서는 **먼저 저장된 동일 physical-result의 hash/바인딩**을 확인한다. 이후 기존 현재 권한·원래 기한/상태·단일매핑/결과/감사 원자 저장을 그대로 실행한다. sensor_stored만 보고 등록 완료로 표시하지 않으며 서버 completed 이후 표시한다.

만료/취소 뒤 처음 도착한 결과도 기존 결과 조회 창 안이면 physical-result로 증거를 보관할 수 있지만 complete는 계속409이며 자동 매핑하지 않는다. 기존 결과조회 TTL(성공/종료 또는 기한 기준86400초)을 그대로 사용하고 읽기/재전송으로 연장하지 않는다. 보존기간 전체의 운영 정책과 자동 삭제는 이번에 새로 정하지 않았다.

## 기존 SQLite 증거 이관과 접속 종료

원발급 접속이 종료되면 새 로그인으로 위 API를 자동 인수하지 않는다.5번은 기존 저장소/서비스/키를 보존하고 **변경하지 않은 signed envelope의 원문 bytes**를 비공개로 추출한다.1번 운영자는 같은 작업/장치·현재키/claim·원문MAC/보관기한을 확인하고 명시 CLI로 보관할 수 있다.

```text
# reviewed private environment; actual enrollment/durability flags and 010 required
python -m flask --app remotework archive-enrollment-result --enrollment-id <작업UUID> --login-id <활성관리자> --result-file <비공개원문JSON파일>
```

이 CLI는 현재 활성admin+해당 장치grant를 요구하고 기존 작업·예약 바인딩을 대조한다. bearer나기존접속hash를변조하지 않는다. 기존 SQLite의 검증된 성공 원문도 증거만 보관하며, MySQL intent가 없던 성공을 자동 매핑하지 않는다. 매핑/예약해제/원래세션인수는 수행하지 않는다. MAC 원문이 없으면 화면/ledger 한 행으로 결과를 위조하지 않고 이관 미완료로 남긴다.

## 만료 예약의 복구 경계

**2026-10-21 추가:** 서명/010 intent·결과/매핑이 없는 만료 작업에는 [운영자 종료·명시 재발급 계약](ENROLLMENT_RECOVERY.md)과 독립011을 제공한다. 아래의 최초 복구 경계 중 ‘별도 검토된 변경안’이 이 제한 범위에서 구체화된 것이며, 실제 서버 적용·예약 복구·5번 Pi 전환 완료는 아니다. signed 결과나 store 증거가 있으면 여전히 아래의 증거 보관/별도 검토를 따르고 자동 재발급하지 않는다.

1. 실제 서버 job/claim/mapping·원발급 접속·현재키/프로필/기한을 읽고 기존 증거/백업을 보존한다.
2. 기존 Pi/PC 작업이 더 이상 store를 실행하지 못하도록 종료·fence하고, 그 뒤 새로 인증된 inventory로 해당 한 칸의 빈 상태를 확인한다. 실패코드/오래된 빈칸목록만으로 빈 슬롯을 추정하지 않는다.
3. 기존 signed 결과를 위 절로 보관하고 현재 작업의 차단·이력/revision 보존·새접속/새작업 관계를 운영자가 명시 결정한다. 단순 DELETE/직접claim해제·다른 손가락 즉시 새작업은 이 구현에서 제공하지 않는다.
4. 변경안·장치 소유자 확인과 실제 증거가 모인 후 별도 검토된 복구/재발급을 적용한다. 이 문서/코드 제출만으로 실제 복구가 완료되거나 슬롯 재사용이 승인된 것은 아니다.

실제 실패 후 빈 슬롯·기존 writer 차단 확인이 아직 없다면 이2단계에서 대기한다. 기존 센서 템플릿/SQLite 복구·서명 원문/MySQL 작업·claim/키를 삭제하지 않는다.5번은 서버 계약을 연결·검증해 MySQL 전용 전환을 마친 뒤 SQLite 제거/기존 증거 보존·이관 완료를 각각 보고한다.

## 검증

`tests/test_enrollment_durability.py`는 소유 MySQL에서 단일승인/동시요청·commit실패rollback·MAC/권한/접속/키회수·기한후증거/매핑차단·손상/중복결과·운영자이관·raw010/FK/CHECK/UNIQUE·실제dump→새DB원문바이트복원을 검사한다. 센서·실제Pi·공유DB·두PC·기존데이터이관이나5번새클라이언트의store직전콜백을 대신 검증한 것은 아니다.
