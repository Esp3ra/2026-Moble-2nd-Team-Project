# 5번 Pi ↔ C# HTTP 연결안

작성자5번 / 전달 대상1번·팀장. PR63 코어 후속이며, 기존 등록 API/새 서버 RW-ID-v2 계약을 바꾸지 않는 **5번 소유 장치 전송 규약**이다. 로컬 구현·시험 단계이고 팀 수용·실물 배포 완료가 아니다.

## 목적·영향

Pi가 서버용 Bearer나 DB 계정을 받지 않고, C#이 키 없이 서명된 request/probe/approval/result의 원문을 중계하게 한다. Pi는 기존 UART/OLED/센서 잠금을 공유한다. 이 경로에 등록/삭제·회원 매핑·출퇴근 저장 기능은 없다. 대상은 새 `identity_http.py`·`HttpFingerprintIdentityDevice.cs`·전용 시험이며 기존 실행기/서비스·MainForm/LoginForm·공통 서버/DB는 바꾸지 않는다.

## 장치 경로·권한

- POST `/identity/v2/jobs`: 정확한 JSON `{relay_id, request_b64}`. relay_id는 C#에서 만든 정규 UUID. 헤더 `X-Identity-Relay`는 C# RAM의 무작위32byte base64url capability다.
- GET `/identity/v2/jobs/{relay_id}`: 같은 capability로 현재 상태/서명 probe 또는 고정 result를 조회한다.
- POST `.../{relay_id}/approval`: 정확한 JSON `{probe_id, approval_b64}`. Pi가 낸 현재 probe UUID와 해당 승인 원문이다. 같은 probe/같은 원문은 단일 효과, 다른 원문은409다.
- POST `.../{relay_id}/cancel`: 정확한 JSON `{}`. 해당 Pi 작업만 중지하며 서버 취소/문맥 폐기·센서 삭제·근태 rollback이 아니다. 완료된 고정 결과는 지우지 않는다.

모든 응답은 `{success:true,data:{relay_id,request_digest,state,probe_id,probe_b64,result_b64,error_code}}` 또는 `{success:false,error:{code}}`. state는 running/awaiting_approval/succeeded/failed. 실패·완료 이후 다시 캡처하지 않는다. capability 누락/다른 값과 없는 작업은 같은404이며 키·원문·capability는 로그/오류에 넣지 않는다. body는 최대32KiB 엄격 JSON, 서명 envelope는16KiB 한도다. 같은 relay/body/capability 재요청은 같은 작업을 반환한다. 같은 challenge를 다른 relay/capability로 다시 열지 않는다.

RAM pool과 job 표는 정해진 상한을 공유하고 자동 삭제/초기화·재시작 재구성이 없다. 셧다운은 자기 worker만 취소·기다리며 공유 센서 서비스의 stopping을 설정하지 않는다. 실제 실행기는 재시작 시 서버 상태 재조정 뒤 새 요청만 허용하는 수명 정책과 비공개 키 loader/권한 검사를 별도로 갖춰야 한다.

## 연결·검증 경계

기본 factory는 비활성으로 아무 route도 설치하지 않는다. 명시 활성화 시에만 신뢰 프로필과 기존 FingerprintService를 주입해 조립한다. 프로필 파일 loader·systemd 전환/키 설치·실물 배포는 이 단계에 포함하지 않는다. C#은 장치용 별도 HttpClient로 Bearer/쿠키를 보내지 않고 proxy/redirect를 끄며 응답 크기·전체/요청 수명과 취소를 제한한다. signed bytes를 JSON으로 재직렬화하지 않는다.

POST 응답 유실은 같은 relay/capability GET으로 확인하고, 정확한 미존재 응답일 때만 같은 POST를 재전송한다. 승인 POST 유실도 같은 probe/원문만 유지한다. 그 사이 probe의2초 fresh 예산이 끝나면 실패하며 새 승인/재스캔으로 우회하지 않는다. 네트워크 중단 중 Pi가 이미 완료했다는 가능성은 센서/서버 저장 실패나 rollback으로 단정하지 않는다.

