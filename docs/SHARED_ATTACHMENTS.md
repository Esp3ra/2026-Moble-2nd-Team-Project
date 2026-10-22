# 3번 공용 첨부 제공 계약 — 로컬 구현, 소비/DB 반영 전 검토

사용자 확정 정책: 업무 등록 첨부와 제출 회차 첨부를 지원한다. 현재 담당자·업무 관리자·검토자만 다운로드하며 관계 변경 후 재검사한다. 업무 등록/교체/취소와 파일 연결/해제는 같은 DB 트랜잭션으로 처리한다. A의 연결 제거가 B를 지우지 않으며 마지막 연결이 없을 때만 파일을 정리한다. 업무 취소 시 업무 첨부만 해제하고 이전 제출 파일은 보존한다. 기존25MiB·10개·임시24시간 기준을 유지한다.

## 변경 경계와 협업 요청

- 3번 소유 `shared_attachments.py` 1개와 기존 `results.py` 연결 부분을 구현한다. 기존4폼, 공통 인증/통신/registry, 2번 제품 코드는 직접 변경하지 않는다.
- 2번은 업무 등록/수정/취소 TX 내부에서 아래 함수를 호출해야 한다. 3번 함수 자체는 commit/rollback을 하지 않는다. 업무 버전 증가·공통 receipt·업무 이력은 기존2번 처리와 함께 유지한다.
- 1번이 추가3테이블/FK/인덱스를 검토하고 예약013을 `migrations/013_shared_attachments.mysql.sql`로 일원화했다. 기존 draft SQL의 내용은 동일하며 006과 공통 schema1은 변경하지 않는다. 정식 검토 파일의 번호화는 공유 DB 적용·배포 ACK 승인이 아니다.
- 위 내용은 구현을 위한 제공 계약이며 다른 담당자가 수신/연결/공유 적용했다는 뜻은 아니다. 새 자원 종류는 task와 submission만 지원한다. 공지/협업 요청 등으로 권한을 임의 확대하지 않는다.

## 활성화와 저장

기존 결과물 활성화에 더해 `REMOTEWORK_ENABLE_SHARED_ATTACHMENTS_DRAFT=1`을 명시한다. 기본 off에서는 새 모델·경로를 등록하지 않는다. DB를 자동 생성하거나 초기화하지 않는다. 기존 `RESULTS_STORAGE_ROOT/shared`를 사용해 기존 결과물 파일 전송 코드와 검증을 재사용한다. 실제 DB에는 SQL을 자동 적용하지 않는다.

## 2번 호출 지점

`authenticated_transaction()`이 시작한 같은 db/session에서만 호출한다.

- `replace_task_files(db, task_id, attachment_ids, expected=ExpectedVersions(...))`: 현재 업무 관리자 확인, 취소 차단, 버전/배정 재확인 후 전체 첨부 목록 교체. 업무 등록에서는 업무/배정/관리자 행을 flush한 뒤, 같은 TX에서 호출한다. 임시 파일은 올린 사람만 최초 연결할 수 있다. 이미 연결된 파일을 B로 공유하려면 A의 현재 열람 권한도 필요하다.
- `release_task_files(db, task_id, expected=...)`: 업무 취소 TX에서 업무 등록 첨부만 해제한다. 제출 참조는 건드리지 않는다. 성공 후 물리 파일 정리는 요청 종료 단계 또는 명시적 `cleanup_shared_files()`가 수행하며 실패 시 deleting 상태로 재시도한다.
- 등록을 취소한 업로드는 DELETE 업로드 API로 명시 해제한다. 연결 확정 뒤에는 업로드 삭제 API로 지울 수 없다. 사용자 입력 형식 오류/저장 실패의 TX rollback은 기존 참조를 유지한다.

2번이 연결하기 전에는 실제 업무 등록/취소 화면의 공용 첨부 처리가 완료됐다고 표시하지 않는다. 이행 순서는 3번 제공부 검증 → 1번 DB 검토·2번 TX/화면 소비 → 실제 환경 통합 검증이다.

## HTTP 제공부

기존 API 응답 envelope·Bearer 인증을 사용한다. 파일 ID는 UUID이며 이름·크기·sha256 반환 형식은 기존 결과물 첨부와 같다.

