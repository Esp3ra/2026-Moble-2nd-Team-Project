# 5번 장치 HTTP 연결 검사

실제 `HttpFingerprintIdentityDevice` + `FingerprintIdentityRelay`에서 loopback TCP로 Python Flask의 `identity_http`와 기존 지문 비교/승인/서명 코어를 호출한다. 센서는 가짜 UART 객체이며, issuer/승인/complete는 **이 시험 프로세스만의 합성 RAM 서비스**다. 1번의 운영 인증 API 구현·MySQL/로그인 성공을 검사한 것이 아니다.

```powershell
dotnet run --project Tests/FingerprintIdentity.Http.Tests -c Release -- --python "<Flask와 tzdata가 설치된 가상환경의 python.exe>"
```

소스 체크아웃이 필요하다. Python에 기존 Flask/tzdata 시험 의존성을 준비하며 실센서·serial/Adafruit/OLED 라이브러리·개인 설정은 읽지 않는다. 프로그램은 `127.0.0.1`의 OS 선택 빈 포트로만 시험 프로세스를 만들고, 종료 시 자기 프로세스/worker를 정리한다. systemd/실제 Pi/서버·계정/DB/키 파일은 바꾸지 않는다. Python 프로세스가 만든 일회성 합성 방향키는 C#에 전달하지 않는다.

검사 범위:

- matched/no_match의 실제 양방향 원문 전달·Pi MAC 검증과 시험 서버의 result binding·영수증. verified도 workflow pending이다.
- POST 전/후 응답 유실, 승인 ACK 유실에서 같은 relay/capability/body·probe 승인 원문을 유지한다. 중복 센서 검색/서버 승인/최종 결과를 만들지 않는다.
- 승인 MAC 변조·2초 지연, 입력 대기 중 취소, 문맥 교체 때 늦은 승인 전달과 complete를 막는다. 공유 FingerprintService stopping은 유지한다.
- 완료 응답 유실 뒤 실제 HTTP GET 복구·새 스캔 금지. redirect·Content-Length/길이 없는 과대 응답 차단과 자기 작업 취소.

`Devices/RaspberryPi/test_identity_http.py`는 별도로 Flask test client에서 capability·MAC/중복/다른 원문·body 제한/알 수 없는 필드·취소/완료 원문·타임아웃·기본off·worker 시작 실패·RAM 상한·종료를 검사한다. `IDENTITY_HTTP.md`의 장치 규약은 5번 소유 제안/구현이며 타 담당 수용·실서비스 활성화와 구분한다.
