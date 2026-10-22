# RW-ID-v2 본인인증 계약 검사

범위·담당·API DTO/시간/재시도: [1번 → 5번 인계](../../docs/DEVICE_IDENTITY_V2.md).
Python codec·Pi receiver reference와 API 입력 DTO를 검사한다. **실제 API/제한 로그인/MySQL 단일소비·근태·센서·C# adapter 검사가 아니다.**
공개 키4개는 서로 다른 합성 test key이며 운영 설정에 복사하지 않는다.

서버 가상환경에서:

```
cd RemoteWork.Server
python -m pytest tests/test_device_identity_contract.py -q
python -m remotework.device_identity_contract
```

소유 MySQL에서 기존 인증/등록 회귀를 함께 실행할 때도 이 새 contract 검사는 DB를 만들지 않는다.
공개 벡터 재생성: 저장소 루트에서 `python Tests/DeviceIdentity.Contract.Tests/generate_vectors.py`.
생성기는 파일2개만 재현하며 키/설정·서버·DB·장치에 접근하지 않는다.

검사 내용: 독립 HMAC/digest 대조, 방향키/domain/v1 분리, 임의 목적·slot/epoch·타 challenge 결합·잘못된 결과거절,
unknown/duplicate필드·정규 UUID/base64url/BIGINT/UTC,15초 입력보다 긴20초 승인·RTT·단조시계·갱신/one-use/probe 무효화,
명시적 서버 기한 clamp, API JSON bytes 포장·토큰/임의slot 주입거절, contract-only 기본off와 잘못된 활성화의 DB연결 전 거절.
