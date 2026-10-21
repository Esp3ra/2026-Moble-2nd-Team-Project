# 협업 요청 저장·응답 — 4번 구현 및 연결 검토안

기준: 원본 alpha 8ed25c6 / LOG ed2978e(2026-10-21), LOG/2번 공통규약 B1~B7,
팀장 PR57 검토와1번 Sang1 PR1의14:15KST 기술 회신·PR43을 대조했다.
직원 조회 재사용 수용을 새 협업 저장 API/DDL 승인으로 확대하지 않는다.

## 구현

- 요청할 업무: 기존 2번 `TaskReadSession`, 본인 진행중·미취소 업무. 직원: 기존 최소 디렉터리,
  활성 직원/관리자 중 본인 제외, 동명이인 ID 표시. 조회 실패를 빈 성공/샘플로 대체하지 않는다.
- `CollaborationRequestForm`: 실제 서버 쓰기 준비·요청 키 취득·전송. 저장 확인 후만 입력 초기화.
  `받은·보낸 요청`에서 저장 내역과 승인·거절 결과를 조회한다.
- 결과불명→미기록 확인→사용자 재전송의 확정4xx 실패는 입력을 보존하면서 잠금을 풀고 새 준비로 돌아간다.
  실패한 결과조회나 결과불명은 원래 키/본문을 유지한다. 자동 쓰기 재시도는 없다.
- `CollaborationResponseForm`: 우측 하단 약390×275 크기의 비모달 창. 승인/거절, 닫기=보류.
  수락 후 요청자는 완료/철회, 수신자는 참여 종료. 서버가 다시 현재 상태/역할/버전을 검사한다.
- `CollaborationNotificationCoordinator`: 받은 pending 및 보낸 요청의 응답을 한 창씩 표시한다.
  조회 범위는 해당 로그인 당사자뿐이다. 현재는 협업 요청 창이 열린 동안의 로컬 연결이다.
  **모든 메뉴에서 수신하려면 1번 MainForm의 로그인 수명에 연결해야 한다. 해당 파일은 수정하지 않았다.**
- 로그아웃·서버 전환 시 창을 닫고 이전 로그인 비동기 응답을 무시한다.
- B4 지연은 목록·응답 창에 표시한다. B7은 `상태 이력` 버튼으로 협업 전용 사건을 조회한다.
  원업무/파일 API를 호출하거나 권한을 확장하지 않는다.
- 실패 시 성공으로 표시하지 않는다. 결과 불명은 공통 W1 결과 조회를 먼저 사용하고,
  미기록일 때만 사용자 선택으로 정확히 같은 키·본문을 재전송한다. 자동 쓰기 재시도는 없다.

## 서버/DB 제안과 실제 배포 경계

사용자 지시에 따라 이후 DB 작업·검증은 MySQL만 사용한다. 이번 테스트에 다른 DB 자동 대체는 없다.

`RemoteWork.Server/remotework/features/collaboration`은 명시 설치형 새 기능이다.
기존 `create_app`/registry/run.py, 공통 인증/DB, MainForm, 2·3·5번 구현은 변경하지 않았다.
일반 서버 실행만으로 이 API가 생기거나 DB가 바뀌지 않는다. 공유 서버 배포는 미실행이다.

| 제안 API | 기능 |
|---|---|
| GET `/api/v1/collaboration-requests/task-context/{task_id}` | 본인 진행중 업무의 현재 배정·버전·마감·이 응답의 서버시각 |
| POST `/api/v1/collaboration-requests` | pending 요청 저장 |
| GET `/api/v1/collaboration-requests?scope=received\|sent` | 당사자 최소 요청 목록·상태 조회 |
| POST `/api/v1/collaboration-requests/{request_id}/actions/{decision}` | accept/decline/withdraw/complete/leave |
| GET `/api/v1/collaboration-requests/{request_id}/history` | 당사자의 협업 상태이력(event_id/status/reason/actor_id/occurred_at) |

history 경로는 이번 B7 보완의 신규 읽기 계약으로1번 재검토 대상이다.

공통 `/write-requests/keys`와 `/write-requests/result`를 재사용하며 행동은
`collaboration.created/accepted/declined/withdrawn/completed/left`다.
결과 영수증에는 request_id/status/row_version/updated_at만 둔다. 제목·본문을 보관하지 않는다.

새 테이블 `collaboration_requests`, `collaboration_events`는 별도 metadata에 둔다.
공통 init-db는 이 테이블을 만들지 않는다. 1번이 예약한 정식 파일은
`RemoteWork.Server/migrations/012_collaboration_requests.mysql.sql`이다.
012 예약/파일 제공은 schema_versions=12·실제 공유 DB 적용을 뜻하지 않는다.
공통 schema1·기존010/011을 보존한다. UPDATE/DELETE CASCADE·자동 물리 삭제는 없다.
InnoDB/utf8mb4/utf8mb4_unicode_ci, signed 타입/nullable/default/auto_increment,
PK/FK·필수 인덱스·생성열/UNIQUE·실효 CHECK를 대조한다. 불일치는503/무변경이다.
명시/상속 utf8mb4 표현 차이는 실제 문자셋을 검증한 뒤 동일하게 취급한다.
현재 배정·대상별 pending+accepted 합계1을 생성열+UNIQUE로 제한한다.
요청/상태/이력/감사/W1 성공 영수증은 같은 트랜잭션이다. 만료·무효화는 실패 응답 때도 별도 보호 TX로 보존한다.

