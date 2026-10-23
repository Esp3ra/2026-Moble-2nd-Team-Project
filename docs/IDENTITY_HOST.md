# 1번 지문 서버 비공개 실행 조립

기존 서버 API·6개 테이블·AttendanceWorkflow를 연결한다. 새 endpoint/DTO/DDL/정책은 없다.
5번 Pi 진입점·화면·센서 코드는 수정하지 않는다. 이 문서의 JSON은 서버 로컬 파일 제안이며
Pi의 기존 `identity-rw-id-v2` wire 또는 프로필을 바꾸지 않는다.

## 실행

`run.py --identity-private-config <서버 전용 파일> --identity-retired-keys <회수키 목록>`을 사용한다.
두 파일을 함께 지정해야 하며 `--demo/--init`과 결합할 수 없다. 파일은 Git 밖에 두고
현재 사용자 소유·일반 파일·단일 링크·비공개 부모/파일 권한을 요구한다. Windows는 현재
사용자·SYSTEM·Administrators 외 허용 ACL을 거절하고 Linux는 owner/mode를 검사한다.
읽기 실패·검사 실패 때 권한을 자동 수정하지 않고 시작을 중단한다.

서버 전용 파일은 `{version:1, contract:"identity-host-private-v1-PROPOSAL", profiles:{장치UUID:기존프로필},
receipt:{key_id:독립식별자,key:32바이트base64url}}`다. profiles는 기존 서버의 정확한
device_id/key_id/key_epoch/enabled/minimum_slot/maximum_slot/capacity/keys 필드이며 keys는
id_request/id_probe/id_approval/id_result 4개다. 모든 키는 패딩 없는 canonical base64url이다.
회수 목록은 `{version:1, keys:[32바이트base64url,...]}`다. 중복·공개 시험키·현재 등록키·
회수 목록과 같은 키·방향키 재사용·receipt키 재사용을 거절한다. **빈 목록을 통과한 것이
과거 회수키가 없다는 증거는 아니다.** 운영자가 기존 배포/회전 이력과 목록 출처를 별도로 대조한다.
서버 receipt키·SECRET_KEY·DB 접속 정보는 PC/Pi 전달물에 넣지 않는다.

`--identity-check-config`를 추가하면 listener 없이 기존 실제 MySQL 물리 검사와 현재 장치
epoch/범위/활성 회원 매핑·미확정 challenge 수·영속 capture fence 수를 읽는다. 등록·취소·
만료·근태 저장·DDL/seed/.env 쓰기는 없다. 실제 실행도 기존 환경변수
`REMOTEWORK_ENABLE_DEVICE_IDENTITY_DRAFT=1`을 명시해야 API가 활성화된다. 파일만 지정하거나
검사만 실행해서 기능을 ON으로 바꾸지 않는다. 일반 `run.py`는 기존 흐름을 유지한다.

## 재시작 대조의 경계

이 도구는 **Pi의 trusted `ready(profile)` 구현이 아니다**. `pi_restart_readiness=NOT_SUPPLIED`를
항상 보고하고 회수 목록의 출처 검증/실물 검증도 완료했다고 표시하지 않는다. 초기6테이블이
비어 있거나 health200이어도 Pi admission을 열지 않는다. 서버 재시작은 기존 미확정 결과
복구를 막지 않는다. 미확정 수는 Pi 재시작 대조의 입력이지 서버 기동 차단 조건이 아니다.

issued는 DB 시간이 지났어도 자동으로 해결 처리하지 않는다. 원 문맥의 기존 GET/명시취소·
만료 경로로 대조한다. verified이면서 workflow 미완료인 경우도 별도 확인 대상으로 센다.
capture_nonce는 종단 처리 뒤에도 지우지 않는다. 옛 challenge를 새 capture로 재실행하지 않는다.
Pi의 현재 보호 hold를 root 읽기로 확인하고 단일서비스 종료/drain/기존 runtime.lock·공유 UART,
실제 회수키 이력, 현재 PC/Pi 프로필을 같은 회차에 대조한 뒤 신뢰 callback을 연결해야 한다.
그 절차가 없으면 5번 entry는 기존대로 차단한다. hold/chmod/센서 삭제·재등록을 자동 실행하지 않는다.

## 검증

`RemoteWork.Server/tests/test_identity_host.py`는 키 재사용/회수·변조·중복 JSON·링크·권한 실패와
소유 MySQL의 현재 epoch·영속 capture fence·원 API 취소 후 read-only 보고를 확인한다.
합성 키/계정·소유 DB이며 실제 Pi/두 PC·실물 출퇴근 검증과 구분한다.

Windows에서 기존 Python 의존성을 준비한 뒤 다음을 실행한다.

```powershell
python Tests/IdentityHost.Tests/run_check.py --allow-disposable-mysql --mysql-bin-dir 'C:/Program Files/MySQL/MySQL Server 8.2/bin'
```

runner는 기존 소유 MySQL 도구로 독립 DB/프로세스를 만들고 정리한다. 사용자 .env를 읽지 않는다.
