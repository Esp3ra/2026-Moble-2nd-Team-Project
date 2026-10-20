# AS608 새 지문 등록 코어 — 5번 로컬 준비

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

- `EnrollmentOperation`: 소문자 UUID `enrollment_id/device_id`, 직원 INTEGER `target_user_id`, 정규 양의 BIGINT문자열 `operation_revision`, 정수 `reserved_slot`, 원문 티켓SHA256 `ticket_digest`. 이것은 코어 입력이며 새 공통 wire DTO 확정안이 아니다.
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