| 경로 | 입력/동작 |
|---|---|
| `POST /api/v1/results/shared/uploads/start` | `{name,size,sha256}`. 업무 등록 전 임시는 관리자만 가능. 기존 업무에서는 `task_id`를 BIGINT 문자열로 추가하며 현재 담당자/업무 관리자·취소 상태를 검사 |
| `POST .../uploads/{id}/chunks` | `{offset,data}`. 기존16KiB/base64 전송 코드 재사용. 기존 업무의 현재 권한·취소·직원 제출 조건을 파일 IO 전후 재확인 |
| `POST .../uploads/{id}/complete` | `{}`. 전체 크기·해시 검사. 연결 확정 후에는 업로드를 수정하지 못함 |
| `DELETE .../uploads/{id}` | 본인 미연결 임시 업로드만 해제. 다른 자원에서 사용 중인 파일을 삭제하는 경로가 아님 |
| `GET .../tasks/{task_id}` | 현재 열람 권한의 업무 등록 첨부 목록. 취소 업무의 등록 첨부 조회 차단 |
| `GET .../files/{id}/download` | 현재 접근 가능한 참조가 하나라도 있는 파일만 다운로드. file_id 소지만으로 허용하지 않음 |
| `GET .../files/{id}/chunks?offset=0` | 기존 결과물과 동일한 조각 다운로드 형식 |

업무 등록·교체·취소를 저장하는 별도 HTTP 경로는 중복 구현하지 않는다. 위 서버 함수를 2번의 기존 쓰기 처리/receipt TX 안에서 **업무 버전 증가 전** 호출해야 한다. 소비자가 임의로 두 번 commit하거나 파일 연결만 먼저 저장하면 안 된다.

공용 파일을 기존 결과물 제출의 attachment_ids에 포함하면 같은 제출 TX에서 submission 참조를 만든다. 기존 결과물 목록/상세/다운로드 응답도 공용 파일을 읽으며 제출 이력은 수정하지 않는다. 이미 저장된 제출물에는 공용 파일을 나중에 추가/교체할 수 없다. 새 회차를 제출해야 한다.

## SQL·정리

추가 테이블은 파일 메타정보 `result_shared_files`, 전송 조각 `result_shared_chunks`, 자원별 연결 `result_shared_links`다. 업무 취소 상태나 업무 상태를 별도로 복제하지 않는다. 기존 파일 저장소의 shared 하위 경로를 쓴다. 업무 등록 전에 올린 파일을 기존의 필수 task_id/단일 submission_id에 억지로 넣거나 가짜 업무를 생성하지 않는다.

최종 참조 해제 시 같은 TX에서 파일을 deleting 상태로 표시하고, 요청 종료 후 별도 정리 TX와 파일 IO로 실제 삭제한다. 삭제 실패는 deleting을 유지하며 기존 앱 팩토리/설정에 연결한 `shared-attachments cleanup` CLI 또는 `cleanup_shared_files()`로 재시도한다. 연결/해제 TX가 rollback되면 정리 대상이 되지 않는다. 24시간이 지난 파일도 살아 있는 참조가 있으면 지우지 않는다.

## R52-03 수정·검증 — 2026-10-21

기준 alpha `44c85f322d22a3f48422353aedb45a09e8970e3c`를 로컬 결합했다. 공용 참조 조회 `_links`와 업무 교체/해제의 참조 목록 조회를 `FOR UPDATE` 및 `populate_existing=True`로 변경했다. 인증에서 만들어진 REPEATABLE READ snapshot에 의존하지 않고, 기존 공통 guard 안에서 현재 commit된 참조와 같은 TX의 변경을 읽는다. 별도 commit·전역 격리 수준 변경·새 정책·DDL·폼 변경은 없다.

`verify_shared.py`의 실제 인증 뒤/guard 전 대기 장치는 테스트 전용이다. A만 보이는 snapshot → 별도 HTTP 요청의 B 연결 commit → A 해제 순서를 실제 소유 MySQL에서 재현했다. 수정 전에는 B 참조가 남았는데 ready 상태 검사에 실패했다. 수정 후에는 B 참조·ready 상태·원본 다운로드·정리 후 바이트가 유지되고, 마지막 B 해제 때만 파일이 정리된다.