`bootstrap.create_collaboration_app` 또는 `install(app, task_context=...)`를 1번이 명시 연결한다.
개발 설정 제안 `ENABLE_COLLABORATION_DRAFT=True`, 공통 쓰기=True가 필요하다.
같은 명시 설치를 `ENABLE_COLLABORATION_DRAFT=False`로 호출하면 공통
`register_write_result_reader(read_only=True)`만 등록한다. 콜드 OFF에서도 현재 권한·저장소·TTL을
검사한 기존 성공 결과를 읽고, 새 키503·업무 변경 경로404로 쓰기를 거부한다.
결과만 설치할 때는 `install_result_readers(app, task_context=..., read_only=True)`를 명시 호출한다.
일반 create_app 등록이나 TTL/공통 API는 변경하지 않았다.
DDL은 백업·물리 구조 대조 후 담당자가 적용한다. `storage.create_missing_storage(..., reviewed=True)`는
기존 테이블이 하나라도 있으면 자동 보수/ALTER를 거절한다. 일반 앱 시작에서 호출하지 않는다.

## 확정 기준 반영

- task_id/row_version/task_row_version: signed BIGINT, 정규 양의 JSON 문자열.
  assignment_revision: signed INTEGER JSON 정수. 요청 식별자는 정규 UUID 문자열이라는 **기술 제안**이다.
- 생성/수락은 현재 본인 담당·진행중·미취소, 활성 대상, 기대 배정·버전을 다시 확인한다.
  협업 승인으로 업무 배정·own/managed·원문/파일/결과물 조회/제출 권한을 부여하지 않는다.
- 상대 비활성화는 현재 행위자의 거절·철회·참여종료·완료를 막지 않는다. 새 수락은 양측 활성 검사를 유지한다.
- task/assignment와 취소 원본을 같은 보호 TX에서 CURRENT 잠금 조회한다.
  2번 storage_present의 실효 저장소 검사를 재사용하고4번 bridge에서 취소 행을 FOR UPDATE로 읽는다.
  2번 기존 ensure_not_cancelled를 대신 수정하지 않았고 로그인 RR snapshot으로 취소를 판정하지 않는다.
- KST 오늘~업무 마감 희망일, pending은 희망일 종료 후 만료, accepted는 지연 표시만 한다.
- 제출/승인/취소/재배정은 관계 종료다. 관리자 변경만으로 자동 종료하지 않는다.
  **배정 차수만으로 중간 상태 변화를 증명하지 않는다.** `task_bridge`는 버전 불변이면 연속성을 확인하고,
  버전 변경 시 2번 이력 검증 어댑터의 True/False/None 결과를 요구한다.
  미연결/판정 불명은 수락·완료503으로 막는다. 테스트용 True/False 람다를 운영에 설치하지 않는다.
- 종료 후90일 당사자 조회, 물리 삭제/백업 보존은 별도 정책이다.

## 남은 연결 협의

1. 1번: 수용한 명시 등록 방향·6행동/W1·예약012의 실제 공통 조립과 신규 history 읽기 대조.
   물리 검증·격리 dump/새DB 복원은 검사했지만 공유 DB 백업·적용/재시작은 미실행이다.
2. 1·2번: 현재 업무/취소/중간 상태 이력의 읽기 계약. 버전 변경 후 연속성 판정 경로.
   관리자만 변경된 경우와 제출 후 복귀한 경우를 구별해야 한다.
3. 1번 MainForm: 로그인당 `CollaborationNotificationScope(this, service, agreedInterval)`를1개 만들고
   `StartAsync()` 호출, 로그아웃/종료 Dispose로 소유한다. 해당 파일은 수정하지 않았다.
   현행 TasksControl 진입을 유지해도 폼이 Owner 체인에서 scope를 찾아 빌린다.
   명시 주입은 `CollaborationRequestForm(source, service, sharedNotifications: scope)`다.
   빌린 폼은 별도 타이머/큐를 만들지 않고 닫혀도 공유 scope를 Dispose하지 않는다.
   공유 scope가 없는 독립 폼만 자체 수신기를 소유한다. 이미 보이는 중요공지/협업 응답 창에는 표시를 양보한다.
   중요공지가 나중에 들어오는 역방향을 포함한 실제 동시 통합은1번 조립 뒤 추가 확인한다.
4. 표시 정책: 현재10초 폴링·닫은 버전은 해당 창 수명 동안 재표시하지 않음·재로그인 pending 표시 등은
   로컬 제안이다. 공지 확인 정책/마감 알림 주기와 섞지 않는다. 공통 합의로 간주하지 않는다.

이 기술 연결을 확인하기 전에는 PR을 Draft로 유지한다. 구현·격리 검사 통과와 공유 서버/실제 두 PC 완료는 다르다.

## PR57 보완과 검사

팀장R57-01/02/03은 CURRENT 취소 읽기/비활성 상대 종료/409 뒤 입력 복구,
1번R57-01/02는 콜드OFF 결과읽기/보존형 FK·물리 검사로 구분한다.
1번I57-01에는 공유/자체 수신 소유 분리로 대응했다. B4/B7 표시·이력 읽기도 보완했다.
`tests/test_collaboration_review.py`는 실제 MySQL RR guard 대기3경로·비활성 상대5경로·콜드OFF/권한/TTL·
물리 오류8종·당사자 이력/90일·raw012 적용·합성자료 dump/새 소유DB 복원 검사를 추가한다.
취소 반대편은 실제2번 HTTP/통합ACK가 아닌 명시적 소유DB 보호 TX 대역이다.
UI 검사는 실제 WinForms+명시 서비스/HTTP 대역이며 공유 서버/두 PC를 대신하지 않는다.
