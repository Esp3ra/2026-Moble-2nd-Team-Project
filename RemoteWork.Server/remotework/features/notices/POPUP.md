# 공지 팝업 — 별도 opt-in 로컬 구현안 (2026-10-20)

상태: 기본 공지와 분리한 **개발 검토안**. 설정 이름·독립 등록·명시 준비 방향은1번이 기술 수용했다. 실제008 SQL·삭제/FK·보존·확인/재표시·공유 적용은 미합의이며 이번 Draft 코드 PR로 검토한다.

## 기본 공지와 분리

- `REMOTEWORK_ENABLE_NOTICE_DRAFT=1`만 켜면 기존 공지 목록/상세/등록/삭제·웹 화면만 등록한다. 기본 DTO에 popup_key를 추가하지 않는다.
- 제안 설정 `REMOTEWORK_ENABLE_NOTICE_POPUP_DRAFT=1`도 켠 경우에만 별도 `notice_popups` Blueprint가 기존 로컬 pending/confirm API를 제공한다. 테스트/앱의 명시적 `ENABLE_NOTICE_POPUP_DRAFT=False`가 환경변수보다 우선한다. 공지가 꺼져 있으면 팝업만 켜도 등록되지 않는다. 기본0.
- `popup_models.py`의 `PopupBase`는 공통 Base와 다른 metadata를 사용한다. 같은 프로세스에서 팝업을 켰다 꺼도 공통 init-db가 확인 테이블을 생성하지 않는다. API 요청/서버 시작에서도 DDL을 실행하지 않는다.
- `notices init-table`은 기존 notices만 준비한다. users/notices를 먼저 준비한 로컬 개발 DB에서만 별도 `notice-popups init-table`이 확인 테이블을 추가한다. 기존 열·행을 자동 이관/삭제하지 않으며 기존 열 타입·null·PK·FK가 다르면 중단한다. 이 검사는 정식 migration 승인이나 전체 물리 호환/보존 정책 승인이 아니다.

```powershell
# 아래는 검토할 개발안. 공유 DB에 실행하지 않음.
$env:REMOTEWORK_ENABLE_NOTICE_DRAFT = "1"
$env:REMOTEWORK_ENABLE_NOTICE_POPUP_DRAFT = "1"
.\.venv\Scripts\python.exe -m flask --app remotework notices init-table
.\.venv\Scripts\python.exe -m flask --app remotework notice-popups init-table
```

기존 `python -m remotework.features.notices.prepare_popup`도 두 옵션과 선행 테이블을 요구하는 같은 명시 준비 함수를 호출한다. 옵션을 자동으로 켜지 않는다.

## 보존한 로컬 동작·미합의

- 로그인 기존 목록을 표시 없이 기준으로 읽고 이후15초 주기 새 상단 고정 공지를 조회하는 기존 WinForms 구현 유지. 전직원/현재 부서 직원 대상. 이것은 기존 로컬 정책이며 팀장 승인된 새 공통 계약이 아니다.
- pending 응답에만 popup_key를 포함한다. 기본 목록/등록/상세 DTO는 원본 기준이다.
- 확인 저장 중복 요청은 기존 로컬 구현처럼 확인1행·감사1행을 유지한다. notice_id/user_id 복합PK, 공지 FK의 CASCADE는 기존 로컬 구조를 보존한 **검토 대상**이며 정식 승인되지 않았다. 확인 주체의 최신 권한·부서/접속 변경 경합, 보존·다중PC·수정 후 재확인·시작 경계/cursor는1번/팀장 협의사항으로 남긴다.
- 팝업 옵션이 꺼져 경로가404이면 직원 클라이언트는 해당 서버에서 팝업을 제공하지 않는 것으로 표시하고 주기 조회를 멈춘다. 서버에서 옵션을 바꾼 후에는 직원 창/접속을 새로 시작한다. 잘못된 서버 주소와 기능 미제공을404만으로 세밀하게 구분한 계약은 아니다. 다른503/통신 실패는 오류 안내/재시도를 유지한다.
- 끈다고 이미 있는 확인 기록을 삭제하지 않는다. 공유/운영 DB 및 현재 실행 DB의 팝업 테이블은 이번에 변경하지 않았다.

검증 결과·명령/미실행은 작업 폴더 외부의 로컬 검증 기록과1번에게 전달하는 통합 회신에서 구분한다. 과거 POPUP.md의 빌드/검사 미실행 설명은 이전 구현 시점의 기록이며 이번 결과로 재사용하지 않는다.



## 1번 회신 수신 / 검토 경계

[1번 2026-10-20 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/43#issuecomment-6283590252)에서 설정명/분리·명시 준비 방향을 기술 수용하고008 작성·검토 번호를 예약했다. 물리DDL·CASCADE/삭제보존·로그인 경계·공유적용은 미합의다. `migrations/008_notice_popups.mysql.sql`은 CASCADE가 있는 기존 로컬 구조의 **격리 개발 검토안**이며 공유환경 적용 SQL이 아니다.

후속 변경에서는 직원 MainForm에 코디네이터1개를 연결하고 만료/로그아웃 정리 접점을 최소 추가했다. 기존 업무/채팅/근태 생성과 공통 인증은 유지한다. 실제 MainForm/HTTP/임시MySQL 자동표시·확인저장 흐름을 검증했으며 MainForm 담당1번의 접점 검토를 요청한다. 새 counter/cursor·주기·보존 정책의 구현/승인으로 표시하지 않는다.

## 추가 회귀·아직 남은 경계

확인은 기존 공통 authenticated_transaction(roles=employee)을 재사용해 guard→현재actor→현재session→notice 순으로 검사한다. 공통 모듈/잠금 순서를 바꾸지 않는다. 요청 identity 로드 뒤 role/활성/부서/접속이 바뀌면 확인을 거절하는 대역 회귀를 추가했다. pending의 최종 로그인 경계·서버 cursor/수신 시점·부서 변경 운영 정책 전체를 구현한 것으로 확대하지 않는다.

클라이언트 코디네이터와 팝업 HTTP는 CaptureSession에 묶으며 동일 계정의 새 로그인도 이전 수신/확인을 재사용하지 않는다. 부서가 바뀌면 이전 창 문맥의 표시를 차단한다. 새 부서의 재기준/수신 정책과 MainForm 연결 구현은 추가했으나 담당1번의 접점 검토 및 실제 DB의 보존 구조 적용은 별도다. pending 자체404만 polling 중단으로 처리하며 확인폼의 개별 NOTICE_NOT_FOUND404는 다른 팝업의 수신을 중단시키지 않는다.

## 사용자 후속 결정 — 확인 기록 보존 (2026-10-20)

목적은 관리자 새 중요공지→접속중 전직원/본인부서 직원 자동표시→확인DB저장이다. MainForm 자동연결을 추가하고 실제 WinForms/HTTP/전용MySQL에서9항목과 확인2행/감사2행을 직접 확인했다. 검사에서만 주기를100ms로단축하고 제품15초기본값을먼저확인했다. 실제사용자DB/두PC/보존정책의완료로확대하지않는다.

사용자가 공지삭제후에도 확인기록을보존하도록선택했다. **현재raw008/기존모델의CASCADE는최종요구와충돌하여배포할수없는비교검토안**이다. RESTRICT로기본삭제를막거나동의없이논리삭제를넣지않는다. 공지참조와원본식별/확인증거분리, nullable SET NULL참조와불변원본공지정보보존등은1번과검토할제안이며필드명/최종DDL은아직구현·합의하지않았다. 보존구조확정/검증전실제앱DB팝업DDL·자동활성화는보류한다.
