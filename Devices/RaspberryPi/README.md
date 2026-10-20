# 5번 라파 지문·조도 독립 시연

Raspberry Pi 한 프로세스에서 UART 지문 인식, SH1106 OLED 표시, Arduino Mega USB 조도 수신을 함께 실행합니다. 공통 회원·로그인·근태 DB와 분리된 장치 시연 코드입니다. Windows 확인 화면은 [Tools/DeviceCheck](../../Tools/DeviceCheck/README.md)에 있습니다.

## 구성과 배선

| 장치 | 연결 / 기본 설정 |
|---|---|
| 지문 센서 | Pi UART `/dev/ttyAMA0`, 57600 baud. 기존 정상 배선과 센서 등록 상태 사용 |
| SH1106 OLED | Pi I2C bus 1, 주소 `0x3C`, 128×64 |
| SZH-SSBH-011 | Mega VCC 5V, GND, AO→A0. DO 미사용 |
| Arduino Mega | Pi와 USB 연결. 현재 기본 포트 `/dev/ttyUSB0`, 115200 baud |
| 한글 글꼴 | `/usr/share/fonts/truetype/nanum/NanumGothic.ttf` |

Mega에는 [mega_light.ino](mega_light/mega_light.ino)를 업로드합니다. A0의 0~1023 값을 200ms마다 한 줄로 전송합니다. lux 측정값이 아닙니다. Mega의 5V 신호를 Pi GPIO에 연결하지 않고 USB 직렬 통신을 사용합니다.

Pi에서 I2C/UART 사용 설정과 실행 사용자의 장치 접근 권한을 준비해야 합니다. 포트가 다르면 `pi_api.py`의 `MEGA_PORT`·`FINGER_PORT`를 실제 장치에 맞춥니다. 확인 명령:

```bash
python3 -m serial.tools.list_ports -v
```

## 최초 실행

Python 3.11 이상을 사용합니다. 아래는 Pi에서 이 폴더 안에 있는 경우의 예입니다. 이미 센서가 동작하는 가상환경이 있으면 그대로 사용할 수 있습니다.

```bash
sudo apt install python3-venv fonts-nanum
python3 -m venv --system-site-packages .venv
source .venv/bin/activate
python -m pip install -r requirements.txt
python -m py_compile pi_api.py
python pi_api.py
```

I2C/UART 설정은 사용 중인 Pi OS와 기존 지문 센서 설정에 맞춰야 합니다. 패키지 목록은 설치 범위이며 실제 Pi의 전체 패키지 버전을 고정·재현 검증한 lock 파일은 아닙니다. Windows에서 하드웨어 패키지를 설치하지 않습니다.

`pi_api.py`를 직접 실행합니다. Flask CLI 또는 여러 WSGI 프로세스로 실행하면 `main()`의 장치 초기화/읽기 스레드 동작을 보장하지 않습니다. Flask 개발 서버를 사용하는 내부 시연이며 운영 배포용 인증 서버가 아닙니다. 센서 UART, Mega USB와 OLED를 읽는 구형 독립 프로그램·miniterm을 동시에 실행하지 않습니다.

이미 자동 실행 서비스가 켜져 있다면 수동 실행 전에 다음 명령으로 중지합니다.

```bash
sudo systemctl stop attendance-device.service
```

## 새 지문 등록 전: 센서 정보 읽기 전용 점검

[fingerprint_inventory.py](fingerprint_inventory.py)는 센서가 보고하는 용량·등록 개수·사용 중 번호와 설치된 지문 라이브러리 버전만 읽습니다. 지문 채취/원본·템플릿 읽기/저장·삭제·설정 변경은 하지 않습니다. 시스템 ID로 제조사·센서 모델을 확정하거나, 용량으로0번/마지막 등록 가능 번호를 추정하지 않습니다. `null`인 항목은 실물 라벨/제조사 자료 확인 전 미확인입니다. 목록/개수가 맞지 않으면 실패 처리하며 이 결과만으로 빈 슬롯을 할당하지 않습니다.

Pi의 기존 센서 가상환경에서 실행합니다. **수동 pi_api·등록 프로그램·miniterm도 모두 종료**해야 합니다. `exclusive=True`만으로 이미 실행 중인 모든 구형 프로그램의 점유를 발견한다고 보장하지 않습니다. 실행 전 서비스가 `inactive`인지 확인하며 자동 중지·재시작은 하지 않습니다.

