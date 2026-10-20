# AS608 새 지문 등록 코어 — 5번 로컬 준비

현재 추가 제공 범위: 아래 후속 절의 `enroll_online`(최초 온라인 승인/시각·단계별 재승인)과 `read_result_online`(만료 후 읽기 전용 승인/기존 결과 반환)을 구현했다. 서버 계약은 PR44 `ba6a37a` 기준이며 실제 PC 승인 HTTP 중계·등록 API/버튼·키 배포·실물 서비스 적용은 아직 별도다. 아래 앞부분의 미구현 표시는 각 준비 시점 기록이고 최신 구현/검증은 마지막 후속 절을 따른다.

1번의 [PR43 등록 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6280787734)과 PR44 계약 초안에 맞춰 **실물 등록 처리만 분리한 코어와 대역**이다. 등록 버튼·서버 API·C# 중계·별도 enroll 티켓 파서/MAC·TLS·배포는 포함하지 않는다. 기존 `pi_api.py`에 새 공개 등록 경로를 추가하지 않았으며, 파일 import만으로 UART를 열거나 지문을 저장하지 않는다.

## 실물 근거와 번호 범위

2026-10-20 사용자 보고: 센서 표기 **AS608**, VCC3.3V, TX→GPIO15(물리10), RX→GPIO14(물리8), GND 연결. 읽기 전용 실물 조회의 용량300·등록11·개수/목록 일치, 라이브러리2.2.25와 서비스 health 복구를 사용자가 확인했다. 실제 목록/회원·장치 식별 자료는 Git에 저장하지 않는다.

