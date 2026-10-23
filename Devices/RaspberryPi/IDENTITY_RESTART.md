# 재시작 관측 자료 소비

작성 5번 · 전달 1번/팀장. 서버 공급 기준은 PR82의 `21f3ca2`와
`docs/IDENTITY_HOST.md`다. 비공개 자료 자체는 Git에 넣지 않는다.

`identity_restart.py`는 기존 개인 전달 경로로 받은 여섯 파일의 검사부다.
manifest의 정확한 파일 목록·중복·SHA256, deployment/observation의 같은 회차,
현재 IdentityProfile의 장치/키 식별자/epoch/범위, 관리범위 감사와 회수 목록 해시·개수,
원 challenge/context/client_request ID와 상태·fence 개수를 대조한다.
별도로 검토한 서버 소스의 해시를 `expected_sources`로 주면 소스도 대조한다.
manifest만으로 전달자 진위나 실행 중 서버 소스를 증명하지 않는다.

이미 기한이 지난 issued도 미확정으로 유지하며 verified/workflow 미완료도 미확정이다.
취소된 작업의 fence와 원 식별자는 삭제하거나 다른 UUID로 바꾸지 않는다.
원 자료는 비공개 경로에 보존하며 반환 요약/표준 출력에는 원 작업 ID·키·토큰이 없다.
제안된 관측 자료에는 인증 토큰이 없으므로 이 자료로 원 PC 문맥의 GET/취소를 대신할 수 없다.
기존 원 문맥 GET/명시 취소·만료/확정 결과 대조는 서버·원 PC 소유자가 담당한다.

관리범위 밖의 키 이력이 없다고 확대하지 않는다. 현재 제안은 구형 등록키의 추가 제외
바이트를 제공하지 않으므로 그러한 제외가 있다고 주장하는 묶음은 중단하고 공급자와 대조한다.
회수 identity 키는 현재 네 방향키/현재 등록키/공개 시험키와 겹칠 수 없다.

## 읽기 전용 진입점

Linux에서 기존 비공개 설정과 새 관측 폴더를 명시한다.

```sh
python3 pi_api_identity_mysql.py \
  --config /PRIVATE/enrollment.mysql.local.json \
  --identity-config /PRIVATE/identity-rw-id-v2.private.json \
  --check-config --restart-bundle /PRIVATE/restart-observation
```

`--restart-bundle`은 `--check-config`와만 사용한다. 서비스 실행과 함께 사용하면
runtime.lock/UART를 열기 전에 거절한다. 기존 owner/mode/no-link 검사를 재사용하며
폴더 권한 자동 수정·상태 파일 생성·서비스 조작·DB/센서 접속은 하지 않는다.
이 명령은 독립 서버 소스를 입력받지 않으므로 소스 해시의 형식까지만 검사하고,
그 소스와 실제 실행 상태의 대조는 신뢰 호스트에서 별도로 수행해야 한다.

## 호스트 연결 경계

반환값은 `RestartObservation`이며 bool 변환도 거절한다. 빈 작업 목록·파일 전달·OFF/ON
표시·health200을 ready 증표로 변환하지 않는다. `pi_ready_authorization=false`와
`NOT_SUPPLIED`를 유지한다. 기존 `run(..., ready=..., retired_keys=...)`의 실제 callable을
이 검사부가 생성하거나 대체하지 않는다.

1번의 같은 회차 서버 설정/원 작업 대조, 5번의 기존 release 의미·이전 서비스 종료/drain·
동일 runtime.lock/단일 UART·현재 process/boot 근거를 묶은 신뢰 호스트 결합이 남는다.
서버/설정/원 작업 또는 Pi process가 바뀌면 새 대조가 필요하다. 고정 TTL이나 자동 승인
정책을 이번 검사부에서 추가하지 않는다. 수명은 기존 IdentityRuntime의 False/예외/stopping
후 영구 닫힘과 새 프로세스 재대조 규약을 따른다.

단일 서비스 전환·새 지문 ON은 별도 실행이며 기존 양의 ConditionPathExists와 release,
완료 등록/매핑을 보존한다. 이 검사부 게시/통과는 전환·활성화·실물 성공이 아니다.

검증: `test_identity_restart.py`는 합성 묶음의 변조/회차·epoch·범위·소스 불일치,
승인 주장 거절, 과거 issued/verified 미완료, 원 취소 ID/fence 보존과 중복 ID·키 재사용을
확인한다. `test_pi_api_identity_mysql.py`는 관측 검사 성공/실패 모두 lock/UART 미접속과
관측 파일을 서비스 시작 허용으로 사용할 수 없음을 확인한다.
