# 실제 단일 서비스 연결·전환·복구

5번 구현. 기존 서버 wire/API/DDL·등록/인증 키와 계정 매핑은 변경하지 않는다.
`pi_api_identity_service.py`가 기존 `ready(profile)` 접점을 실제 프로세스에 조립한다.
등록·인증은 기존 `pi_api.main`의 단일 FingerprintService/UART/OLED를 빌리고 같은
MySQL `runtime.lock`의 획득된 RuntimeLease를 서비스 종료까지 보관한다.

## 관측파일만으로 시작하지 않는 구조

1. 기존 strict loader로 등록/인증 설정, 관리범위 회수 목록·현재 profile/epoch/범위를 검사한다.
2. 별도로 검토한 서버3소스 해시와 같은 cycle의 원 작업/fence 관측을 대조한다.
   실제 ON을 보고한 새 관측과 미확정0이 필요하다. 과거 issued/verified 미완료를
   시간 경과나 빈 응답으로 지우지 않는다. 종단 작업의 fence는 계속 보존한다.
3. root 전환 도구가 실제 이전 서비스의 SIGINT/45초 설정·정상 종료0·PID 소멸·
   cgroup v2 전체 비움·동일 runtime.lock 획득 가능을 확인한다. 실패하면 새 실행기를 설치하지 않는다.
4. root가 측정한 OS 인계 자료를 `/run/remotework-identity/handoff.json`에 root:서비스그룹
   0640/부모0750으로 만든다. 사용자 전달 JSON이나 ready 설정이 아니며 키/토큰은 없다.
   기존 양의90번 hold/release는 그대로 둔다. 검토된 실행 소스는 root 소유 `/opt`에 고정한다.
5. 같은 단일 unit의 root ExecStartPre가 그 자료를 이번 systemd INVOCATION_ID에 **한 번만**
   연결한다. 이후 재시작·재부팅·다른 invocation은 재사용할 수 없다.
6. 실제 진입점이 root 자료/boot/invocation/profile/모든 입력 해시와 systemd의 현재
   `MainPID`·`InvocationID`·cgroup 소속을 검사한 후 기존 lock을 획득하고 단일 센서를 연다.
   호출자 환경의 `INVOCATION_ID`만으로 승인하지 않는다. callback도 같은 profile
   인스턴스·현재 PID/start tick/boot·systemd 소속·살아 있는 lease·입력 불변과 종료 여부를
   동기 검사한다. 센서 채취는 여전히 각 원 요청의
   최신 capture/result 서버 승인/HMAC을 통과해야 한다.

따라서 파일 전달·health200·원 작업0·시간 대기·CLI 플래그 하나만으로 True를 만들지 않는다.
루트 OS 인계 자료는 새 서버 ACK/endpoint/키나 서버 제공 ready 증표가 아니다. Pi 내부 구현
접점이며 적용 전 코드/운영 결합 검토가 필요하다. 현재 OFF 묶음으로 apply하면 서비스 정지 전에
`server_off_or_original_work_unresolved_no_stop`으로 거절한다.

서버3소스 대조 파일은 내용 SHA를 포함한 `server-sources-SHA256.json`으로 보관한다.
같은 Pi 소스 버전에서 서버 코드만 변경되어도 이전 회차 파일을 덮어쓰지 않는다.
현재 대조에 선택한 정확한 파일을 probe·root 인계 입력·서비스 인수에 동일하게 전달한다.
이전 고정 이름 파일은 증거로 보존하며, 같은 내용 주소의 파일이 변조되어 있으면 계속 거절한다.

## 실행 중 닫힘

`StartupReadiness`는 설정/소스/root 자료·프로세스·lease 변경 또는 종료 시 영구 닫힌다.
기존 IdentityRuntime은 이후 HTTP를503으로 거절한다. 별도 watcher도 PC poll 없이 준비
철회를 발견해 기존 작업을 취소하고 drain한다. 같은 프로세스에서 원 자료를 복원해도 다시 열지 않는다.
종료 순서는 준비 철회→인증/등록 drain→조도 중지→같은 센서 lock 안 UART close→lease 해제다.
기존 등록/조도/health의 기능과 기존 지문/매핑을 재초기화하지 않는다.

OS 확인은 고정 `/usr/bin/systemctl --system show attendance-device.service`를 1초 제한과
최소 환경으로 실행한다. 호출자의 PATH·DBus 주소·loader 설정은 전달하지 않는다.
실제 unit의 주 프로세스와 현재 PID, OS 실행 회차와 환경 값, cgroup v2 소속이 모두 같고
unit이 active/activating인 경우만 허용한다. 조회 실패·시간 초과·필드 누락/중복도 차단한다.
첫 프로세스가 종료되어 lock이 풀려도 새 프로세스가 옛 환경/인계 자료를 재사용할 수 없다.
root `ExecStartPre claim`의 기존 단일 회차 연결과 전환·복구 순서는 유지한다.

**원격 서버 프로세스 교체를 Pi가 자동 감지한다는 뜻은 아니다.** 기존 wire에 원격 process
attestation은 없으며 새 endpoint를 추가하지 않았다. 계획된 서버 설정·키/프로세스 변경 시
운영자가 Pi 인증 서비스를 먼저 중지하고 새 회차를 대조한다. 이후 각 채취/결과의 실제 현재
인가·epoch·mapping·원 challenge/fence 재검사는 기존 서버/승인 프로토콜이 담당한다.
이 운영 순서는1번과 함께 적용 전에 확인해야 한다.