[개발보드 공급자의 AS608 자료](https://wiki.lckfb.com/zh-hans/dmx/module/sensor/fingerprint-recognition-sensor.html)는300개/0~299를 설명한다. 판매 모듈별 차이와 단위 장치의 실물 등록 검증을 구분한다. **이 장치에서0번·299번 저장/삭제를 시험하지 않았으며 제조사/세부 보드 리비전은 확인되지 않았다.** 배선색을 모든 모듈에 일반화하거나 현재3.3V 배선을 변경하지 않는다.

`SensorProfile(expected_capacity, minimum_slot, maximum_slot)`은 호출자가 명시한다. 아래 `300,1,299`는0번을 제외한 대역 예시 정책이며 운영 등록 범위 확정·실물 검증 완료가 아니다. 실제 capacity가 달라지면 거절한다. `reserved_slot`은 향후 서버 예약/검증된 티켓에서만 받아야 하며, 이 모듈이 빈 번호를 자동 배정하지 않는다.

## 접점과 진행

```python
from fingerprint_enrollment import (
    EnrollmentLedger, EnrollmentOperation, FingerprintEnrollment, SensorProfile,
)

# 실제 배포 전에 전용 서비스 사용자 소유의 비공개 디렉터리를 준비한다.
ledger = EnrollmentLedger(".enrollment-state/enrollments.sqlite3")
worker = FingerprintEnrollment(
    sensor, codes, shared_sensor_lock, ledger, configured_device_id,
    SensorProfile(300, 1, 299), authorize=validate_current_enrollment_authorization,
    progress=show_enrollment_stage,
)
# operation 필드는 검증된 enroll 티켓에서만 만든다. PC의 임의 회원/번호는 금지.
receipt = worker.enroll(operation, cancellation_event)
```

- `EnrollmentOperation`: 소문자 UUID `enrollment_id/device_id`, 직원 INTEGER `target_user_id`, 정규 양의 BIGINT문자열 `operation_revision`, 정수 `reserved_slot`, `SHA256(request MAC input)` 소문자 hex인 `ticket_digest`. envelope JSON 전체나 재직렬화 payload의 해시가 아니다. 이것은 코어 입력이며 새 공통 wire DTO 확정안이 아니다.
- `authorize`는 기본 허용 구현이 없으며 호출마다/채취 도중/저장 전후에 현재 승인 여부를 재검사한다. 향후 어댑터가 별도 enroll domain/MAC·장치/대상/예약·신뢰 시각/만료·취소/키 검증을 **먼저 구현**해야 한다. 예제처럼 항상True를 반환하는 대역을 제품에 연결하지 않는다. 현재 이 검증/서명 어댑터는 미구현이다.
- `shared_sensor_lock`은 미래 통합 시 기존 `FingerprintService.lock`과 같은 객체여야 한다. 인증·등록 모두 같은 프로세스/UART 소유 아래 직렬화한다. 코어만의 잠금으로 다른 프로세스 UART 사용을 차단했다고 표시하지 않는다.
- 진행은 빈 번호/목록 확인→손 떼기→첫 채취/특징 추출→기존 등록 지문 중복 검색→손 떼기→두 번째 채취/특징 비교→빈 번호 재확인→쓰기 전 기록 commit→센서 저장→목록 재확인→센서 결과 기록이다. `finger_search` 등은 설치 버전 [Adafruit2.2.25](https://github.com/adafruit/Adafruit_CircuitPython_Fingerprint/blob/2.2.25/adafruit_fingerprint.py) API를 사용한다.
- `progress`는 remove_finger/first_capture/second_capture/sensor_stored_server_pending을 전달한다. 표시 구독이 닫혀도 센서 작업/결과를 바꾸지 않는다. 취소 토큰을 별도로 확인한다.
- 손 떼기/올리기는 각15초, 전체 기본90초/최대120초의 **로컬 I/O 예산**이다. 센서 호출 자체를 중간에 강제 중단하는 보장은 없고 반환 후 재검사한다. 이는 서버 ticket TTL 합의값이 아니며 실제 최악 시간 측정은 아직 하지 않았다.
- `SensorReceipt`는 enrollment/result/device/slot/state/elapsed_ms다. **sensor_stored는 서버 매핑 완료가 아니며 서명된 공통 인증 결과도 아니다.** 정상 회원 연결 표시는 향후 서버 commit 응답으로만 한다. 관리자 Bearer·장치 키·생체 템플릿/이미지를 이 DTO로 전달하지 않는다.

## 재시도·실패·복구

Pi의 전용 SQLite ledger는 작업ID·장치·슬롯·고정 result_id·요청 바인딩 hash·진행 상태·제한된 오류·경과시간만 보존한다. 원문 티켓/nonce/결과 envelope·키·회원ID/이름·점수/지문을 보관하지 않는다. 서버 MySQL 모델/DDL이나 일반 감사 대체물이 아니다. 실제 배포 시 서비스 사용자만 접근할 디렉터리0700/파일0600·저장소 권한을 확인하고 파일을 임의 삭제/편집하지 않는다. `.enrollment-state/`는 Git에서 제외한다. 보관/정리 기간은 아직 합의되지 않았으며 자동 정리는 없다.

- 같은 작업·같은 바인딩의 저장 완료 재조회는 재시작 후에도 같은 `result_id`/센서 receipt를 돌려주며 스캔/저장을 반복하지 않는다. 조회 호출도 승인 검사를 통과해야 한다.
- 같은 ID의 다른 회원/장치/슬롯/revision/티켓은 거절한다. 저장 완료 슬롯은 다른 작업에 자동 재사용하지 않는다.
- `capturing`/`store_pending`에서 중단됐거나 저장 응답이 유실되면 해당 장치의 새 등록을 보수적으로 막고 `recovery_required`로 취급한다. 저장을 다시 보내거나 새 번호를 고르지 않는다.
- 저장 전 실패는 failed로 남고 같은 작업의 자동 재채취를 막는다. 저장 시작 뒤 실패/취소/기한 초과·목록 불일치·최종 ledger 기록 실패는 확인 필요로 남긴다. 기존/불명 템플릿을 rollback 명목으로 삭제하지 않는다.
- 이 모듈은 승인된 삭제/매핑 최종 확인·슬롯 해제·오프라인 복구/이관을 아직 제공하지 않는다. 복구 목록/권한·재등록/탈퇴 정책과 서명된 동일 결과 보존은1번과 후속 합의가 필요하다. 코어 receipt만으로 서버 완료를 주장하지 않는다.

## 검증·현재 제공 범위

```text
python -m unittest discover -s Devices/RaspberryPi -p "test_fingerprint*.py" -v
```

대역 센서/임시 SQLite로 정상 두 채취·기존 지문 보존·등록 지문 중복/다른 손가락·사용 중/시간 초과/범위·빈 번호 재확인·권한/취소·동일 작업/다른 바인딩·재시작·쓰기 전 기록·센서 성공/응답 유실·로컬 디스크 실패를 검증한다. 실제 지문 채취/저장/삭제·OLED·서버/DB·C# 버튼·티켓/MAC·TLS·전원 차단/실제 디스크 손상·두PC는 이 대역 검증에 포함되지 않는다.

2026-10-20 Windows/Python3.13.15에서 위 원본 명령으로 **33개 통과**(새 등록 코어22개+기존 읽기 점검11개), `py_compile`·diff 검사와 로컬 상태/보고 파일 Git 제외를 확인했다. 테스트의 SQLite 조회 연결 미해제로 발생한 Windows 임시파일 정리 실패를 닫기 처리로 수정한 뒤 제출 원본이 통과했다. 센서/서버에 새 등록 코드를 배포하지 않았고 프로그램용 공통/타 담당 파일도 변경하지 않았다. 사용자의 실물 읽기 조회 성공과 이 대역 결과를 실제 새 등록/제품 연결 완료로 확대하지 않는다.

1번에게 대조할 제공물은 코어 입력/receipt·공유 잠금·로컬 진행/취소·불명 결과 차단 방식이다. 별도 enroll wire domain/원문 바이트·키/만료/서명된 결과 재시작 보존, 서버 슬롯 예약/claim 및 복구/보관 정책을 합의하고 실물시험하기 전에는 실제 등록 버튼/공개 API에 연결하지 않는다.

## 2026-10-20 후속 — 요청 검증·등록 코어·서명 결과 보관 연결

이 절은 위 코어만 준비했던 시점의 미구현 기록을 갱신한다. 기준은 원본 alpha `6cb4d5d`, LOG `99cd6f8`, PR `66f287b`와 [1번의 최신 PR43 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6281407809), 미병합 PR44 head `2a34e22e6828cfbb31cffcc9208e6cdbc767d3a2`이다. 기존 원본 변경은 로컬 alpha에 포함돼 있고 이번 확인 때 추가 변경은 없었다. PR44를 병합하거나 1번 공통 파일을 수정하지 않았다.

새 파일:

- `fingerprint_enrollment_wire.py`: Pi 전용 독립 codec. 정확히6개 envelope 항목, 중복/미지 JSON 항목, UTF8/크기/정규 base64url/32바이트 MAC/UUID/정수와 BIGINT문자열/nonce/시각 형식, 별도 `RW-ENROLL-v1` domain과 방향키·기대 epoch를 확인한다. 회원/nonce/claim 등이 들어 있는 요청은 메모리에서만 검증하고 결과에는 공통5필드와 digest/result ID/outcome/failure/elapsed만 담는다. `ticket_digest`는 **원래 요청의 MAC input**에서 계산한다. 방향키는 신뢰 설정에서 명시 공급하고 PC에 전달하지 않는다. 제품용 요청 서명 API/실제 키는 추가하지 않았다.
- `fingerprint_enrollment_adapter.py`: 검증한 요청을 `EnrollmentOperation`으로 바꾸고 기존 코어와 공유 센서 잠금을 사용한다. `current_approval(ticket)`와 `trusted_utc()`는 기본 허용/시스템 시각 fallback 없이 **외부에서 반드시 제공**해야 한다. 누락/예외/False/비bool True·미래/만료·취소는 센서 I/O 전에 거부하며 채취 도중·쓰기 전후·결과 전달 직전에도 검사한다. 느린 승인 호출 뒤 시각도 다시 검사한다.
- `test_fingerprint_enrollment_adapter.py`: 공개 합성 키·가상 대상과 가짜 센서만 사용하는 codec/연결/캐시 회귀. 기존 `test_fingerprint_enrollment.py`의 대역을 재사용하므로 두 테스트 파일을 함께 둔다. 1번의 공개 요청/결과 MAC과 digest를 그대로 대조한다.

`SignedResultCache`는 Pi 전용 비공개 폴더에 별도 SQLite를 사용한다. 저장 항목은 등록 작업 ID·코어 바인딩 hash·서명된 결과 바이트뿐이다. 성공 결과를 **commit한 뒤에만 반환**하며 재전송은 저장된 원문 바이트를 반환해 서명·result ID·elapsed를 다시 만들지 않는다. 같은 작업의 다른 claim/epoch/대상 등은 digest/바인딩으로 거부한다. 저장된 결과도 MAC/공통필드/digest를 다시 검증하고 변조/손상 시 덮어쓰기·새 스캔 없이 중단한다. 결과 캐시 기록에 실패하면 원래 코어의 고정 receipt를 보존하고 전송 결과를 반환하지 않는다. 이후 승인된 재시도는 기존 receipt에서 미전송 결과를 고정하며 센서를 다시 쓰지 않는다.

경로 예: `.enrollment-state/signed-results.sqlite3`. POSIX에서 전용 부모 폴더 소유자/0700, 파일 소유자/0600을 요구하고 symlink·일반 파일이 아닌 대상·공개 권한은 거부한다. 실제 SD 전원장애 내구성, Windows 운영 ACL, 키 저장/배포·TLS는 이번 검증 범위가 아니다. 캐시/ledger를 임의 삭제·편집하거나 만료만으로 불명 슬롯을 해제하지 않는다. 서버 MySQL/DDL 변경은 없다.

호출 접점은 다음과 같으며 아래 미구현 제공자를 항상 True/일반 PC시각으로 대체해서 배포하지 않는다.

```python
from fingerprint_enrollment_adapter import EnrollmentAdapter, SignedResultCache
from fingerprint_enrollment_wire import DirectionKeys

# selected_keys: 신뢰할 Pi 설정에서 선택한 DirectionKeys, 수신 헤더로 임의 선택 금지
# current_approval_provider: 새 Pi 난수에 결합한 검증 가능한 온라인 승인 등 후속 합의 필요
# trusted_utc_provider: 신뢰/오차 보장이 있는 UTC 제공자, 미확인 시 None/예외로 차단
adapter = EnrollmentAdapter(
    sensor=sensor, codes=codes, sensor_lock=shared_sensor_lock,
    ledger=ledger, profile=explicit_sensor_profile, keys=selected_keys,
    cache=SignedResultCache(".enrollment-state/signed-results.sqlite3"),
    current_approval=current_approval_provider, trusted_utc=trusted_utc_provider,
)
signed_bytes = adapter.enroll(original_request_bytes, cancellation_event)
```

**제공 범위와 남은 작업:** 어댑터는 코어의 `sensor_stored` 성공 receipt만 서명·고정한다. 실패/복구 필요는 제한된 `EnrollmentError.code`와 코어 ledger로 남고, 실패 envelope/진행 조회·취소 HTTP DTO와 최종 복구 API는 아직 연결하지 않았다. codec은 실패 결과 형식도 검사하지만 그것이 실패 응답 경로 구현 완료를 뜻하지 않는다. 이 어댑터는 완료 캐시 조회도 유효한 요청과 현재 승인을 요구한다. 만료 뒤 과거 성공 결과 조회의 접근·조회 창은 별도 서버 계약이며 여기서 임의 TTL/24시간 예외를 만들지 않았다.

현재 승인·UTC 제공자의 실제 신뢰 구현, 서버 예약 claim·발급/완료/조회·단일소비/매핑 transaction, 등록 버튼/C# 중계·비동기 API·실제 키/배포는 여전히 미구현이다. 온라인 승인 메시지/경로를 이번에 임의 확정하지 않았다. 센서 성공은 서버의 계정 연결 성공이 아니다. `pi_api.py`·서비스·UART/OLED·실제 지문은 변경/사용하지 않았다.

이번 Windows/Python3.13.15 실행 명령:

```text
.\Tools\HandPresence\.venv\Scripts\python.exe -X utf8 -W error::ResourceWarning -m unittest discover -s Devices/RaspberryPi -p 'test_fingerprint*.py' -v
```

**55개 중54통과/1생략**(기존33개 포함, 신규22개 중21통과/1생략). 생략은 Linux/Pi POSIX 파일 소유자/권한 검사이며 실물 Pi에서 별도로 실행해야 한다. 공개 벡터 MAC/digest 일치·엄격 파서·현재 승인/시각 불명 차단·재시작 동일 바이트·캐시 실패 뒤 재저장 방지·불명 결과 차단을 가짜 센서/임시 DB로 확인했다. 사용자가 앞서 Pi에서 실행한 코어33개 성공은 **이 새 어댑터를 Pi에서 실행한 결과가 아니다.** 실제 지문 등록/삭제·서버 계정 연결·전원 차단/두PC는 미실행이다.

수정은 위 신규3파일과 이 문서/장치 README뿐이며 로컬 미커밋 상태다. 포크 push·PR 생성/원본 병합·Pi 배포·협업 댓글 전송은 수행하지 않았다. 날짜 LOG는 팀장 전용이고 최신 협업 지침은 기존 PR43 댓글의 통합 회신을 팀장이 기존 LOG에 취합하는 방식이어서 새 LOG 파일/PR은 만들지 않았다. 사용자 승인 후 게시할 경우 최신 검사 결과와 공개 코드 커밋을 하나의 협업 회신으로 전달한다.

### 사용자 수행 Pi 검증 — 2026-10-20

위 Windows 검증 후 사용자가 Pi의 기존 가상환경에서 준비 모듈을 별도 `enrollment_lab` 폴더로 복사하고 `python3 -m unittest discover -s . -p 'test_fingerprint*.py' -v`를 직접 실행했다. 제공된 전체 출력은 **55개/0.148초/OK, 생략0개**이며 Windows에서 생략한 POSIX 캐시 소유자/파일 권한 검사도 `ok`다. 이는 사용자가 수행한 Pi 가짜 센서/임시 SQLite 검사 결과로, 위 Windows 결과와 합산하지 않는다. 준비 모듈의 Pi 복사·검증은 확인했지만 실제 서비스/API에 등록 기능을 활성화하거나 실물 지문 저장/삭제·서버 계정 연결·실제 전원 차단 시험을 수행한 것은 아니다. 로컬 코드 미커밋·포크 push/PR 생성·협업 회신 전송 대기는 유지한다.

### 협업 회신 갱신 — 2026-10-20

위 사용자 수행 결과 확인 뒤 사용자가 전달을 승인해 [기존 PR43의 5번 통합 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6281140854)에 후속 절을 추가하고 재조회로 본문 일치를 확인했다. 이전 코어 준비 기록은 보존했다. 정확한 digest/방향키·epoch/단일claim·동일결과 바이트 방향의 수용, Windows54통과/1생략과 사용자 Pi55통과, 성공만 서명하는 현재 범위·실제 승인/UTC 제공자 미구현, 서버 API/온라인 현재성/매핑 commit·비동기·운영 정책 회신 요청을 한 번에 전달했다. 새 Pi 난수에 묶인 온라인 승인 왕복은 대조 제안이며 이미 합의/구현된 기능이 아니다. 댓글 게시만 확인했으며 1번의 읽음·회신/공동 합의·제품 통합을 완료로 표시하지 않는다. 코드 미커밋·포크 미게시/새PR 미생성 상태는 유지하고 별도 협의 LOG 문서는 만들지 않았다.

### 1번 API 후속 수신·연결 문제 점검 — 2026-10-20

사용자 요청으로 [1번의 새 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6282390850)과 PR44 head `08f1464fdd4383d80a3cb6def20a457b3eb4e34b`의 API 계약·승인/서비스/CLI/모델/raw007·검사를 확인했다. PR44는 확인 시 open/미병합이었다. 이전의 서버 API 미구현 상태는 당시 기록이며 현재 1번은 독립 기본off API/DB와 온라인 승인 참고 구현을 제공했다. 원본 alpha에는 아직 병합되지 않아 작업공간에 임의 병합하지 않았다.

이번 직접 재현은 Windows/Python3.13.15, 공개 합성 키·가짜 센서/임시 SQLite와 `git show`로 읽은 peer 순수 codec의 메모리 로딩으로 수행했다. Flask/서버 앱/HTTP·실제 MySQL·실물 센서를 실행하지 않았다. 아래 항목은 수정 완료가 아닌 검토 결과다.

1. **서버 프로필 검사 불일치:** `features/enrollment/setup.py`의 실제 CLI 조건식을 AST로 추출해 평가했다. 용량300/최소1/최대299/후보12는 서버 거절·Pi `SensorProfile(300,1,299)` 허용, 용량300/최소0/최대999/후보999는 서버 허용·Pi 거절이었다. 서버 모델/raw007도 `capacity <= maximum_slot - minimum_slot + 1`을 사용한다. 실제 용량과 허용 슬롯 부분범위의 의미/상한을 1번과 맞춰 CLI·ORM·rawSQL을 함께 정정해야 한다. 실제 MySQL DDL 거절/허용을 실행해 재검증한 결과는 아니다. 예제 범위를 운영 슬롯 승인으로 확대하지 않는다.
2. **5번 최초 승인/시각 연결 미완료:** `trusted_utc()`가 None이고 `current_approval` 호출에서 첫 승인을 받아 UTC를 만들려는 공급자를 연결하면 `not_authorized`, 승인 공급자 호출0회·센서 호출0회로 재현됐다. 현재 `_authorized`가 시각부터 검사하므로 1번 계약처럼 최초 probe 승인을 어댑터 진입 전에 준비하는 별도 시작 접점이 필요하다. 이는 차단 방어를 없애라는 뜻이 아니다. 기존 시각/승인 공급자 부재 차단은 정상이며 실제 온라인 bootstrap은 미구현이다.
3. **5번 만료 캐시 읽기 미연결:** 성공 결과를 고정한 뒤 원래 요청 만료 시각으로 옮겨 재요청하면 `not_authorized`, 추가 센서 호출0회였다. 신규 저장 승인과 분리된 `result_read` 캐시 읽기/재전송 경로를 추가해야 한다. 만료된 요청으로 `enroll()`의 쓰기 권한을 열어 해결하지 않는다.

호환성 확인: 우리 어댑터의 서명된 성공 결과를 PR44 `enrollment_protocol_draft.verify_completion`으로 검증해 `sensor_stored`와 바인딩 통과를 확인했다. 새 `ApprovalProbe`도 합성 서버 승인으로 최초 수신·sensor_write/result_read 구분·같은 probe 재사용 거절을 직접 확인했다. 이 순수 검사 성공을 실제 서버/MySQL/키배포·현재 권한 증명·실장치 통합으로 확대하지 않는다. 1번의175/698개 수치는 상대 보고이며 이번에 재실행한 수치가 아니다.

제품 코드 수정·새 push/PR·협업 댓글 갱신은 이번 검토에서 하지 않았다. 1번의 프로필 조건 정정 협의와 5번 온라인 승인 시작/만료 결과 읽기 구현·PC 비동기 중계가 다음 작업이다. 날짜 LOG/새 협의 문서는 만들지 않았고 이 절은 코드 문서의 로컬 검토 메모다.

후속 전달: 사용자가 위3건의 전송을 승인해 [기존 PR43 통합 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6281140854)에 A(1번 프로필 검사 정정), B(5번 최초 승인/시각 준비), C(5번 result_read 전용 캐시 읽기)를 추가했다. 재조회로 본문 일치를 확인했다. A의 CLI/ORM/raw007 조건 의미·회귀 검증, B/C의 연결 기준 확인을 요청하고, 만료 후 이미 서버가 확정한 결과 재전송과 기한 지난 최초 완료/복구를 구분했다. 게시만 완료했으며 상대 읽음/회신·합의·코드 수정/실제 통합·push/새PR은 아직 확인/수행하지 않았다. 별도 LOG 문서/PR은 만들지 않았다.

### A 수정·B/C 기준 회신 수신 — 2026-10-20

[1번 최신 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6282644762)과 PR44 head `ba6a37ab95a37e7a6468f6518efe88a77be2650d`를 확인했다. CLI·ORM CHECK·raw007·실행 시 후보 검사 모두 `0 <= minimum_slot <= maximum_slot < capacity`로 수정된 diff를 확인했다. 실제 CLI 조건식을 추출해 용량300/범위1~299/후보12 허용, 범위0~999·0~300 및 범위1~299의 후보0 거절을 직접 재검증했다(4조건 통과). 실제 MySQL DDL/전체719개 검사는 이번에 재실행하지 않았으며 1번의 작성자 보고와 구분한다. 기존007이 적용된 DB의 CHECK는 create_all로 갱신되지 않아 명시 변경 절차가 필요하다는 안내도 수신했다. 실제 사용자 DB에는 변경하지 않았다.

B의 최초 probe→검증된 승인/신뢰UTC→어댑터 시작/단계별 갱신과 C의 result_read 전용 캐시·원문 재전송 기준은 1번이 확인했다. 이 연결 기준 회신은 수신 완료이고 실제 Pi 온라인 승인/callback·만료 결과 읽기는 여전히 5번 구현 대기다. PR44는 open/원본 미병합이며 fetch로 검토 소스만 받아 작업 브랜치에 임의 병합하지 않았다. 추가 원격 회신/push/PR은 이번 확인에서 하지 않았다.

## 2026-10-20 후속 — B 온라인 승인 연결과 C 만료 결과 읽기 구현

사용자 구현 요청으로 B/C를 로컬에 구현했다. 기준 원본 alpha/LOG/PR와 미커밋 변경을 보존하고 PR44 최신 head `ba6a37ab95a37e7a6468f6518efe88a77be2650d`를 fetch/대조했다. 원본 alpha 추가 변경은 없었고 PR44는 미병합이므로 임의 병합하지 않았다. 1번 서버/DB/공통/C#·기존 Pi 서비스는 변경하지 않았다.

### 제공 파일·동작

- 신규 `fingerprint_enrollment_approval.py`: 별도 `RW-ENROLL-APPROVAL-v1`/`enroll_approval` 수신 검증과 메모리 전용 `OnlineEnrollmentApproval`. 장치/키/epoch·공통5필드·티켓digest·매번 새32바이트 난수 probe·scope·decision·서명 유효구간을 검증한다. 왕복2초/명목5초·1.25초 보수 여유+RTT+수신 후 경과를 적용한다. 서명 검사 처리 시간도 경과에 포함하고 비정상/역행 시계를 거절한다. probe/승인/시각은 디스크에 저장하지 않는다.
- 변경 `fingerprint_enrollment_adapter.py`: `enroll_online(raw_request, cancellation, exchange=...)`가 **최초 새 probe 왕복·MAC/바인딩 검증→신뢰 UTC 확보→기존 등록 코어 진입**을 수행한다. 각 권한 검사 때 온라인 공급자가 새 probe로 현재 승인을 갱신한다. `_authorized`도 승인 갱신 뒤 시각을 검사하도록 바꿔 이전 UTC가 없거나 만료된 상태에서 새 승인을 얻지 못하던 시작 순서를 해결했다. 원래 티켓의 시작/만료·취소 검사는 계속 유지하며 읽기 승인을 쓰기로 사용하지 않는다.
- 같은 어댑터의 `read_result_online(enrollment_id, cancellation, exchange=...)`: private 캐시의 서명된 최소 작업 바인딩을 내부에서 읽고 확인한 뒤 **현재 서버의 result_read 승인**을 새로 받아야 결과를 외부에 반환한다. 원래 티켓을 다시 보관/제출할 필요가 없으며 재시작 뒤에도 등록작업ID만으로 기존 결과를 찾을 수 있다. 원래 쓰기 티켓의 만료를 예외로 풀어 등록 코어에 진입하지 않는다. 센서·코어·새result_id·서명·캐시 갱신을 호출하지 않고 기존 원문 바이트만 반환한다. 캐시 없음/손상/다른 작업/권한·접속·조회창 거절 시 새 결과/스캔을 만들지 않는다.
- 신규 `test_fingerprint_enrollment_online.py`: 독립적인 합성 서버 서명기·가짜 승인 중계와 기존 가짜 센서/임시 SQLite로21개 검사를 추가했다. 기존 어댑터/코어/읽기 검사55개에 포함된 중복 수치는 합산하지 않는다.

승인 제공자는 한 작업·한 scope·한 호출 수명으로 생성하고 반환/실패 시 `finally`에서 무효화한다. 취소/무효화 뒤 늦은 승인 응답을 거절하고 새 호출/재시작에 이전 승인이나 probe를 재사용하지 않는다. 갱신 실패 시 이전 유효 승인을 fallback으로 쓰지 않는다. 키/로그인/대상·장치·작업이 바뀌면 PC 소유자는 진행 중 cancellation을 취소해야 하며 새 호출은 새 승인 수명을 갖는다.

기본 승인 시계는 Linux `CLOCK_BOOTTIME`을 사용해 절전 시간도 포함한다. 이 시계가 없는 환경에서는 승인 실패로 차단한다. Windows 대역 검사는 명시적인 가상 단조시계를 주입했다. 임의 wall clock이나 항상True 공급자를 운영 대체물로 설치하지 않는다. 승인 자체는 물리 저장/권한 회수를 원자적으로 묶지 못하므로 저장 뒤 불확실성은 기존 ledger의 복구 필요로 남긴다.

### PC 중계 접점과 호출 예

`exchange(enrollment_id, seven_field_body, cancellation)`는 동기 callback이며 1번의 현재 관리자/원래 발급 접속으로 PC가 `POST /api/v1/device-enrollments/{id}/approval`을 중계하고 응답의 `approval_envelope`를 정규base64url decode한 **원문 bytes**를 반환해야 한다. body는 operation_revision/reserved_slot/claim_revision/key_epoch/ticket_digest/probe_nonce/scope 정확히7항목이다. PC가 서명/nonce를 만들거나 Pi에 Bearer를 넘기지 않는다. 중계자는 동시 작업/접속·서버 변경과 취소를 고정해야 한다. 이 callback의 실제 PC HTTP 송수신/비동기 중계는 이번 파일에서 대신 구현한 것이 아니다.

```python
# keys/sensor/lock/profile/ledger/cache와 PC 중계 callback은 실제 배포 준비 후 주입.
adapter = EnrollmentAdapter(
    sensor=sensor, codes=codes, sensor_lock=shared_sensor_lock,
    ledger=ledger, profile=explicit_sensor_profile, keys=selected_keys,
    cache=private_signed_result_cache,
)
signed_bytes = adapter.enroll_online(
    original_request_bytes, cancellation_event, exchange=pc_approval_exchange,
)
# 원래 요청 만료 후에도 새 result_read 승인을 얻어 기존 원문만 반환.
same_signed_bytes = adapter.read_result_online(
    enrollment_id, cancellation_event, exchange=pc_approval_exchange,
)
```

반환 바이트를 PC가 base64url 포장해 complete로 전송하고 **서버 completed**를 확인해야 계정 연결 완료다. 같은 원문 complete 재전송은 서버가 이미 확정한 결과를 되돌려주는 경우와 만료/취소 뒤 처음 들어온 미확정 센서 보고를 구분한다. 후자는 자동 매핑하지 않고 복구 대상으로 남는 1번 계약을 유지한다. 읽기 callback이 허용됐다고 서버의 보관/상태/기한을 재설정하거나 늦은 첫 완료를 승인하지 않는다.

### 이번 검증·게시 상태

Windows/Python3.13.15에서 다음 명령으로 **76개 중75통과/1생략**(기존55개+신규21개)을 확인했다. 생략은 기존 POSIX 소유자/권한 검사이며, 이 새 코드를 Pi에서76개 실행한 결과는 아직 없다.

```text
.\Tools\HandPresence\.venv\Scripts\python.exe -X utf8 -W error::ResourceWarning -m unittest discover -s Devices/RaspberryPi -p 'test_fingerprint*.py' -v
```

추가로 PR44 `ba6a37a`의 **실제 순수 approval.sign**을 메모리에서 로드한 합성 교환으로 우리 enroll_online→서버 순수 verify_completion 통과를 확인했고, 그 approval.sign의 result_read 승인으로 만료 후 같은 바이트/센서 호출0회 반환도 확인했다. 이는 순수 코드 바이트/바인딩 호환성 검사이며 HTTP/MySQL/실제 권한 DB·운영 키/TLS·실물 센서 검증이 아니다. py_compile·git diff --check와 `.enrollment-state/` Git 제외도 확인했다.

남은 작업: PC의 실제 비동기 승인/등록 중계·진행/취소 API·관리자 버튼, 실패 envelope 전달, 실물 채취/최악시간·전원장애, 두PC·키배포/TLS·공유 적용·운영 권한/보관/복구 정책이다. B/C 모듈은 로컬 미커밋이며 이번에 직접 Pi 서비스에 적용하거나 원격 push/PR 생성·협업 댓글 전송을 하지 않았다. 날짜 LOG/새 협의 MD·LOG PR은 만들지 않았고 이전 상태 기록은 각 시점으로 보존했다.

### 사용자 수행 B/C Pi 검증 — 2026-10-20

사용자가 변경된 어댑터·새 승인 모듈/검사를 Pi의 분리된 enrollment_lab에 복사한 뒤 기존 finger-env에서 `python3 -m unittest discover -s . -p 'test_fingerprint*.py' -v`를 직접 실행했다. 제공된 전체 출력은 **76개/0.262초/OK, 실패·생략0개**다. 신규21개와 기존 POSIX 권한 검사도 모두 ok이며 Windows75통과/1생략과 별도 환경 결과로 기록하고 합산하지 않는다. 최초 온라인 승인/UTC 연결·scope/nonce/시간/권한 차단·만료 후 동일 결과 바이트 반환을 가짜 서버 응답/센서와 임시 DB로 검증한 결과다. 실제 PC HTTP 중계·관리자 화면/실물 지문 등록·서버 계정 매핑 통합·운영 서비스 활성화를 수행한 것으로 확대하지 않는다. 로컬 코드 미커밋·포크 미게시/PR 미생성 상태는 유지한다.

## 2026-10-20 후속 — C# 중계·전용 등록창·Pi 비동기 작업 경로

사용자 요청으로 5번 소유 C# 중계와 전용 등록창·Pi 작업 통신을 준비했다. 원본 alpha `6cb4d5d`/LOG `99cd6f8`/PR `66f287b`, PR44 `ba6a37a`의 계약과 기존 생성 ID 접점을 대조했다. **1번 소유 UserManagementForm/MainForm·공통 ApiClient·서버/DB 파일은 변경하지 않았다.** 기존 관리자 화면에 실제 버튼을 붙이는 작업은1번 접점 연결 대기이며 이 전용 창을 현재 앱에서 이미 열 수 있다고 표시하지 않는다.

### 구현·파일

- `RemoteWork.WinForms/Features/Attendance/FingerprintEnrollmentRelay.cs`: 고정 직원ID/관리자 CaptureSession·대상 취소를 받는 `EnrollmentTarget`, 같은 client_request_id/서버 작업을 유지하는 `EnrollmentAttempt`, 허용 장치→발급/by-request→Pi 승인 요청 중계→complete/조회→서버 completed를 처리하는 릴레이. 등록 발급은1번의 실제200계약을 따른다. 현재 접속/대상이 바뀌면 늦은 응답을 거부하고 같은 시도의 동시 실행을 막는다.
- PC는 원문 메시지를 base64url 포장 상태 그대로 전달하고 장치용 MAC키/서명기를 갖지 않는다. 서버 요청은 공통 Session, Pi 요청은 별도 HttpClient를 사용해 관리자 Bearer가 Pi에 붙지 않는다. 승인7필드의 현재 작업·슬롯/revision/epoch·scope를 먼저 대조하고 digest/nonce 형식을 확인한 뒤 공통 서버에 중계한다. 최종 서명/권한 판단은 Pi/서버 책임이다.
- 발급 응답 유실은 같은 client_request_id의 by-request로 확인한다. Pi에 보내기 전에 DeviceStarted를 남기고 불확정 전송 후 재시도는 읽기 전용 경로로만 보낸다. 결과를 받은 뒤 complete 응답이 유실되면 같은 결과 원문/기존 작업 조회를 사용하며 새 UUID/스캔을 만들지 않는다. 서버 completed 전에는 성공으로 표시하지 않는다. 이 PC 시도 상태는 메모리 전용이고 앱 재시작 복구 UI/별도 운영 보관 정책은 아직 없다.
- `FingerprintEnrollmentForm.cs`: 읽기 전용 대상ID, 허용 장치, 명시적 시작/같은 작업 다시 확인/취소 요청과 진행 상태를 제공한다. 작업 중 장치 변경/중복 클릭을 막고 닫기·대상 취소·로그아웃 뒤 업데이트를 차단한다. 세션 취소 콜백은 동기 UI Invoke를 하지 않는다. 센서 stored만으로 완료 표시하지 않는다. 호출자가 대여한 채널을 해제하기 전 `PendingOperation`을 await해 취소 정리를 기다릴 수 있다.
- `Devices/RaspberryPi/fingerprint_enrollment_relay.py`: RAM 작업 broker `EnrollmentRelayJobs`와 선택 Flask 경로. 등록 요청을 검증한 뒤 별도 worker가 기존 어댑터를 실행하고 승인 callback을 짧은 조회/응답 왕복으로 중계한다. 완료 결과만 기존 private 캐시에서 반환한다. 한 probe의 응답 ACK 재전송은 같은 hash일 때 중복 효과가 없고 다음 probe에 적용하지 않는다. 취소는 worker 이벤트/승인 대기를 깨우며 물리 삭제/서버 매핑 취소를 대신하지 않는다.
- `pi_api.create_app(fingerprint_service, motion_feed, enrollment_jobs=None)`에 명시 주입 접점을 추가했다. 기본 main/기존2인수 호출에는 새 route가 없으며 `/health`·`/motion`·`/scan` 기본 동작은 유지한다. 실제 적용자는 같은 UART/sensor·FingerprintService.lock과 기존 표시 소유를 공유하는 어댑터/작업 broker를 준비해야 한다. 모듈만 복사하거나 기존 main을 실행한다고 새 등록 경로가 열리지 않는다.

### 5번 PC↔Pi 작업 계약

실제 서버의 `/api/v1/device-enrollments`와 별개인 Pi 전용 경로다.

| 경로 | 입력/역할 |
|---|---|
| POST `/enrollments/jobs` | relay_id·enrollment_id UUID, mode=enroll/read_result, request_envelope(등록은원문base64url/읽기는null) |
| GET `/enrollments/jobs/{relay_id}` | running/awaiting_approval/succeeded/delivered/failed와 승인7필드 challenge 또는 완료 원문 결과 조회 |
| POST `/{relay_id}/approval` | probe_nonce·approval_envelope(base64url), 동일 probe/같은 hash ACK는 같은 효과 |
| POST `/{relay_id}/cancel` | Pi worker 취소 신호. 서버 cancel·물리 삭제 완료를 뜻하지 않음 |

모든 요청은 PC가 작업마다 생성한32바이트 RAM transport capability를 `X-Enrollment-Relay`로 보낸다. 이는 공통 로그인 Bearer/장치 MAC키가 아니며 일반 로그/파일/Git에 남기지 않는다. challenge와 결과를 다른 시도에서 읽거나 취소하지 못하게 바인딩한다. 실제 쓰기는 여전히 서버의 새 서명 승인과 Pi MAC/현재성 검증이 필요하다. capability/MAC은 암호화가 아니므로 실제 배포 전 합의된 신뢰 TLS/전용 연결이 필요하고 이번에 운영 transport를 활성화하지 않았다.

완료 결과의 transport 사본은 최종 검증된 승인에서 계산한 **보수적 잔여 유효기간 안에서 한 번만 전달**한다. 사용한 뒤/시간 경과/취소 시 RAM 결과를 지우고 delivered로 남긴다. 옛 capability로 결과를 계속 재조회하지 못하며 응답 유실은 새 result_read 작업/새 probe·서버 현재 권한 승인으로 private 캐시에서 다시 읽어야 한다. 승인 만료 예외를 주거나 전체 보관기간을 transport 토큰 유효기간으로 사용하지 않는다. 전달 시계도 승인 시계와 같아 suspend/역행을 검사한다.

PC의 개별 HTTP 제한12초는 유지하고 전체 Pi 작업 대기는 별도 비동기150초 예산이다. 이는 서버 ticket TTL/운영 정책 확정이 아니다. 승인 응답 왕복은 기존2초 제한을 따르므로 더 느린 전달은 차단된다. 실제 채취/PC·서버 지연 실측은 아직 없다.

### 1번 관리자 화면 주입 예 — 미연결 접점

PR44의 생성 응답 보관 접점을 소유한1번이 **UI 스레드에서** CreatedUserEnrollmentTarget을 확인하고 새 창을 열 수 있다. 아래는 실제1번 파일에 반영한 코드가 아니라 연결 예다. Pi주소↔허용 장치 배정은 운영 설정과 함께 고정해야 하며 임의 회원/목록 첫 행으로 대상을 바꾸지 않는다.

```csharp
var created = EnrollmentTarget; // 1번 UserManagementForm의 현재 생성 응답 접점
created.ThrowIfChanged();
var target = new RemoteWork.WinForms.Features.Attendance.EnrollmentTarget(
    created.UserId, created.AdministratorSession,
    () => !created.Cancellation.IsCancellationRequested, created.Cancellation);
using var channel = new HttpEnrollmentPiChannel(configuredPiAddress);
using var view = new FingerprintEnrollmentForm(target, channel);
view.ShowDialog(this);
await view.PendingOperation; // 닫기/취소 후 빌린 채널 해제 전에 정리 대기
```

실제 Pi에서는 키/허용 프로필·상태 경로를 비공개 설정으로 준비한 뒤 `EnrollmentRelayJobs(adapter, approval_clock=suspend_aware_clock)`를 구성하고 `create_app(fingerprint, feed, jobs)`에 명시 전달한다. 기존 service의 main/설정을 이번에 대신 교체하지 않았다. 승인 gateway에 PC의 Bearer를 보내거나 test public key를 운영에 설치하지 않는다.

### 실제 검증·제한

- C# `FingerprintEnrollment.Tests`: 통제 HTTP대역86검증과 실제 loopback HTTP의 C#→Python 작업 broker·서명 승인/센서대역·결과 검증/같은 원문 재조회까지 포함해 **최종114검증 통과**. 86/114는 포함 수치로 합산하지 않는다. 일반 서버 API/권한 DB는 통제 대역이며 실제 공통 서버/MySQL을 실행한 검증이 아니다. 실제HTTP 대조 과정에서 JsonContent의 chunked전송과 body 길이 제한이 맞지 않던 것을 고정 길이 StringContent로 수정한 뒤 통과했다.
- C# `FingerprintEnrollment.Ui.Tests`: **실제 WinForms12검증 통과**, UI 스레드 검사·대상 고정·중복 시작 차단·서버확정·닫기/대상 취소·로그아웃/늦은 결과 차단. 별도 기존 MainForm/계정생성 화면의 실제 주입 검증은 아니다.
- Python 전체 Pi 검사: 저장소 지정 Flask3.1.3/tzdata2026.5를 준비한 별도 임시 Python3.13.15 가상환경에서 `python -X utf8 -W error::ResourceWarning -m unittest discover -s Devices/RaspberryPi -p 'test_*.py'` → **109개 중108통과/1생략**. 생략은 기존 POSIX 소유자/권한 검사다. 새 job broker/Flask opt-in 경로와 기존 scan/motion/이름·판정 회귀, transport 결과의 일회 전달/승인 잔여시간 만료를 포함한다. 기존 hand 검사환경에서는 Flask 부재로10개 API setup이 실패했으며 환경을 바꿔 최종 통과한 이력이다. 앱/테스트 논리를 제거해 통과시킨 것은 아니다.
- .NET8 Windows 대상 새 코드 Release 빌드 경고0/오류0, py_compile·git diff --check를 확인한다. 구체 실행은 Tests/FingerprintEnrollment.Tests와 .Ui.Tests README를 따른다. 새Pi검사109개를 실제 라파에서 실행한 결과는 아직 없고 앞선 사용자76개 성공과 구분한다.

이번 변경은 로컬 미커밋이다. 실제 키/장치·배선·기존 지문/실명 자료·서버/공유DB를 수정하지 않았고 실제 지문 등록/삭제·전원장애·두PC/TLS·운영 배포는 미실행이다. 관리자 버튼은1번 연결 대기, 실패 envelope/앱 재시작의 PC 복구·운영 정책은 후속이다. 코드 push/새PR·원격 협업 댓글 전송은 하지 않았다. LOG 날짜 기록은 팀장 전용이며 이 코드 문서에 실제 작업/검증 상태를 남겼다.

## 2026-10-20 게시 승인·1번 화면 연결 인계

사용자가 코드 push·원본 alpha 대상 PR 게시를 승인했다. 업로드 전 원본 alpha `deedce0`, LOG `1917654`, PR `c2611f1`을 받아 확인했으며 원본 alpha의 새 README 검토 기록을 로컬에 실제 병합했다. 이 최신 변경은 README뿐이고 위 검증을 마친 제품/테스트 소스는 동일하다. 결합 상태에서 같은 명령으로 Python 전체109개(108통과/1 POSIX 생략), C# 중계114검증, 실제 등록창12검증, Release 빌드 경고0/오류0을 최종 재확인했다. Python 출력의 `센서 오류: sensor error`는 예외 처리 대역 검사의 의도된 출력이며 실물 장치 오류가 아니다. 앞 절의 로컬 미게시 표시는 각각 작성 당시 기록이다. 실제 push·PR 주소·원본 병합 여부는 게시 후 PR 본문과 [기존 통합 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6281140854)에 구분해 전달한다.

[1번 최신 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6284035607)은 관리자 등록 버튼·생성 ID 고정·공통 조립을 1번이 연결하는 분담을 수용했다. 1번 PR44 최신 head `75a8259935490cd70ef598fea7c64b7dbac6016c`는 아직 미병합이다. R44-01의 후보 조회 수정은 작성자 재검토 대기 보고이며 5번이 실제 MySQL로 재검증하거나 승인한 것이 아니다. 서버 코드를 이번 PR에 복사/병합하지 않는다.

### 게시하는 실제 타입·소유권

```csharp
EnrollmentTarget(int userId, ApiClient.Session administratorSession,
    Func<bool> isCurrent, CancellationToken cancellation)
FingerprintEnrollmentForm(EnrollmentTarget target, IEnrollmentPiChannel pi)
Task FingerprintEnrollmentForm.PendingOperation { get; }
bool FingerprintEnrollmentForm.RegistrationCompleted { get; }
HttpEnrollmentPiChannel(string address, HttpMessageHandler? handler = null)
```

target와 channel은 호출자 소유이며 폼은 빌려 사용한다. UI 스레드에서 현재 생성 응답을 확인해 폼을 열고, 종료 뒤 `await view.PendingOperation`을 거친 후 channel을 Dispose한다. 동기 Wait/Result로 UI를 막지 않는다. 현재성 콜백은 백그라운드에서도 호출되므로 컨트롤에 직접 접근하지 않아야 한다. 로그아웃/접속·대상 수명 취소와 현재성 검사를 함께 연결한다.

`IEnrollmentPiChannel.TransferAsync(EnrollmentJob job, bool readOnly, Func<JsonElement, CancellationToken, Task<string>> approve, IProgress<string>? progress, CancellationToken cancellation)`은 동일 원문의 base64url 결과 `Task<string>`을 반환한다. 제품 PC는 MAC 키를 갖지 않으며 승인 요청은 원래 관리자 Session을 통해 중계한다.

**장치별 주소 연결은 추가 대조가 필요하다.** 현재 폼은 서버의 허용 장치를 직접 조회해 선택하며, 생성자에서 채널 하나를 받는다. `HttpEnrollmentPiChannel` 하나는 주소 하나에 고정된다. 따라서 여러 장치가 있는 목록에 고정 주소 채널 하나를 무조건 연결하면 안 된다. 1번의 공통 조립부는 선택된 `EnrollmentJob.DeviceId`/현재 서버에 맞는 채널을 제공하는 구조와 등록 시작 전 설정 누락 검사를 대조해야 한다. 게시만으로 이 구성이 구현됐다고 표시하지 않는다.

1번이 제안한 별도 PC 로컬 `enrollment-endpoints.json`은 현재 제안이며 이번에 파일/로더를 설치하지 않는다. 기존 `client.json`에 중첩 객체를 추가하지 않는 방향을 수신했다. 현재 채널은 `ApiClient.ValidateUrl`을 사용하므로 주소는 **http/https의 루트 base URI**이고 사용자정보·경로 접두사·query·fragment는 허용하지 않는다. TLS 검증을 끄지 않는다. 다장치 주소 선택·누락/서버 불일치 안내는 1번과 접점 대조 후 검증한다.

인계 후 확인할 것은 실제 관리자 버튼/생성 ID·접속 수명, 장치 주소 구성, 공통 서버+Pi 결합 및 승인된 실물 두 채취→매핑이다. 등록 성공용 서명 결과 이외 실패 envelope, PC 재시작 복구 UI, 실제 서비스 조립/키·TLS 배포·운영 복구/보관 정책은 여전히 후속이다. LOG 날짜 기록은 게시하지 않고 팀장에게 기존 협업 댓글/이 검증 기록으로 전달한다.
