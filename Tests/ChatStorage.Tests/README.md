# PR21 채팅 세션·문자 수 회귀

실제 제품 ApiClient/HttpChatService/HttpChatParticipantDirectory/ChatText 소스를 링크하는 .NET 8 실행기다. 대체 구현을 복사해 테스트하지 않는다.

```powershell
dotnet run --project Tests/ChatStorage.Tests/ChatStorage.Tests.csproj -c Release
```

40개 HTTP 대역 시나리오: 같은 세션 정보 갱신 허용(후보·방·메시지·생성·전송·숨김), 실제 로그아웃·동일 계정 재로그인·다른 사용자 교체 차단, 전체 페이지의 동일 세션, 한글/ASCII/보충 평면 문자 2,000/2,001 경계·혼합 본문·이모지1,001 재현·불명 전송 재시도 UUID 유지. 실제 DB/서버 결과와 구분한다.

실제 HTTP 모드는 사용자가 삭제 가능한 전용 개발 DB/서버를 준비한 뒤 명시적으로 실행한다. 공유 DB를 지정하지 않는다. 활성 합성 계정 staff1/Staff-test-2026! 및 admin1/Admin-test-2026!와 채팅 활성화/3테이블이 필요하다. 이 공개 테스트 비밀번호를 실제 사용자 계정에 쓰지 않는다.

```powershell
dotnet run --project Tests/ChatStorage.Tests/ChatStorage.Tests.csproj -c Release -- --http http://127.0.0.1:테스트포트
```

15개 실제 C#→TCP→DB 검사: 후보·방 생성·이모지2,000 저장/조회·동일 세션 정보 갱신·상대 계정 조회·새 클라이언트 재로그인 복원·본인 숨김/상대 기록 유지·신규 대화 후 숨김 유지·초과 본문 거절. 한 PC의 두 계정 검증이며 물리적으로 두 PC를 사용한 검증이 아니다. 실행기 자체가 사용자 서버/DB를 만들거나 삭제하지 않으므로 전용 환경 준비와 정리는 호출자가 맡는다.

서버 `python -m pytest -q tests/test_chat_participants.py tests/test_chat_storage.py`는 conftest의 임시 SQLite/이름이 remote_work_test_로 시작하는 폐기 가능 MySQL만 사용한다. 테스트는 행을 비우므로 공유 DB 금지다. 동시 동일 UUID 저장 검사는 실제 MySQL 전용이고 SQLite에서는 생략한다. fresh-process 등록/명시적 CLI 경계는 일회성 SQLite와 MySQL mock을 사용하며 실제 MySQL DDL 증거와 구분한다.
