# 채팅 자동 수신 검증

```powershell
dotnet run --project Tests/ChatAutoReceive.Tests/ChatAutoReceive.Tests.csproj -c Release
```

실제 WinForms·제품 HttpChatService/ApiClient·명시적 HTTP 대역으로32검사를실행한다. 자동방/메시지표시·초안/선택/스크롤보존·같은결과재렌더방지·늦은응답·방전환·전송과경합·중복poll방지·화면숨김/재표시·실패backoff/복구·로그아웃중지·반복Dispose를확인한다. 대역의응답은시험용이며실제DB결과가아니다.

아래모드는삭제가능전용DB/서버와합성계정staff1/Staff-test-2026!,admin1/Admin-test-2026!을준비한뒤에만실행한다. 공용서버/DB를지정하지않는다.

```powershell
dotnet run --project Tests/ChatAutoReceive.Tests/ChatAutoReceive.Tests.csproj -c Release -- --http http://127.0.0.1:시험포트
```

실제두WinForms채팅화면→TCPHTTP→DB11검사다. 방자동수신·양방향자동메시지·초안보존·같은세션정보갱신·개인숨김자동반영·상대기록유지·로그아웃중지를확인한다. 물리적인두PC가아닌한PC두계정이며, 실행기자체는서버/DB를준비하거나초기화하지않는다.

2026-10-19로컬수정본:32대역검사및소유MySQL8.2/REPEATABLE READ의실제HTTP11검사통과. 서버rawchatDDL적용후검사했고소유서버/MySQL/temp정리를확인했다. 기존ChatStorage40·ChatParticipants47도같은새제품DLL에서재실행통과. Release경고0/오류0. 실제사용자서버·공유DB·물리두PC·장기부하미실행.


## PR21 합의 반영 후 재검증 — 2026-10-19

기준: 기존 PR21 head907ae77 + 최신 원본 alpha bbbe43a. 1번 PR25 head2173492의 005 SQL/안내를 동일 내용으로 포함하고 번호 없는 SQL을 제거했다. ORM은 InnoDB·utf8mb4·utf8mb4_unicode_ci를 명시한다.

- Release 제품·자동 수신·초대 테스트 프로젝트: 경고0/오류0.
- 자동 수신 실제 WinForms+HTTP 대역32, 기존 HTTP 대역40·UI 대역47, 실제 cmd/Flask 대역 Init-Chat 환경 격리9 통과.
- 검토된 raw005를 적용한 전용 MySQL8.2/REPEATABLE READ에서 실제 WinForms→TCP→서버/DB 자동 수신11 통과. 한 PC 두 계정으로 초대·양방향 자동 메시지·개인 숨김·상대 기록·로그아웃을 검사했다.
- 서버 pytest30 통과: 기존28에 모델의 MySQL DDL 옵션 컴파일 검사1, 실제 MySQL3테이블 엔진/문자셋/collation 검사1을 추가했다. 기존28은 실제 MySQL18·cold등록/CLI 경계10으로 구분한다. 실제 물리 스키마 전체/공유 DB 호환성을 인증한 결과는 아니다.
- 물리 두 PC, 사용자 운영 서버/DB, 공유 적용, 장시간 부하, 실제 dump/restore는 이번에 실행하지 않았다. 1번이 보고한 복구 검증을 작성자 실행 결과에 합산하지 않는다.

재현 명령:
```powershell
dotnet run --project Tests/ChatAutoReceive.Tests/ChatAutoReceive.Tests.csproj -c Release
dotnet run --project Tests/ChatStorage.Tests/ChatStorage.Tests.csproj -c Release
dotnet run --project Tests/ChatParticipants.Tests/ChatParticipants.Tests.csproj -c Release
python Tests/ChatStorage.Tests/check_init_chat_scope.py
# RemoteWork.Server에서 전용 remote_work_test_ DB만 지정한 뒤 실행
python -m pytest -q tests/test_chat_participants.py tests/test_chat_storage.py tests/test_chat_collation.py
```
실제 HTTP 모드는 위 --http 절차를 따르며, 작성자는 소유 임시 MySQL·서버를 별도로 띄워 실행 후 정리했다. 사용자 .env/공유 서비스는 사용하지 않았다.
