# 5번 본인 확인 설정·실행 조립 준비 — 2026-10-22

작성자 5번 / 전달 대상 1번·팀장. PR63 HTTP 후속의 로컬 구현·협업 기록이다. 실제 서비스 전환이나 운영 키 설치 승인이 아니다. 날짜 LOG는 팀장 취합 범위를 유지한다.

## 배경·대상·영향

기존 `pi_api_enrollment_mysql.py`가 단일 UART/OLED와 MySQL 등록을 소유한다. 새 인증의 네 키를 기존 등록 키와 분리해 읽고, 같은 `FingerprintService`를 빌려 HTTP runtime을 조립한다. 대상은 새 `identity_private.py`, `identity_runtime.py`, 소유 실행기 `pi_api.py`의 선택 factory·종료 순서와 검사다. 공통 서버/API/DB·LoginForm/MainForm을 변경하지 않는다. 새 로컬 DB·상태 파일·키 생성·별도 UART·서비스 설치는 없다.

## 비공개 설정 형식

별도 파일의 정확한 필드는 `version`, `contract`, `device_id`, `key_id`, `key_epoch`, `profile`, `keys`다. version은 정수2, contract는 `identity-rw-id-v2`, key_epoch는 v2의 정규 양의 정수 **문자열**이다. profile은 `capacity`, `minimum_slot`, `maximum_slot`. keys는 `id_request`, `id_probe`, `id_approval`, `id_result` 각각의 정규 base64url 32byte 키다. 예제 운영 키·완성 JSON은 Git에 두지 않는다. 설정에는 활성화/재시작 승인 플래그가 없다.

신뢰 소유자가 이미 검증해 로드한 `EnrollmentConfiguration`을 별도 인자로 공급한다. device/capacity가 같고 슬롯 범위가 기존 등록 프로필 안에 있어야 한다. 기존 request/result 키는 자동 제외하며, 추가 구형/회수 키는 신뢰 소유자가 `retired_keys`로 공급한다. 새 JSON이 제외 목록을 선택하지 못한다. 같은 키·공개 시험 키·구형 키 재사용을 거부하고 키는 repr/오류/출력에서 숨긴다.

실제 파일 loader는 Linux에서만 기존 비공개 파일 검사를 재사용한다. 절대 경로·symlink 금지·부모 폴더와 파일의 현재 사용자 소유·그룹/기타 접근 금지·일반 파일/단일 hardlink·16KiB 제한을 확인한다. 파일/권한을 자동 생성·수정하지 않는다. 검사 전용 CLI는 명시 `--check-config`를 요구하며 센서/서버/DB/서비스를 열지 않는다.

## 실행·재시작 차단

`pi_api.main(..., identity_factory=...)`는 기존 센서 객체를 선택 factory에 전달하고, 반환된 runtime의 `mount(app)`를 서버 시작 전에 호출한다. 기존 등록 factory와 함께 쓸 수 있다. 기본 호출과 기존 MySQL 실행기는 바꾸지 않아 새 경로가 자동으로 켜지지 않는다. factory 실패·바인딩 실패·종료 때 인증→등록→조도→센서 잠금 drain→UART close 순서로 정리한다.

`IdentityRuntime`은 RAM pool을 프로세스 수명 동안 한 번만 만들고 여러 app에 재장착하지 못한다. `ready`는 신뢰 실행기 소유의 **로컬 주입 접점** `ready(profile) -> bool`이다. 서버 endpoint/서명 형식/새 정책 합의가 아니다. 기본값은 차단이고, 이 콜백이 없다면 모든 새 인증 경로가503 `IDENTITY_DEVICE_NOT_READY`다. 예외·True 이외의 반환도 차단한다. 매 HTTP 요청에서 검사하며, 실패하면 그 runtime은 영구 차단·자기 작업 취소한다. 이후 콜백이 True로 바뀌어도 같은 runtime을 다시 열지 않는다.