추가로 인증 snapshot 뒤 같은 업무 첨부가 교체된 상황의 release/replace 및 각 rollback, 미연결 snapshot 뒤 연결된 파일의 업로드 삭제 거부를 확인했다. 일반 조회 snapshot은 옛 상태를 유지함도 별도로 검사해 이 순서가 우연히 새 snapshot으로 실행된 것은 아닌지 확인한다. 기존 과거 제출 참조·현재 권한·정리 실패 재시도 검사를 유지했다.

이번 직접 결과: 공용 첨부 **239개 통과**(추가 회귀105 + 기존134). 기존 서버191·웹183·실제 C# HTTP/WinForms41, C#115소스 컴파일 오류0/경고0, 실제2·3번 취소·연장 MySQL/HTTP258, JS 명시 DOM/HTTP 대역52 및 기본off 검사 통과. 추가 회귀105를239에 다시 합산하지 않는다. lifecycle 검사 중 RuntimeError 로그는 기존 의도적 rollback fixture이며 최종 종료0이다.

사용자 DB·실행 중 제품 서버·실제 공유DB 적용·배포 ACK·두PC/장비는 변경/검증하지 않았다. 업무 소비 route는 명시 테스트 fixture이며 2번 실제 업무 UI/TX 연결 완료를 뜻하지 않는다. 이미 잘못 저장된 실제 데이터의 일괄 복구/삭제 작업도 하지 않았다. 1번 DDL 검토·공유 적용과2번 소비 후속은 위 계약대로 남는다. PR52의 R52-03은 **3번 수정·격리 검증 완료, 2번 재검토 대기**로 회신한다.


## 2026-10-22 — T3-1~5 제공부 구현

기준 alpha `5dcd66e291a0b12470c3347909653a64458ef70e`, LOG `75e6fe5002ad91547de919ab79acfd5a00bbf82b`의 2번 수용 회신·T3-5 전송 확인 요청을 따른다. 아래는 3번 제공부이다. 2번 업무 제품·공통 인증·DB·옵션·ACK·DDL은 수정하지 않았다. 취소/pending 차단, 기존 교체/해제 helper 및 정식013은 기존 기준을 유지한다. 이전 절의 ‘읽기/복귀/웹 어댑터 미제공’ 상태는 아래 제공으로 갱신한다.

### 현재 첨부 ID와 최소 감사

`current_app.extensions["results_shared_attachments"].read_task_attachment_ids(db, task_id, *, expected=ExpectedVersions(...)) -> tuple[str, ...]`

현재 인증 TX와 동일한 db만 허용한다. 현재 업무·배정·관리자 잠금, 현재 지정 관리자인지, 원래 두 버전, 취소 상태를 검사하고013 준비를 확인한 뒤 task 참조 ID를 현재 locking read로 읽어 정렬된 tuple을 반환한다. commit·버전 변경·감사 작성은 하지 않는다. 잘못된 TX는 RuntimeError, 식별자/expected 형식400, 권한403, 배정/버전/취소409, 스키마503이다. 권한과 버전 검사가 스키마 검사보다 앞선다.

2번은 **첫 mutation 안에서** 이 함수를 읽고, 검증된 요청의 최종 ID 집합과 비교한다. 기존 `replace_task_files(..., expected=원래 expected)`는 버전 증가 전에 호출한다. 입력 배열 원문은 정렬/중복 제거하지 않는다. 등록 생략/[]=없음, 수정 생략=유지/[]=전체 task 참조 해제/값=최종 전체 목록, 최대10개 정규 소문자UUID를 유지한다. 중복/null/타입 오류400, 미연결 만료 파일 연결403, 업로드 소유자 경로의 만료409를 유지한다. 실제 변경은 기존 task.updated·row_version 증가 각1번이며 assignment_revision/제출 회차는 유지한다. 실제 변화 없음409를 소비자가 판단한다.

감사 **제공/소비 제안 필드명**은 `changed_fields`의 `attachment_ids`, 정렬된 `before_attachment_ids`/`attachment_ids`이다. 기존 before/after·task.updated를 보존한다. 3번 reader는 이를 “변경 항목: 첨부 자료 / 첨부 자료: N개 → M개”로 표시한다. 파일명·본문·경로는 넣지 않는다. HistoryControl과 웹 이력은 기존 summary를 사용하므로 폼을 변경할 필요가 없다. **2번 실제 writer 연결/필드 수신 확인은 후속**이며 여기서 대신 활성화하지 않는다.

