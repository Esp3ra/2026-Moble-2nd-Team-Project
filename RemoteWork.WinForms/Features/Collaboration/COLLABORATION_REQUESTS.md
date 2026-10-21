# 협업 요청 저장·응답 — 4번 구현 및 연결 검토안

기준: 원본 alpha 395d70d / LOG 41af6c5(2026-10-21), LOG/2번 공통규약 B1~B7,
1번 PR43 기술 회신 및 Sang1 PR1의 직원 디렉터리 재사용 회신.
직원 조회 재사용 수용을 새 협업 저장 API/DDL 승인으로 확대하지 않는다.

## 구현

- 요청할 업무: 기존 2번 `TaskReadSession`, 본인 진행중·미취소 업무. 직원: 기존 최소 디렉터리,
  활성 직원/관리자 중 본인 제외, 동명이인 ID 표시. 조회 실패를 빈 성공/샘플로 대체하지 않는다.
- `CollaborationRequestForm`: 실제 서버 쓰기 준비·요청 키 취득·전송. 저장 확인 후만 입력 초기화.
  `받은·보낸 요청`에서 저장 내역과 승인·거절 결과를 조회한다.
- `CollaborationResponseForm`: 우측 하단 약390×275 크기의 비모달 창. 승인/거절, 닫기=보류.
  수락 후 요청자는 완료/철회, 수신자는 참여 종료. 서버가 다시 현재 상태/역할/버전을 검사한다.
- `CollaborationNotificationCoordinator`: 받은 pending 및 보낸 요청의 응답을 한 창씩 표시한다.
  조회 범위는 해당 로그인 당사자뿐이다. 현재는 협업 요청 창이 열린 동안의 로컬 연결이다.
  **모든 메뉴에서 수신하려면 1번 MainForm의 로그인 수명에 연결해야 한다. 해당 파일은 수정하지 않았다.**
- 로그아웃·서버 전환 시 창을 닫고 이전 로그인 비동기 응답을 무시한다.
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

공통 `/write-requests/keys`와 `/write-requests/result`를 재사용하며 행동은
`collaboration.created/accepted/declined/withdrawn/completed/left`다.
결과 영수증에는 request_id/status/row_version/updated_at만 둔다. 제목·본문을 보관하지 않는다.

새 테이블 `collaboration_requests`, `collaboration_events`는 별도 metadata에 둔다.
공통 init-db는 이 테이블을 만들지 않는다. `collaboration.proposal.mysql.sql`은 **미번호 DDL 초안**이며
010/011 등 다른 담당 migration을 대체하지 않는다. 삭제 CASCADE/자동 물리 삭제는 없다.
현재 배정·대상별 pending+accepted 합계1을 생성열+UNIQUE로 제한한다.
요청/상태/이력/감사/W1 성공 영수증은 같은 트랜잭션이다. 만료·무효화는 실패 응답 때도 별도 보호 TX로 보존한다.

`bootstrap.create_collaboration_app` 또는 `install(app, task_context=...)`를 1번이 명시 연결한다.
개발 설정 제안 `ENABLE_COLLABORATION_DRAFT=True`, 공통 쓰기=True가 필요하다.
DDL은 백업·물리 구조 대조 후 담당자가 적용한다. `storage.create_missing_storage(..., reviewed=True)`는
기존 테이블이 하나라도 있으면 자동 보수/ALTER를 거절한다. 일반 앱 시작에서 호출하지 않는다.

## 확정 기준 반영

- task_id/row_version/task_row_version: signed BIGINT, 정규 양의 JSON 문자열.
  assignment_revision: signed INTEGER JSON 정수. 요청 식별자는 정규 UUID 문자열이라는 **기술 제안**이다.
- 생성/수락은 현재 본인 담당·진행중·미취소, 활성 대상, 기대 배정·버전을 다시 확인한다.
  협업 승인으로 업무 배정·own/managed·원문/파일/결과물 조회/제출 권한을 부여하지 않는다.
- KST 오늘~업무 마감 희망일, pending은 희망일 종료 후 만료, accepted는 지연 표시만 한다.
- 제출/승인/취소/재배정은 관계 종료다. 관리자 변경만으로 자동 종료하지 않는다.
  **배정 차수만으로 중간 상태 변화를 증명하지 않는다.** `task_bridge`는 버전 불변이면 연속성을 확인하고,
  버전 변경 시 2번 이력 검증 어댑터의 True/False/None 결과를 요구한다.
  미연결/판정 불명은 수락·완료503으로 막는다. 테스트용 True/False 람다를 운영에 설치하지 않는다.
- 종료 후90일 당사자 조회, 물리 삭제/백업 보존은 별도 정책이다.

## 남은 연결 협의

1. 1번: 행동명/DTO/오류·명시 등록 설정/공통 W1 연결, DDL 번호·물리 적용·배포·복구.
2. 1·2번: 현재 업무/취소/중간 상태 이력의 읽기 계약. 버전 변경 후 연속성 판정 경로.
   관리자만 변경된 경우와 제출 후 복귀한 경우를 구별해야 한다.
3. 1번 MainForm: 로그인당 coordinator 1개, 로그아웃/종료 Dispose, 메뉴와 독립된 수신.
   생성 시 `new CollaborationNotificationCoordinator(this, service, agreedInterval)` 후 `StartAsync()`.
4. 표시 정책: 현재10초 폴링·닫은 버전은 해당 창 수명 동안 재표시하지 않음·재로그인 pending 표시 등은
   로컬 제안이다. 공지 확인 정책/마감 알림 주기와 섞지 않는다. 공통 합의로 간주하지 않는다.

이 기술 연결을 확인하기 전에는 PR을 Draft로 유지한다. 구현·격리 검사 통과와 공유 서버/실제 두 PC 완료는 다르다.