이 접점은 **1번의 실제 서버 상태 재조정 구현을 대신하지 않는다**. 콜백은 이미 검증된 로컬 준비 상태만 짧게 읽으며 네트워크/센서 I/O나 runtime 재진입을 하지 않는다. 실제 실행기에서 공급하려면 이전 프로세스의 미확정 challenge/결과 확인, 이전 요청 재채취 금지와 새 요청 허용을 서버와 대조해야 한다. 단순 health200·설정 검사·상수 True를 운영 콜백으로 사용하면 안 된다. 그 제공 전에는 실제 MySQL 실행기에 factory를 연결하지 않는다. 새 재조정 API/토큰/설정명을 임의로 정하지 않는다.

## 확인 요청·합의·검증 상태

1번의 [PR63 HTTP 검토](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/63#issuecomment-6312104306)는 수신했다. 새 설정 형식/로컬 readiness 접점은 5번 구현 범위이며 실제 서버 재시작 조정·공통 adapter와의 결합 수용은 대기다. 1번은 기존 v2 API 제공 시 재시작 뒤 이전 요청 금지와 결과 조회/취소의 실제 연결 방법을 함께 대조해 달라. 이미 제공한 HTTP 전달부를 다시 미제공으로 분류하지 않는다.

## 직접 검사와 현재 상태

Windows / Python3.13.15 / .NET SDK10.0.302(net8.0-windows), 합성 설정·가짜 UART·소유 loopback만 사용했다.

- 미추적 구형 v1 파일6개를 제외한 Git export+이번 Pi5파일에서 `python -m unittest discover -s Devices/RaspberryPi -p 'test_*.py'`: **251 실행 /248 통과·Linux 전용3 생략,5.490초**. 구형6파일을 바꾸거나 검사에 섞지 않았다. 새 검사23개(설정9/runtime14)는 전체에 포함된다. Linux flock2개·실제 파일 ACL/owner/link1개를 Windows에서 생략했다.
- 새 검사: 엄격 설정/장치·slot 정책·키 분리/회수 키·출력 비노출, 기본 차단/예외·회수 뒤 재개 금지, 같은 pool과 고정 결과, 기존 MySQL 등록 경로·scan·조도 보존, 두 runtime의 종료/실패 정리·진행 중 UART 잠금 drain. 처음 `/scan` 검사에서 대역 confidence가 Mock이어서500이 발생한 것은 시험 입력을 정수로 보완했고, 실패 실행을 통과 수에 합산하지 않았다.
- `dotnet run --project Tests/FingerprintIdentity.Tests -c Release`: **143 assertions 통과**.
- `dotnet run --project Tests/FingerprintIdentity.Http.Tests -c Release -- --python <시험 venv python.exe>`: **110 assertions 통과**. loopback 호스트가 새 runtime의 mount/close를 사용한다. readiness는 시험 전용 대역이며 서버 재시작 조정 구현이 아니다.
- Release 빌드 **경고0/오류0**, `git diff --check` 통과.

**미실행:** 실제 비공개 파일/키·Linux ACL/CLOCK_BOOTTIME·서버 재조정/새 v2 API/MySQL·기존 Pi 서비스의 실제 factory 연결/전환·실센서·두PC·로그인/근태 finalize. 실제 설정 검사 CLI도 개인 파일에 실행하지 않았으며 시험에서 loader/CLI의 읽기 경계만 검증했다. 기본 MySQL 실행기는 그대로여서 새 인증 경로가 자동 활성화되지 않는다.

로컬 구현·검사 완료 / 사용자 게시 승인 전 / 포크 push·PR 갱신·외부 회신·원본 병합·실물 배포 미실행이다. 커밋은 작업 보고에 연결하고, 게시 승인 후 기존 PR63·PR58 대표 댓글을 갱신한다. 기존 지문·회원 매핑·키·서비스/DB는 이번 로컬 준비로 변경하지 않았다. 날짜 LOG는 직접 게시하지 않았다.