## 준비된 도구와 실제 적용

개인 전달 준비기가 만드는 manifest는 source commit/실제 파일 SHA·기존 경로/검토 서버
소스 해시뿐이다. 키를 새로 만들거나 Git에 넣지 않는다. 다음은 Linux 전용 명령 형식이다.

```sh
# 서비스/센서를 건드리지 않는 실행 계획
python3 identity_transition.py plan --manifest /PRIVATE/transition-manifest.json --manifest-sha256 SHA256

# 상호 준비·서버 ON의 새 회차·검토 완료 후에만 사용
sudo /usr/bin/python3 -I identity_transition.py apply --manifest /PRIVATE/transition-manifest.json --manifest-sha256 SHA256

# 도구가 출력한 정확한 root 백업으로 명시 복구
sudo /usr/bin/python3 -I identity_transition.py rollback --backup /var/lib/remotework-device/transitions/BACKUP
```

apply는 보호된 코드 설치/strict 현재 설정 대조·OFF/미확정 거절을 먼저 수행한다. 실제 교체는
기존 단일서비스 정지→정상 drain→새80번 override/단일 invocation 연결→같은8080의
health/motion/identity OPTIONS200 확인이다. 새로운 별도 서비스를 띄우지 않는다.
기존60/90번 파일·release·설정/키·센서 저장/삭제·계정/매핑/DB는 변경하지 않는다.
화면용 로컬 이름표가 있으면 장치 표시용으로만 보존하며 계정 연결 근거로 쓰지 않는다.

실패 시 새 admission 철회→후보 정지·drain/같은lock 해제 확인→**우리의 정확한80번 override만**
제거→검증한 직전 MySQL entry로 복구→health/motion/등록 OPTIONS 확인 순서다.
강제 종료·남은 작업/lock·원 파일 변경이 있으면 자동 fallback을 하지 않고 중단한다.
SQLite 등 폐기 writer를 재시작하거나 evidence/지문을 삭제하지 않는다. root 인계 증거는
개인 root 백업에 보존한다. **Pi 복구가 서버 ON/OFF를 되돌리는 것은 아니므로 서버 소유자와
실패 시 OFF 복구를 함께 진행해야 한다.**

Restart=no이며 재부팅/새 systemd invocation은 옛 인계 자료로 인증을 다시 열지 않는다.
새 회차가 필요하면 정확한 root 백업으로 기존 MySQL 서비스를 복구한 뒤 새 자료로 다시 적용한다.
무인 재부팅 뒤 자동 readiness 공급은 현재 서버 계약으로 제공하지 않는다.

## 검사 범위

`test_identity_startup.py`는 cycle/boot/invocation/profile/키·입력·lease 불변과 영구 닫힘,
OFF/미확정 시 정지 금지, 정상 인계·실패 복구/미완료 drain 시 fallback 금지를 검사한다.
실제 `process_stamp()` 경로에 proc/systemd 대역을 주입해 정상 main process,
종료 후 새 PID의 옛 인계 재사용 거절, 다른 invocation/cgroup·비활성 unit,
OS 조회 실패/시간 초과/비정상 출력·환경 주입 차단과 소속 상실 후 영구 닫힘도 확인한다.
`test_pi_api_identity_service.py`는 실제 호스트 조립/동일센서·lease/종료 수명과
실제 Flask·기존 서명 승인 중계→센서 대역→같은 원문 결과 재생을 검사한다.
PC poll 없이 철회해 실제 대기 worker를 취소하는 것도 확인한다.

서비스/systemd/root 인계·UART는 해당 단위 시험에서 명시 대역이다. 실제 root 전환/복구,
실계정 출근·정오·퇴근/응답 유실/두PC는 별도 실물 검사이며 단위 시험 성공으로 표시하지 않는다.

## 2026-10-23 실제 시연 보고와 남은 확인

운영 진입점 소스661ac19a의 사용자 SSH/sudo 실행에서 Linux294검사와 실제 단일서비스 전환,
health/motion/identity OPTIONS200을 보고받았고, PC에서도 해당 HTTP 응답을 별도로 확인했다.
사용자는 실제 지문 등록·출퇴근과 1..12 고정 풀 적용 후 추가 회원의 정상 자동 등록을 보고했다.
이 실물 보고는 R89-01 수정 전 코드에 대한 기록이다. 이후 R89-01에서 systemd 실소속 검사를
추가했으며 수정본의 실제 Linux 서비스 전환은 아직 실행하지 않았다. 기존 지문/매핑·서명 결과와 fence,
근태를 보존했으며 정상 점유를 복구번호로 우회하거나 예약·격리를 자동 회수하지 않았다.

실제 정오·통신 단절/응답 유실의 원 UUID 복구·두 PC 전체 시나리오와 무인 재부팅은 이 보고로
완료 처리하지 않는다. 공유 MySQL 설정/백업·매핑 확인은1번 담당의 보고이며 단위검사와 구분한다.
