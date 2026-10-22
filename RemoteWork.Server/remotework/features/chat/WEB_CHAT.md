# 웹 채팅

기존 `/features/chat` 메뉴는 채팅 활성화 시 `/chat` 화면으로 이동한다.
클라이언트와 같은 개인/그룹 대화방, DB 활성 사용자 초대·부서 필터,
참여자·이전 메시지 조회, 메시지 전송, 본인 목록 숨김을 제공한다.
레이아웃은 왼쪽 대화방/참여자, 오른쪽 대화/메시지 입력으로 구성한다.
파일 전송 API가 없으므로 첨부 버튼은 비활성화한다.

## 규약과 공통 연결

- 원본 alpha `57a9b08`, LOG `df94afd`와 기존 C08/C09·005 및
  `RemoteWork.WinForms/Features/Collaboration/CHAT_AUTO_RECEIVE.md` 대조.
- 기존 `/api/v1/chat`의 Bearer 인증, DTO, UUID·문자 수·현재 참여자 검사,
  트랜잭션/감사, 005 DDL, 독립 활성화와 공통 schema version1을 유지한다.
- `storage.py`의 기존 처리를 공용 함수로 꺼내 두 채널에서 재사용한다.
  API 경로·성공/오류·조회 커서·같은 UUID 재시도 의미는 바꾸지 않는다.
- `/chat/data/*`는 4번 웹 화면용 어댑터다. `rw_session`의 기존 web 인증만
  사용하며 Cookie를 Bearer로 교환하거나 `/api`의 인증을 완화하지 않는다.
  POST는 공통 `protect_forms`의 form CSRF + JSON 문자열 payload를 받는다.
  현재 AuthSession에 결합한 HMAC `X-Chat-Session`을 추가로 대조하여 같은
  계정의 재로그인이나 다른 계정 전환 시 옛 페이지의 읽기/쓰기를 거절한다.
  토큰 원문·DB session hash는 페이지/URL/브라우저 저장소에 내보내지 않는다.
- `features/registry.py`에서 기존 채팅 활성화와 함께 등록한다. 새 flag/DB
  테이블·migration 번호·권한 정책·공통 W1 receipt를 추가하지 않는다.

## 자동 수신과 응답 유실

표시 중인 화면에서 기존과 같은 1초 주기 조회를 사용한다. 한 체인만 실행하고
실패 시 2→4→8→10초 대기한다. 기존 after/limit 페이지를 모두 읽은 후 반영한다.
선택 변경/쓰기·화면 숨김은 진행 중인 조회를 무효화하고 늦은 응답을 버린다.
새 메시지가 없으면 본문을 다시 그리지 않고 초안과 스크롤을 보존한다.
전송은 Enter, 줄바꿈은 Shift+Enter이며 IME 조합 중 Enter는 전송하지 않는다.

방/메시지 ID는 `crypto.getRandomValues`로 UUID를 생성하므로 LAN HTTP에서도
`crypto.randomUUID`/secure-context 요구에 의존하지 않는다. 방 이름60자,
메시지2,000자는 Python과 같은 Unicode code point 단위로 검사한다.
본문은 DOM textContent로 표시한다. localStorage/sessionStorage에 저장하지 않는다.

전송 실패/응답 불명 시 원래 UUID·본문을 메모리에 보존한다. 자동 POST 재시도는
없으며 다음 사용자 클릭만 같은 요청을 보낸다. 목록에서 그 UUID를 확인하면
저장 성공으로 복구한다. 저장 성공 뒤 목록 조회 실패는 재전송 사유가 아니다.
화면 종료/새로고침으로 잃은 미확정 요청의 UUID 복구는 기존 클라이언트와 같이
미지원이다. 다시 전송하기 전 저장된 대화를 확인해야 한다.

## 적용·검증

기존 `REMOTEWORK_ENABLE_CHAT_DRAFT=1`과 검토된 005가 준비된 서버에서
업데이트된 서버 코드를 재시작하면 메뉴가 연결된다. 화면/요청에서 CREATE,
ALTER·초기화·seed를 실행하지 않는다. 다른 PC의 1번 서버를 사용하는 경우에는
그 서버에도 코드 배포가 필요하며, 바탕화면 파일 복사만으로 원격 배포되지 않는다.

`Tests/WebChat.Tests/README.md`의 소유 MySQL 검사와 실제 브라우저 검사를
참고한다. SQLite·실사용 DB·물리 두 PC와 독립 시험을 구분한다.