Task/배정/관리자/참조/감사/receipt는 한 인증 TX에서 전부 성공 또는 rollback한다. successful receipt 재생에는 파일 검사를 새로 추가하지 않는다. 실패/결과 불명은 원래 ID·본문·W1 키를 보존하며 자동 삭제/재업로드/새 키 저장은 없다. 기능OFF/013 미준비 때 2번 등록 생략/[] 허용·명시 등록503·명시 수정503의 기존 경계를 유지한다. 이 제공만으로 2번 TASK_ATTACHMENT_REPLACEMENT_NOT_READY를 해제하지 않는다.

### 읽기 전용 페이지와 명시 복귀

`GET /results?task_id=<정규 BIGINT 문자열>&view=read`는 현재 담당자·지정 관리자·허용 검토자를 실제 검사한다. 결과/제출 이력/다운로드를 읽으며 제출/재작업/검토/업로드 UI를 렌더링하지 않는다. 지정 관리자를 실제 검토자로 취급하지 않는다. data GET만 제공하던 상태와 달리 실제 read 페이지와 JS가 제공된다. 기존 own/review 동작은 보존한다.

2번은 결과물 기능ON일 때 등록되는 공개 dict `current_app.extensions.get("results_task_web")`를 사용한다. OFF일 때 dict가 없으므로 results_web/모델을 import하지 말고 링크/첨부 UI를 제공하지 않는다.

- `provider["session_scope"]() -> str`: 현재 cookie AuthSession의64hex scope. Bearer/token_hash 원문이 아니다. 현재 인증 요청에서만 사용한다.
- `provider["issue_back_context"](task_id: str, *, scope: "own"|"managed", status: None|5개 상태, include_cancelled: bool=False) -> str`
- `provider["validate_back_context"](token: str, *, task_id: str) -> dict(scope, status, include_cancelled)`
- `provider["build_url"](task_id: str, *, view: "own"|"read", scope, status=None, include_cancelled=False) -> str`: 검증된 내부 results URL. managed는 반드시 read이다.

발급/검증은 이미 열린 같은 인증 TX를 재사용하거나 없으면 현재 인증 TX를 연다. 발급은 scope의 현재 본인/지정 관리자 관계도 확인한다. 서버 서명은 원래 task·user·같은 AuthSession scope·원래 필터·공통 AuthSession expires_at을 묶는다. 별도 임의 TTL·전역 마지막 업무·외부 return_url을 만들지 않는다. 만료/변조/다른 업무/다른 접속은400 INVALID_BACK_CONTEXT, 만료/폐기 로그인 자체는 공통401이다. 다중 탭의 각 링크가 독립적이다. 문맥 없이 정상 진입할 때 필터나 복귀 목적지를 추측하지 않는다.

2번 예: `provider["build_url"](task["task_id"], view="read", scope="managed", status=status, include_cancelled=include_cancelled)`를 자기 템플릿 링크로 전달한다. own은 view="own"이다. 해당 부분은2번 파일에서 소비해야 한다.

문맥 있는 페이지의 “원래 업무로 돌아가 최신 정보 조회”는 `GET /results/tasks/<id>/back?back_context=...`를 명시 클릭한다. 현재 인증·문맥·현재 own/managed 상세 권한을 재검사하고 내부 `url_for("task_web.detail", task_id=..., scope=..., status=..., include_cancelled=...)`로302한다. 실제 상세 GET은 현재 데이터를 다시 읽는다. 복귀403/404는 이미 확인한 저장 성공과 분리해 안내한다. 저장 성공 표시는 후속 결과물 조회403에도 보존한다. 불명 저장 중에는 복귀를 막고 기존 처리 결과 확인을 먼저 하게 한다. 자동 복귀/재전송은 없다.

### 업무 웹에서 소비할 첨부 어댑터

아래 GET/POST는 기존 cookie 인증, 공통 응답 `{ok,data,request_id}`, 현재 `X-Results-Session` 헤더를 사용한다. 각 POST는 기존 `FormData`에 `csrf_token`과 **JSON 문자열** `body` 두 필드만 보낸다. 새 CSRF 헤더나 Bearer를 만들지 않는다. 중복 폼/JSON 필드·지원하지 않는 query400, CSRF 실패400 CSRF_FAILED, 구접속409 RESULTS_SESSION_CHANGED, 로그인401, 권한403, 제공부/스키마503을 그대로 전달한다.

