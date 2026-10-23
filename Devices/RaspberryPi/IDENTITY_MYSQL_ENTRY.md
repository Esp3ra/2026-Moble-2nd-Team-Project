# 등록·본인 인증 단일 실행 진입점

2026-10-23 실제 호스트 구현: [IDENTITY_SERVICE.md](IDENTITY_SERVICE.md)의
`pi_api_identity_service.py`가 root의 정상 종료/단일 invocation 인계와 현재 process/profile/
획득된 runtime lease를 결합한다. 아래 `run(..., ready=...)`는 기존 주입 접점으로 유지한다.
새 호스트·전환/복구 도구의 구현과 실제 검토/서비스 적용·새지문ON/실물 성공은 구분한다.
아래 HTTP/프로필 부재·미설치 기록은 최초 준비 단계 이력이며 현재 배포 상태로 읽지 않는다.

`pi_api_identity_mysql.run(argv, ready=..., retired_keys=...)`는 기존 MySQL 등록과 본인 인증을 하나의 `pi_api.main`에 명시 조립한다. 기존 `mysql_runtime_session`의 동일 runtime.lock을 먼저 획득하고 하나의 FingerprintService를 두 factory가 빌린다. 기존 main이 인증→등록→조도→센서 drain→UART 종료를 담당한다. 별도 DB·UART·서비스/로컬 SQLite/키 생성이나 등록·삭제를 하지 않는다.

등록 MySQL 설정과 별도 인증 설정을 기존 strict loader로 검증한다. 기존 등록 키와 인증4키를 분리하고 `retired_keys`도 신뢰 호스트가 제공한다. 실제 비공개 설정을 Git에 넣지 않는다.

```bash
python3 pi_api_identity_mysql.py --config /private/enrollment.mysql.json \
  --identity-config /private/identity.json --check-config
```

이 검사는 profile/키 분리의 읽기 전용 검사이며 state 잠금·센서·서버·runtime을 열지 않는다. PASS와 함께 NOT_READY를 출력하며 운영 readiness 승인이 아니다. 권한/소유자·링크 검사는 기존 Linux loader 규약 그대로다.

실제 실행은 신뢰 호스트가 기존 `ready(profile)` 콜백을 주입해야 한다. **CLI 플래그/JSON의 ready=true/health200/시간 대기**로 이를 대체하지 않는다. 주입이 없거나 callable이 아니면 state 잠금과 UART를 열기 전에 중단한다. 서버의 기존 challenge GET/명시취소·만료/capture fence와 이전 미확정 작업·키 epoch/회수 검증을 대조하는 실제 호스트 준비는 1·5번 공동 작업이다. entry가 그 대조를 완료했다고 주장하지 않는다. 기존서비스를 이 파일로 전환하기 전에 프로필/키 공급·재시작 대조·단일서비스 백업/복구를 확인해야 한다.

2026-10-23 직접 HTTP에서 Pi health200·등록OPTIONS200·identity/v2/jobs OPTIONS404를 확인했다. 사용자의 읽기전용 SSH 출력은 active 서비스의 기존 MySQL entry와 비교6소스 불일치, 인증v2 비공개 프로필 부재, BOOTTIME 사용 가능을 보고했다. 파일 부재와 구버전은 그 출력만으로 구분하지 않는다. 기존 지문·매핑·서비스를 변경하지 않았다.

새 entry4개 검사는 합성 loader/runtime으로 읽기전용·준비누락 차단·동일센서/기존잠금·오류 경계를 확인한다. 기본 Python3.14에는 Flask가 없어 첫 실행이 import 실패했으며, 기존 시험용 Python3.13 환경으로 실행해4개 통과했다. 기존 runtime/HTTP/권한 전체 회귀와 실제 Linux 비공개 설정·센서 시험은 각각 구분한다.

검토한Git export의 전체 Pi 회귀는255실행/252통과·Linux전용3생략,6.547초다. 기존251+새4를 합친 전체이며4를 다시 합산하지 않는다. 사용자가 소스285c8aec의 런타임19파일을 라파의 별도 후보폴더에 배치했고 해시검증PASS/NOT_ACTIVE 결과를 전달했다. 이는 비공개키 설치/서비스 전환/실물인증 성공이 아니다. 기존서비스와 등록지문은 보존했고 준비 callback·키 공급/실효 서버설정은1번과 대조 중이다.