시험은 내 PC의 loopback HTTP·Python 가짜 센서·프로세스 안에서 만든 합성 키만 사용한다. 시험 issuer/approval/complete는 운영 서버 API가 아니다. 실제1번 API/제한 로그인·MySQL·새 키·실센서·두PC는 별도 검증이다. 구현/시험 결과·커밋·PR과 상대 확인 요청은 작업 후 갱신한다. 날짜LOG는 팀장 취합이며 게시 전에는 외부 전달/합의 완료로 기록하지 않는다.

## 구현·검사 결과와 확인 요청 — 2026-10-21

- `identity_http.py`: 명시 factory와 RAM job/nonce 승인 중계·중복/다른 원문·capability 검사, 취소·worker 종료·한도/시작 실패 처리를 구현했다. 기본off이며 기존 실행 서비스에 장착하지 않았다. 중지한 manager의 HTTP 접근은503으로 닫고 공유 센서의 stopping은 건드리지 않는다.
- `HttpFingerprintIdentityDevice`: 실제 HttpClient 구현이다. 임시32byte capability만 Pi에 보내고 계정 Bearer/쿠키/서명키를 갖지 않는다. POST 응답 유실의 GET 우선 복구와 같은 ACK 재전송, probe당 서버 승인1회, 최초 관찰부터2초 안의 ACK 한도, 최대65초 전체 전송 수명·2초 개별 요청·64KiB 응답 제한·종료 시 자기 작업 취소를 적용한다. C#의 시간 제한은 전송을 중단하기 위한 것이며 Pi의 BOOTTIME/서명된 서버 시각 검증을 대체하지 않는다.
- Python3.13.15 Git export 전체 **228 tests / 226 PASS·Linux flock2 skip**(5.437초). 새 HTTP14개는 이 전체에 포함된다. 이전214/새14를 반복 합산한 수치가 아니다.
- .NET SDK10.0.302/net8.0-windows: 기존 중계 **143 assertions PASS**, 별도 실제 loopback HTTP **110 assertions PASS**, Release 경고0/오류0. TCP·Flask·실제 Pi 코어/가짜UART·시험 issuer에서 matched/no_match, 최초POST 전후 유실·승인ACK 유실·complete 유실, 서명/지연·취소/문맥 교체, redirect·명시/길이 없는 과대 응답, 자기 프로세스 종료를 검사했다. 실제 회원/서버/DB·장치/키 파일은 사용하지 않았다.
- 기준 코드/협업: [PR63 head51e3871](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/63) 후속 로컬 준비본. 1번의 [코어 방향 수용/독립 검사](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/63#issuecomment-6312104306)는 이 새 HTTP 규약/구현의 수용과 구분한다. 후속 커밋·게시 상태는 작업 보고와 기존 대표 댓글에 연결하며, 이 시점 새 push/PR 갱신/외부 회신은 하지 않았다.
- **1번 확인 요청:** 기존 서버 `probe_b64/approval_b64/result_b64` 원문 계약과2초 RTT를 그대로 소비한다. 새 장치 endpoint나 capability를1번 공통 인증/API/DB에 추가하는 요청이 아니다. 실제 IdentityPending/현재문맥 HTTP adapter를 붙일 때 승인/취소·결과 조회 수명을 대조해 달라. 공개 키나 기존 등록 키를 운영에 재사용하지 않는다.
- **미실행/미연결:** 실센서·Linux BOOTTIME·새 서버 API/MySQL/두PC·비공개 프로필 loader/설치·재시작 시 서버 상태 재조정·실제 Pi 서비스 조립과 전환·LoginForm/MainForm/정상 토큰/근태 finalize. 기존8080 서비스·지문·회원·개인 설정은 유지했다. 이 시험 성공은 실물 로그인 성공이 아니다.
