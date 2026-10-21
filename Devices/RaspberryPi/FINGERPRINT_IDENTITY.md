# RW-ID-v2 지문 본인 확인 어댑터 — 5번 로컬 준비본

작성자 5번 / 전달 대상 1번·팀장. 기준: 1번 PR62 `0fcffbdbf6e021462aa7b9e0b56c89251a1035a3`의 [계약](https://github.com/jinkyu1452-cmd/Sang1/blob/0fcffbdbf6e021462aa7b9e0b56c89251a1035a3/docs/DEVICE_IDENTITY_V2.md). 새 로그인 제품의 활성화·실물 검증이나 기술 합의 완료 기록이 아니다.

## 이번에 구현한 범위

- `fingerprint_identity_wire.py`: 1번 codec·fresh reference를 Pi용으로 옮겼다. 기존 enrollment 모듈에서 엄격 JSON/base64/UUID/UTC 파싱만 가져오며 등록 키·domain·TTL·payload를 재사용하지 않는다. v2 네 방향 domain/HMAC·전체 envelope 원문 SHA256·scope/nonce/expected slot·mapping revision/epoch를 대조한다. 공개13벡터의 payload/envelope/MAC/digest가 정확히 일치한다. 별도 서버 패키지 import는 없다.
- `IdentityProfile`: 프로그램 내부의 신뢰 프로필 접점이다. 실제 파일 loader/설치기는 아니다. 네 방향의 서로 다른32byte키, epoch/허용 slot/capacity를 검사한다. 공개 벡터 키와 기존 키 재사용을 거절하며 기존 등록/v1·회수된 키의 제외 목록을 명시적으로 공급해야 한다. 이 목록은 PC 입력이나 새 프로필 JSON의 임의 필드가 아니라 신뢰 설정 소유자가 기존 설정에서 확보할 값이다. AS608 코어의16bit slot 범위도 적용한다. 키·원문·개인정보를 출력하지 않는다.
- `IdentityAttempt`: 검증된 요청 하나에 한 번만 센서를 사용한다. 기존 `FingerprintService.lock` 획득 **뒤** 새 capture probe를 생성하고, 비교·result 승인·서명까지 같은 잠금을 유지한다. 공유 UART/OLED를 빌리며 다른 scan은409, 조도 수신은 계속된다. 새 등록·센서 저장/삭제·계정 매핑 변경은 없다.
- 캡처는 기존 비교 코어의 내부 `_compare_owned`를 사용한다. 공개 `compare_fingerprint`의 기존 동작/잠금 소유는 유지한다. blocking 전후에 취소·기한·승인을 확인하며, 결과가 늦으면 matched를 반환하지 않는다. 기본 시계는 Linux `CLOCK_BOOTTIME`; 지원되지 않으면 중단한다. 주입 시계는 시험/신뢰 런타임 소유자가 제공하며 유한값·역행을 검사한다.
- capture/result scope·fresh RAM nonce는 별개다. 승인 RTT 상한2초와1.25초 여유를 적용하고, 최초 승인 잔여와15초 중 더 짧은 **단일 캡처 예산**만 쓴다. 이 구현은 최초 승인 수명을 넘어 입력을 이어가지 않으며 갱신으로 입력 예산을 연장하지 않는다. 짧은 승인은 짧게 끝낸다. matched/no_match 뒤에는 새 result 승인이 필요하다. 승인 실패·불명확/취소·역행은 결과를 성공으로 고정하지 않는다.
- different_finger/unregistered는 no_match+null slot으로 합친다. matched만 검증된 기대 slot을 담는다. busy/timeout/cancelled/sensor_error의 failure_code는1번 계약과 일치하며 점수·이름·타인 slot·생체 데이터는 담지 않는다. 로컬 OLED는 ‘지문 일치·서버 확인 필요’까지만 표시한다.
- 결과 ID와 서명 원문은 한 번 만들어 RAM에 고정한다. 같은 attempt 재전달은 동일 bytes이며 실패/중단 뒤 자동 재스캔하지 않는다. `IdentityAttemptPool`은 같은 challenge/원문에 같은 attempt를 돌려주고 다른 원문을 거절한다. RAM 상한 도달 시 새 요청을 거절하며 자동 제거/초기화 기능은 없다. **프로세스 재시작 후 이전 요청을 다시 실행할 수 있다는 보장이 아니다.** 실제 소유 런타임은 풀을 요청마다 새로 만들면 안 되며, 서버 상태 재조정·재시작 승인 전까지 이전 요청 실행을 차단해야 한다.

## C# 중계와 남은 연결

`FingerprintIdentityRelay`는 이미 발급받은 challenge 하나를 처리하는 내부 코어다. `IIdentityRelayServer`와 `IIdentityRelayDevice`에 현재 문맥에 묶인 transport를 주입한다. 생성자·스캔 중·승인/완료/조회 응답 뒤에 세션/취소를 확인한다. PC는 키를 보유하거나 MAC을 검증한다고 주장하지 않는다. 구조·장치/challenge·원요청 digest를 검사한 뒤 서명 bytes를 base64url로 포장할 뿐 payload를 재직렬화하지 않는다.

complete 전 결과를 고정한다. 응답 유실 뒤에는 같은 문맥 GET부터 수행하고 issued일 때만 같은 원문을 재전송한다. 이미 verified/rejected면 조회 결과를 반환하며 새 센서 호출·새 UUID·새 결과 재구성을 하지 않는다. 로컬 matched만으로 성공하지 않고 현재 서버의 정확한 challenge/purpose 영수증을 확인한다. 반환 verified는 workflow pending인 **본인 확인 영수증만**이다. 출근·퇴근 저장/정상 토큰 전환이나 finalize를 구현하지 않았다. 정상 토큰이 먼저 발급되는 기존 로그인을 새 인증으로 포장하지 않는다.

**이번에는 실제 Pi HTTP job/probe 경로·HTTP device channel·서버 HTTP adapter·LoginForm/MainForm UI·로그인 발급 복구를 추가하지 않았다.** callback은 제품 RPC 규약 확정이나 호출 가능한 endpoint가 아니다. 이 파일을 Pi에 복사하는 것만으로 로그인 지문 요청이 켜지지 않는다. 새 상태 파일/SQLite·로컬 DB는 없으며 비공개 키 loader·새 프로필 설치도 없다. 기존 등록 프로필·Pi8080 서비스·완료된 지문은 변경하지 않았다.

1번의 실제 서버 HTTP/제한 로그인/MySQL 단일소비·근태 전환 제공 후, 5번이 Pi↔PC transport/화면을 연결하고1번 문맥 수명/실제 DTO와 다시 검증한다. 실제 활성화 전에는 재시작 시 미확정 작업 처리, 키 제외 목록/ACL, 서버 신뢰·장치 endpoint 설정, 취소/문맥 폐기, 응답 유실 복구를 함께 점검해야 한다. 임시 공개 키·가짜 회원/근무 세션으로 이를 대체하지 않는다. 기존 등록 재시도나 지문 초기화가 필요한 단계가 아니다.

## 실행한 검사

Windows .NET8 / Python3.13 전용 가상환경, 합성 값/대역만 사용했다.

```powershell
dotnet run --project Tests/FingerprintIdentity.Tests -c Release
dotnet build RemoteWork.WinForms/RemoteWork.WinForms.csproj -c Release --no-restore
python -m unittest discover -s Devices/RaspberryPi -p test_fingerprint_identity.py
python -m unittest discover -s Devices/RaspberryPi -p test_fingerprint_match.py
```

- C# **143 assertion**:13원문 round trip/digest, constructor 복사, capture/result probe·승인/결과 exact bytes, 단일 실행, complete 유실 전후 GET·같은 bytes 재전송, 기기 결과 유실 시 재스캔 금지, dispose/문맥 교체·늦은 응답, 다른 challenge/purpose/완료 상태 거부, no_match·변조 binding. 실제 HTTP adapter가 아닌 통제 transport다. Release 경고0/오류0.
- 새 Python **24 tests**(13벡터 대조 subtest 포함), 기존 비교20개. 전체는 미추적 구형v1 파일6개를 제외한 Git export에 이 변경만 겹친 뒤 **214 tests / 212 PASS·Linux flock2개 skip**. 기존 예상 센서 오류 테스트의 출력은 실물 오류 관찰이 아니다.
- 실제 새 서버 endpoint/MySQL 경합·영속/재시작·Linux CLOCK_BOOTTIME/실센서·실물 로그인·정오/퇴근/두PC는 미실행이다. 공개 codec 벡터 일치나 로컬 대역 통과를 그 완료 근거로 쓰지 않는다.

협업 상태: 요청/1번 계약 게시를 수신하여 로컬 구현·검사한 단계다. 상대 코드 검토·합의·포크 push·PR·원본 병합·배포 완료와 구분한다. 팀장 전용 날짜 LOG에 직접 쓰지 않고, 게시 승인 후 기존 PR58 대표 댓글/LOG1·5 취합으로 전달한다. 연차 로컬 준비본은 별도 보류 상태이며 지문 PR에 섞지 않는다.
