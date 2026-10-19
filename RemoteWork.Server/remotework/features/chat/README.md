# 채팅 저장 실행·규약

1번의 2026-10-19 기술 회신에서 기존 API·참여자 권한·UUID 동일 요청 재시도·개인 숨김·독립 flag/Init-Chat 규약을 수용했다. 사용자도 협의 완료를 확인했다. 원본 PR #25/#26 병합과 운영 적용 승인은 별도다.

- 활성 직원·관리자는 채팅 전용 후보 API에서 본인을 제외한 활성 사용자의 ID·이름·부서를 조회한다. 공통 관리자 /users 권한을 바꾸지 않는다.
- 현재 참여자만 조회·전송할 수 있으며 관리자가 비참여자 방을 우회 조회하지 않는다. 개인 숨김은 자신의 hidden_at만 변경하며 다른 참여자·메시지·감사는 삭제하지 않는다. 새 메시지로 숨김을 복원하지 않는다.
- 동일 UUID/동일 요청은 재사용하며 내용이 다르면 409를 반환한다. 불명 전송의 앱 재시작 후 UUID 복구는 미구현이다.
- 채팅은 기본 off다. 공통 DB는 chat off의 새 프로세스에서 준비한 뒤, 명시적으로 채팅 테이블과 서버 활성화를 진행한다. Init-Chat.cmd 실행 후에도 서버 flag는 별도로 지정해야 한다.
- [005 DDL 및 적용 전제](../../../migrations/005_chat_storage.md)를 따른다. 기존 번호 없는 chat_storage.mysql.sql은 제거했다. 005는 1번 PR #25 head2173492의 SQL/안내를 수정 없이 가져온 개발용 자료다. SQL 실행과 ORM Init-Chat을 중복 migration으로 실행하지 않는다.
- ORM 생성은 InnoDB·utf8mb4·utf8mb4_unicode_ci를 명시한다. 공통 users의 INTEGER와 DB 문자셋/collation을 먼저 확인한다. 기존 테이블의 타입·길이·NULL·FK·인덱스·엔진·collation은 별도로 비교해야 하며 CLI의 열 이름 검사만으로 호환 완료를 주장하지 않는다. 자동 ALTER/DROP은 하지 않는다. 공통 schema version은 1이다.
- 약 1초 자동 조회는 WinForms 채팅 화면에서 실행한다. [동작과 제한](../../../../RemoteWork.WinForms/Features/Collaboration/CHAT_AUTO_RECEIVE.md)을 따른다. 전체 이력 조회 부하·차분 조회·서버 push는 향후 협의한다.

공유 DB 적용자·대상·백업/복구·비밀 전달·운영 보존/복원/탈퇴 정책과 물리 두 PC 검증은 별도 단계다. 이번 검증은 전용 폐기 DB와 합성 계정만 사용한다. 결과는 PR 본문의 실제 테스트 및 Tests/ChatAutoReceive.Tests/README.md를 확인한다.