```bash
# 서비스가 실행 중이었다면 점검 동안 중지하고, 끝난 뒤 다시 시작합니다.
sudo systemctl stop attendance-device.service
python fingerprint_inventory.py --confirm-sensor-idle
sudo systemctl start attendance-device.service
systemctl is-active attendance-device.service
curl http://127.0.0.1:8080/health
```

중간 조회에 실패해도 원래 실행 중이던 서비스는 마지막 명령들로 복구합니다. 이전에 정지해 두었던 서비스라면 점검 뒤 임의로 시작하지 않습니다. 센서 정보는 터미널에서만 확인하며 등록 목록 결과를 Git에 추가하지 않습니다. 실제 슬롯 등록/삭제·회원 연결·별도 enroll 서명/서버 API·재시작 복구는 이번 점검 도구에 없습니다. 1번의 PR44는 확인 시 미병합 계약 초안이므로 이번에 병합하거나 그 화면/공통 파일을 수정하지 않았습니다.

구현 참고: Adafruit 공식 [API](https://docs.circuitpython.org/projects/fingerprint/en/latest/api.html)의 `read_sysparam`, `count_templates`, `read_templates`. 설치 버전/실제 센서 응답은 별도 확인하며 읽기 점검으로 실물 등록 가능 범위나 회원 신뢰를 증명하지 않습니다.

장치 없는 로컬 검증: `python -m unittest -v test_fingerprint_inventory`. 이 대역 검증은 실제 UART/센서를 사용하지 않습니다.

## 표시 이름은 각 장치 안에만 보관

게시본의 `USER_NAMES`는 비어 있습니다. 없어도 `사용자 N`으로 센서 슬롯을 표시합니다. 표시 이름이 필요하면 [예제](user_names.example.json)를 `pi_api.py`와 같은 폴더의 `user_names.local.json`으로 복사해 장치 안에서만 수정합니다. 이 파일과 이벤트 CSV는 Git에서 제외합니다.

```bash
cp user_names.example.json user_names.local.json
nano user_names.local.json
```

기존 소스에 실명이 들어 있는 `USER_NAMES`를 사용 중이라면 교체 전에 기존 파일을 백업하고 그 표를 이 로컬 JSON으로 옮깁니다. 등록된 지문은 센서 내부에 그대로 있으며 이름표를 바꾸어도 지문 등록을 수행하지 않습니다. JSON의 키는 센서 슬롯 문자열이고 회원 DB의 사용자 ID가 아닙니다. 잘못된 파일은 시작 시 오류로 표시하며 내용을 로그에 출력하지 않습니다.

이번 게시 과정에서는 실행 중인 Pi 파일·이름표·서비스를 원격 수정하지 않았습니다.

## 장치 API

포트는 기본 8080입니다. 아래 호출은 Pi 내부에서 확인하는 예입니다. Windows에서는 `127.0.0.1` 대신 실제 Pi 주소를 사용합니다.

```bash
curl http://127.0.0.1:8080/health
curl http://127.0.0.1:8080/motion
curl -X POST http://127.0.0.1:8080/scan
```

| 요청 | 응답 / 의미 |
|---|---|
| `GET /health` | 200, `success`, `message`, `motion_available`. API 도달과 조도 수신 가능 여부. 지문 실인식 검증을 대신하지 않음 |
| `GET /motion` | 수신 가능200, 수신 대기/끊김503. `success`, `data`와 `Cache-Control: no-store` |
| `POST /scan` | OLED의 손가락 떼기→올리기 안내, 단계별 최대15초. 인식 성공200과 `success/finger_id/name/confidence` |
| 미등록 지문 | 기존 시연과 호환되게200 + `success=false`. HTTP200만으로 성공 판단 금지 |
| 사용 중 / 입력 초과 / 센서 오류 | 각각409 / 408 / 500, `success=false`, `message` |

`/motion`의 `data`에는 현재 `state/detected/raw_value`, 최근 변화 `motion_state/recent_motion/seconds_since_motion/last_motion_at`, 마지막 수신 시각 `observed_at`, `connected/available/error`, `thresholds`, `simulation`이 있습니다. 시각은 이 독립 시연의 KST ISO 8601입니다. 공통 API의 UTC·Bearer·envelope 규약으로 승인된 것이 아닙니다.

- **190 이상**: `COVERED`, `detected=true`.
- **186 이하**: `CLEAR`, `detected=false`로 즉시 해제.
- **187~189**: 이전 상태 유지. 시작 직후 이 구간만 수신하면 `LEVEL_UNKNOWN`, `detected=null`.
- 실제 상태 전환 후10초 미만: `RECENT_MOTION`, 이후 유효 수신이 유지되면 `NO_RECENT_MOTION`.
- 처음 수신·재연결은 기준값 설정으로 취급하며 움직임을 생성하지 않습니다.
- 유효 수신이3초 끊기면 `DATA_LOST`; USB 오류는 `CONNECTION_ERROR`. 오래된 값은 `null`이며 이석으로 판정하지 않습니다.

USB 읽기는 한 스레드가 담당하며 지문 요청은 잠금으로 한 건만 처리합니다. OLED는 지문 안내를 우선하고 완료 후3초간 결과를 유지합니다. 그동안 USB 수신과 `/motion` 조회는 계속됩니다. 이벤트 CSV는 기본 `~/attendance/light_motion_events.csv`에 로컬 저장하며 `finger_shadow_simulation`을 표시합니다. 이 파일을 저장소에 추가하지 않습니다.

## 부팅 자동 실행

[attendance-device.service.example](attendance-device.service.example)의 네 자리표시자를 실제 값으로 바꾼 **장치용 로컬 파일**을 만듭니다.

| 자리표시자 | 확인할 값 |
|---|---|
| `CHANGE_ME_USER` | Pi에서 `id -un` |
| `CHANGE_ME_WORKING_DIRECTORY` | `pi_api.py`가 있는 폴더의 절대경로 (`pwd`) |
| `CHANGE_ME_VENV_PYTHON` | 센서 가상환경에서 `python3 -c "import sys; print(sys.executable)"` |
| `CHANGE_ME_SCRIPT_PATH` | 그 폴더의 `pi_api.py` 절대경로 |

수정한 로컬 파일을 설치합니다. 기존 서비스가 있으면 먼저 백업하고 실제 경로를 확인합니다.

```bash
sudo cp attendance-device.service /etc/systemd/system/attendance-device.service
sudo systemctl daemon-reload
sudo systemctl enable --now attendance-device.service
systemctl status attendance-device.service --no-pager
curl http://127.0.0.1:8080/health
```

설정 예제는 `User` 권한으로 한 프로세스를 실행하며 오류 종료 후5초에 재시도합니다. SIGINT로 종료를 전달하고 최대45초 정리를 기다립니다. `enabled`와 `active`만으로 부팅 후 센서 정상 동작을 단정하지 않고 재부팅 후 API도 확인합니다.

```bash
sudo systemctl restart attendance-device.service
sudo systemctl stop attendance-device.service
sudo systemctl disable --now attendance-device.service
journalctl -u attendance-device.service -n 50 --no-pager
```

각각 수정 반영, 일시 중지, 자동 실행 해제와 중지, 로그 확인입니다. 서비스가 켜져 있을 때 `python pi_api.py`를 따로 실행하면 포트/장치가 중복 사용됩니다.

## 검증과 남은 범위

장치 없이 새 테스트 환경에서 실행:

```bash
python -m venv .test-venv
# Windows: .test-venv\Scripts\activate
# Pi/Linux: source .test-venv/bin/activate
python -m pip install -r requirements-test.txt
python -m unittest -v test_motion_tracker test_pi_api test_local_names
```

2026-10-19 사용자가 기존 배포본에서 직접 확인한 범위는 지문 실인식, 조도 수신, Windows curl/점검 화면, 서비스 등록과 재부팅 후 자동 실행·health/조도 수신입니다. 당시 실행본의 전체 해시/패키지 목록을 수집하지 않았으므로 이 게시 커밋 전체를 Pi 재배포·실검증했다고 표시하지 않습니다. 게시 때 추가한 로컬 이름표 로더는 별도 자동 테스트 범위입니다.

손가락 그림자 모의실험이며 실제 다리·재석/이석 정확도 검증은 하지 않았습니다. 운영 장치 인증·암호화/권한·신선한 결과 검증·회원 연결·근태 저장·WinForms 통합·장시간/전원 장애 검증은 미구현 또는 미실행입니다. 센서 점수는 확률이 아니며, 장치 결과를 공통 회원 본인 확인으로 바로 신뢰하지 않습니다.

기존 [2번 용도 결정](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_지문센서_원격근무_본인인증_용도변경.md)에 따라 계정 로그인 이후 본인 확인이 목적입니다. 장치·슬롯과 회원 연결, 요청 시점·주기, 실패/중복·저장·출퇴근 연계는 후속 협의 대상으로 유지합니다. [5번 구현·협업 기록](https://github.com/dudu110101/jwh_2nd_project/tree/LOG/5번)과 코드 PR에서 게시·검토 상태를 확인합니다.