| 메서드/경로 | body 또는 반환 data |
|---|---|
| GET `/results/data/shared/tasks/<task_id>` | `{items:[{attachment_id,name,size,sha256}]}` |
| POST `/results/data/shared/uploads/start` | `{name,size,sha256}` + 기존 업무면 `task_id` 문자열. 생성 전은 task_id 생략·관리자만. 반환 파일 메타4항목·201 |
| POST `/results/data/shared/uploads/<id>/chunks` | `{offset:정수,data:base64}`.16KiB 단위. 반환 `{attachment_id,next_offset}` |
| POST `/results/data/shared/uploads/<id>/complete` | `{}`. 전체 size/hash 검사, 반환 파일 메타4항목 |
| GET `/results/data/shared/tasks/<task_id>/files/<id>/download` | 전체 binary, Content-Disposition 파일명. 서버 size/hash 및 IO 뒤 현재 권한/해당 task 참조 재검사 |
| GET 같은 `/chunks?offset=0` | `{attachment_id,offset,next_offset,eof,data}`. 최대16KiB. 전체 조립 후 size/hash는 소비자도 검증 |
| POST `/results/data/shared/uploads/<id>/discard` | `{}`. 본인 미연결 임시만 삭제 예약. 반환 `{attachment_id}` |

현재 task 참조는 IO 전후 검사하므로 A가 해제되고 B만 남아도 A URL로는 접근하지 못한다. B/과거 제출은 보존한다. 업로드 조각·완료는 기존 현재 권한·취소·연장 조건의 IO 전후/저장 전 검사를 재사용한다.25MiB/10개/임시24시간·마지막 참조 정리·실패 재시도 기준은 동일하다. 삭제 요청은 사용자 확정 취소일 때만 명시 호출하고 **불명 업무 저장에 discard하지 않는다**. chunk/complete 실패 때 원래 upload ID를 유지하고 전체 검증 전에는 파일을 완성본으로 사용하지 않는다.

실제2번 업무 웹의 파일 선택 UI/등록·수정 writer 연결과 최종 공유DB/파일권한/같은 배포ACK는 후속이다. 이번에1·2·4·5번 제품 코드를 대신 수정하거나013을 재작성/적용하지 않았다. 자동 결과 제출과 별도 미확정 검토 요청 정책을 추가하지 않는다.


### 최신 LOG의 C# 전송 확인 — 2026-10-22

`ReviewsControl.cs` 안의 기존 `ResultsFileTransport`에 정상16KiB `FB EF BE` 반복 파일을 전달해 공통 `ApiClient.Json`의 `+`→`\u002B` 확장으로32KiB 제한을 초과하는 실패를 실제 소유 MySQL/HTTP에서 재현했다. 같은 파일을 `verify_client.cs`의 실제 업로드 회귀에 추가했다. 기존 폼 배치·상태/권한·공통 Json 설정·청크16KiB·서버32KiB 한도를 바꾸지 않고, 전송 DTO의 **내부 생성 base64 값만** pre-encode하는 전용 converter를 추가했다. 파일명/설명 등 다른 문자열에는 기존 인코더를 그대로 사용한다. 공통 ApiClient 및2번 전송부를 수정하지 않는다.

T3-5 검증은 기존 일반 바이트 전송과 이 반복 바이트 전송, 완료 metadata의 size/hash를 함께 확인한다. 이 변경은 새 폼이나 별도 API 파일을 추가하는 것이 아니다. C# 전송의 시작 결과 불명/명시 폐기 UI를 새로 구현하거나 자동 재전송/삭제로 바꾸지도 않는다.

최신 LOG에서1번의 raw006/009/013 실제 DB 적용 **보고를 수신**했다. 동일 적용을 다시 미제공으로 요청하지 않는다. 그 보고와3번의 이번 직접 실행을 구분하며 공유 파일 저장소/권한·검토자 관계·동일 배포/통합ACK·실계정/두PC는 별도 후속이다. PR64 captured DELETE도 병합 제공 상태로 유지한다.
