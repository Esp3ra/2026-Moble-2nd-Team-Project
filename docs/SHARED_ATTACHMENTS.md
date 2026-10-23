# 3번 공용 첨부 제공 계약 — 로컬 구현, 소비/DB 반영 전 검토

## 2026-10-23 D3 사용자 선택 / 3번 보완 작업 경계

기준 alpha `ea45d437f05d5af41f309b656ad9a533b7c3f9d3`, LOG
`cd7ad2123ff4a9b6fbdc6d1eede12d909cb0d693`. 4번 PR76 댓글
`member4-d3-owner3-request-20261023`에 대한 본 채팅 사용자 선택은 다음과 같다.
공지 A의 연결만 해제하고 B가 같은 파일을 쓰면 B 다운로드를 유지한다.
마지막 참조가 없어지면 별도 보관기간/보존 열람 기능 없이 실제 파일을 정리한다.
다른 참조/현재 권한 검사를 무시하는 강제 삭제는 하지 않는다.
공지·참조·감사 DB 처리 실패는 rollback한다. commit 후 파일 정리만 실패하면
기존 deleting 상태에서 재시도하며, 이를 공지 DB 삭제 실패나 완료된 바이트 삭제로 표시하지 않는다.

3번은 기존 release/cleanup 동작을 재구현하지 않고, 같은 인증TX의 현재 첨부 ID를
읽는 공개 함수와 실패/경합 회귀만 보완한다. 4번의 notice_resource release409와
block_notice_file_delete·공지 삭제 서비스/화면은 변경하지 않는다.
이 기록은 사용자 선택을 담은 로컬 제공/검증 계약이며 팀장 수신·4번 연결·공유 배포
완료를 뜻하지 않는다. 원UUID/W1 공지 저장 복구는1·4번 별도 후속이다.

### D3 현재 제공 접점과 소비 순서

`app.extensions["results_resource_attachments"]`의 다음 함수를 사용한다.

- **추가:** `read_resource_attachment_ids(db, kind, resource_id) -> tuple[str, ...]`.
  같은 인증TX에서 현재 자원의 정렬된 첨부 UUID만 locking read로 반환한다.
  현재 `read` 권한을 검사하며 파일명·다른 자원 ID/권한은 반환하지 않는다.
  별도 commit·파일 IO·변경·감사는 없다. HTTP 목록과 다른 동일TX용 공개 접점이다.
  잘못된 kind/ID400, 현재 권한403, 자원 없음404, 제공부/스키마 미준비503,
  호출자의 인증TX/db가 아니면 RuntimeError다.
- **기존 유지:** `release_resource_files(db, "notice", notice_id) -> None`.
  현재 `release` 권한 콜백을 거쳐 A 참조만 해제하고 현재 전체 참조를 확인한다.
  B 참조가 있으면 ready/원 바이트를 유지하며, 마지막 참조면 같은TX에서 deleting 표시한다.
  standalone 삭제 HTTP API나 새로운 성공/보존 DTO는 만들지 않는다.
- **기존 유지:** `current_app.extensions["results_shared_attachments"].cleanup_shared_files() -> int`.
  원TX commit 후 요청 종료 단계에서 실행하며, 실패분은 기존
  `shared-attachments cleanup` CLI/위 공개 함수로 운영자가 재시도할 수 있다.
  실행에는 기존 앱 설정·app context가 필요하며4번이 저장소 경로/ORM을 직접 복제하지 않는다.
  성공한 물리 정리 수를 반환한다. 반환0은 대기 파일이 없다는 증명이 아니다.

4번이 연결할 순서는 다음과 같다. 실제 적용은 기존 release409/삭제 차단과 함께
4번이 변경해야 하며,3번이 해당 파일을 대신 수정하지 않는다.

1. 기존 관리자 인증TX/현재 공지 잠금 안에서 공개 reader로 현재 ID를 읽는다.
2. 같은 db로 공개 release를 호출하고 공지 삭제·감사를 동일TX에 묶는다.
3. DB/참조/감사 처리나 commit 실패는 모두 rollback한다. 실제 파일은 commit 전에 지우지 않는다.
4. commit 뒤 정리를 시도한다. 파일만 잠겨 실패하면 삭제대기로 남아 접근은 차단되고,
   재시도해서 정리한다. 이미 commit된 공지를 자동 복구하거나 바이트 삭제 완료로 표시하지 않는다.

**검증:** 최신 alpha 위 정식015를 별도 소유 MySQL에 적용해 실제 loopback HTTP로
기존108 + D3 추가80 = 총188검사 통과. 추가 검사는 동일TX 외 호출 거절·현재 ID/
권한/입력, A/B 참조 보존, 공지·참조·감사 rollback, 마지막 참조의 commit 실패,
DB commit 후 파일 잠금/삭제 재시도, 실제 인증 RR snapshot 뒤 B 연결 commit과
A 삭제 경합을 포함한다. 권한/공지 삭제 소비는 명시 시험 대역으로서4번 실제 서비스가 아니다.
초기 실행은 시험기의 SQL 주석 안 세미콜론 분리로 실패했고, 주석을 먼저 제거하는
시험기만 수정한 후 정식015 원문 그대로 재실행했다. SQL/DDL 제품 오류로 보고하지 않는다.

정식013/015·공통 인증/옵션/DB·4번 코드·4폼은 변경하지 않았다. 실제 공유 서버/DB,
실계정·두PC·브라우저/WinForms 공지 삭제·C# 빌드는 이번 미실행이다.
새 보관기간/보존 화면/팀장 결정·공유 적용을 완료로 기록하지 않는다.

## 2026-10-22 공지·채팅 제공부 작업 경계 — 구현 전 기록

기준 alpha `7692ce41cf36e89e6f5881b61efcc7fcb05fe5e5`, LOG
`d98f6473c2942d5e480e5018b604c11e9e6a278f`, 4번의 PR54 기존 통합 댓글
`member4-notice-chat-file-ports-20261022` 요청을 따른다.
3번 소유 파일 전송·검증·참조 함수와 cookie 어댑터만 확장한다.
1·2·4·5번 제품, 기존 업무/제출 참조와 정식013, 폼/화면은 변경하지 않는다.
4번이 현재 공지/채팅 권한 콜백과 저장 TX에서 소비하고 1번이 별도 참조 DDL을
검토해야 실제 화면에서 사용할 수 있다. 새 삭제/보존 정책, 등록 필드, 자동 재전송은
확정하지 않는다. 아래 추가 제공 계약은 4번 소비 계약의 제안이며 수신 합의가 아니다.
실제 DB 적용·서버 재시작·원격 게시를 하지 않는다.

**코드 PR 게시 전 최신 결합 검증:** 사용자 후속 요청으로 alpha
`652b21a20b08fd7c4e3d77332a7d219366dac779`와 LOG
`1ed5396d1403a8db70b0ddf2517ac83482c94984`를 다시 확인했다.
구현 `d56817d48354df58929f029cbf7c610dedbf4c33`에 최신 alpha를 일반 merge한
`31aaced8f05c7562cf1f4525cba469a1de8309e5`에서 아래 새108·공용251·연결150·웹183/서버191과
기본off 검사를 직접 재실행해 모두 통과했다. 이전 실행 수치와 합산하지 않는다.
PR diff는 기존과 같은6파일이며 다른 담당 변경은 최신 alpha 원본 그대로 보존한다.
아래 실제4번 소비/DDL 적용/정책·LAN 제약과 미실행 범위는 그대로다.
이 절은 같은 코드 PR의 검증 기록이며 별도 회신 문서 PR이 아니다.

### 공지·채팅 제공 코드와 상태

- **로컬 제공 완료:** `features/shared_resource_attachments.py`의 전송/조회/참조 함수,
  `results_web.py`의 cookie 어댑터, `shared_attachments.py`의 전체 참조 보존/정리 연결.
  모든 경로는 `RemoteWork.Server/remotework/` 아래이다. 기존 4개 폼은 변경하지 않는다.
- **기존 기준 유지:** 25MiB/파일, 최종 목록10개, 16KiB/base64 조각, SHA256,
  미연결24시간, 현재 로그인·자원 권한 재검사, A 해제 후 B 보존. 013/업무·제출 참조는 그대로다.
- **소비 계약 제안:** 아래 콜백/함수/경로를 4번이 기존 저장·화면에 연결하는 방식.
  공지/채팅의 기존 등록 HTTP 입력에 `attachment_ids`가 이미 있다고 주장하지 않는다.
  필드·요청 identity·기존 성공 재생 처리의 실제 소비는 4번 변경/합의가 필요하다.
- **DDL 검토안:** `docs/sql/notice_chat_attachments.mysql.sql`. 번호를 선점하지 않았다.
  기존003/005/013을 요구하는 별도3테이블이며 기존 테이블 ALTER/초기화/seed/CASCADE가 없다.
  `ResourceBase`는 공통 Base와 분리했다. FK 대상 메타정보는 DDL 참조용 최소 정의이며
  4번 모델을 자동 등록하거나 4번 DB를 직접 읽는 권한 구현이 아니다.
- **협의/미제공:** 공지 물리 삭제 때 파일 연결 해제/보존을 실행할 조건, 채팅의
  향후 물리 삭제/탈퇴 보존 정책, 4번 실제 UI/저장 소비와 공지 저장 요청의 결과 불명 복구.
  새 정책이나 소비 코드를 대신 구현하지 않았다. 채팅 본인 숨김은 참조 해제가 아니다.

### 4번 권한 콜백과 동일 TX

앱 시작 시 최초 요청 전에 다음 제공부를 사용한다. 권한 콜백이 없으면 해당 자원의
경로는503 `RESOURCE_ATTACHMENT_ADAPTER_REQUIRED`이며 admin을 기본 허용하지 않는다.

```python
provider = app.extensions["results_resource_attachments"]
provider.register_adapter(app, "notice",
    authorize_upload=notice_upload_authority,
    authorize_resource=notice_resource_authority)
provider.register_adapter(app, "chat",
    authorize_upload=chat_upload_authority,
    authorize_resource=chat_resource_authority)
```

- `authorize_upload(db, context_id) -> True`: 공지는 context_id=None에서 현재 관리자 작성
  권한, 채팅은 정규 방UUID에서 현재 참여·개인 숨김·계정 상태를 4번이 확인한다.
  정확히 True가 아니면403이다. 업로드 시작/조각/완료/상태조회/임시삭제 때 검사하며,
  파일 IO가 있는 조각/완료는 기존 공통 전송 코드가 IO 전후 각각 검사한다.
- `authorize_resource(db, resource_id, operation) -> ResourceAccess(context_id)`:
  operation은 `read`/`attach`/`release`이다. 공지는 양수 signed INTEGER(최대2147483647),
  채팅은 공개 메시지UUID이며 seq나 업무ID로 대체하지 않는다. 공지에서는
  ResourceAccess(None), 채팅에서는 실제 메시지가 속한 방UUID를 반환한다.
  None/임의 bool 반환은 권한 허용이 아니다. 현재 대상/참여·숨김·회수와
  자원 존재 및 해당 쓰기 권한은 4번이 검사하고403/404 등 기존 오류를 낸다.
- 콜백은 호출자의 `authenticated_transaction()`과 같은 db에서 현재 locking read 및
  `populate_existing=True`를 사용한다. 공통 순서 guard → 현재 actor/session →
  자원/참여자(여러 사용자면 ID 정렬) → 파일/참조를 유지한다. commit/rollback,
  중첩 TX, 네트워크/파일 IO를 콜백에 추가하지 않는다. 현재 공통 guard 아래만
  호출하므로 별도 전역 잠금이나 격리 수준 변경은 없다.
- 현재 읽기 권한은 목록 조회와 다운로드 IO 전후 모두 재검사한다.
  A 경로는 A의 현재 참조를 요구한다. B 참조가 남았다고 A 경로를 허용하지 않는다.
  임시 파일은 소유자만 처리한다. 업로드 소유권을 공개 다운로드 권한으로 사용하지 않는다.

### 4번 저장 TX에서 사용할 함수

```python
# 4번의 기존 인증 TX 안에서 원 공지/메시지 및 감사와 함께 호출한다.
# 공지/메시지 행은 먼저 flush하되 commit은 하지 않는다.
files = provider.replace_resource_files(db, "notice", notice.id, final_attachment_ids)
files = provider.replace_resource_files(db, "chat", message.message_id, final_attachment_ids)
# 아래는 물리 삭제/연결 해제 정책 합의 후, 원 자원 삭제 전의 동일 TX에서만 호출.
provider.release_resource_files(db, "notice", notice.id)
```

`replace_resource_files`의 파라미터는 최종 전체 파일ID 배열이다. 배열 원문은
호출자가 보존하고 중복 제거/정렬해서 원 요청을 바꾸지 않는다. 중복/형식/10개 초과400,
권한/다른 자원 종류·채팅방 범위403, 미완료409, 스키마 미준비503이다.
처음 연결은 본인 유효 임시 파일만 가능하다. 이미 연결된 파일을 재사용할 때는
원 자원의 현재 열람 권한도 필요하며 공지↔채팅/다른 채팅방 공유를 새 정책으로
허용하지 않는다. 새 작업의 참조/원 자원/감사가 같은 TX에서 전부 저장되거나 rollback된다.
이 함수는 등록/수정 필드를 확정하거나 자원 버전/감사/원UUID/receipt를 별도로 저장하지 않는다.

현재 공지 `create_notice_record`에는 영속 요청UUID/W1 결과 조회가 없다.
따라서 공지 저장의 응답 유실을 이 제공부만으로 복구 완료했다고 표시하지 않는다.
4번은 저장 요청 identity/조회 계약을 1번과 연결해야 한다. 채팅의 기존 메시지UUID
성공 재생에서는 원 파일 ID 집합도 원문과 대조하되 이 참조 변경 함수를 재실행하지 않는다.
구체 감사 필드/요청 필드명은 4번 소비 검토 대상이다.

결과 불명 시 원 파일ID·본문·키/메시지UUID를 보존한다. 새 키/새 메시지/새 업로드로
대체하거나 임시 파일을 자동 삭제하지 않는다. `uploads/...` 상태 GET은 전송 상태일 뿐
공지/메시지 저장 성공 증명이 아니다. 연결 후 임시 업로드 조회·삭제는403이며,
원 자원의 결과 조회로 성공 여부를 확인한다. 실패 확정 뒤에만 사용자가 명시적으로
임시 삭제할 수 있다. 기존 미참조24시간 정리는 유지한다.

`release_resource_files`는 호출자의 명시 권한·정책 확인 뒤 참조만 해제한다.
다른 자원 참조가 있으면 바이트를 보존하고 마지막 참조만 없을 때 기존 deleting/재시도
정리를 사용한다. rollback이면 원 참조/바이트를 유지한다. FK에는 CASCADE가 없어
참조가 남은 공지/메시지의 물리 삭제는 DB가 막는다. 공지 확인 기록의 CASCADE 합의를
파일 삭제 정책으로 확대하지 않는다. 채팅 숨김에서는 이 함수를 호출하지 않는다.

### API / cookie 웹 어댑터

API 기본 경로 `P=/api/v1/results/shared/resources`, 웹 `W=/results/data/shared/resources`.
`kind`는 `notice`/`chat`만 허용한다. API는 기존 Bearer 인증만, 웹은 기존 cookie/CSRF와
`X-Results-Session`의 원 웹 로그인 결합만 사용한다. 브라우저로 Bearer/다운로드키를 전달하지 않는다.

| 제공 경로 | 입력/출력 |
|---|---|
| `POST P/{kind}/uploads/start` | `{name,size,sha256,context_id}` → 기존 `{attachment_id,name,size,sha256}`,201 |
| `POST P/{kind}/uploads/{id}/chunks` | `{offset,data}`; 기존16KiB/base64·같은 바이트 재시도/충돌409 |
| `POST P/{kind}/uploads/{id}/complete` | `{}`; 실제 전체 크기/SHA256 검사·동일 ID 명시 재시도 |
| `GET P/{kind}/uploads/{id}` | 현재 소유자·권한 검사; 메타정보+`state,received_offsets`; 완료 처리를 하지 않음 |
| `DELETE P/{kind}/uploads/{id}` | 현재 소유자의 미연결 임시 파일만 해제 |
| `GET P/{kind}/{resource_id}/files` | 현재 자원 열람 권한의 `{items:[기존 파일 메타정보]}` |
| `GET P/{kind}/{resource_id}/files/{id}/download` | 현재 해당 자원 참조 확인·파일 크기/SHA256·IO 후 현재 권한 재검사 |
| `GET P/{kind}/{resource_id}/files/{id}/chunks?offset=0` | 기존 조각 다운로드 응답·IO 후 현재 권한 재검사 |

웹에서는 P를 W로 바꾸고, 쓰기는 기존 form의 `csrf_token`과 JSON 문자열 `body`를
전달한다. 임시 삭제만 `POST W/{kind}/uploads/{id}/discard`에 body=`{}`를 쓴다.
조회도 X-Results-Session을 요구한다. 이 어댑터는 공지/채팅 페이지를 새로 만들지 않는다.

**LAN 제약:** 서버/데스크톱의 SHA256 검사는 유지한다. 브라우저의 기존
`crypto.subtle`은 HTTPS 또는 localhost 보안 컨텍스트가 필요하므로 사설IP의 평문HTTP에서
동일하게 사용 가능하다고 표시하지 않는다. 이 제공부는 대체 JS 해시 구현/인증서/프록시를
임의 설치하지 않는다. 4번 웹 소비에서 실제 접속 환경을 확인하고 지원 방식은1·3·4번이
대조해야 한다. 기존 검사 비활성화나 업로드 완료 POST의 자동 재송신은 없다.

### 이번 직접 검증 / 미실행

`Tests/Results.Http.Tests/verify_resource_attachments.py`: 별도 소유 MySQL에 위 검토 SQL을
적용하고 실제 loopback HTTP로108검사 통과. 권한/소비 콜백·공지/메시지 TX는 명시한
시험 대역이며 4번 제품 소비가 아니다. 실제 참조 rollback, 같은 최종 목록 중복 효과,
A 해제/B 보존, 개인 숨김/비참여 관리자 차단, IO 중 대상 변경, 임시 만료 정리,
cookie/CSRF·접속 변경·API/web 분리·조각 충돌을 직접 확인했다.

기존 회귀: 공용251(내부R52-03 105 포함, 재합산 안 함), 연결150,
웹183·서버191 통과. 웹 회귀의 C# 실행은 `--skip-csharp`로 제외했다.
기본off/공유만on 새 프로세스에서 DB 연결 없이 신규 자원 테이블/4번 모델의
공통 Base 자동 등록이 없음을 확인했다. 새3테이블의 전체 FK/운영 migration 승인은
1번 검토 대상이며 실제공유 DB/서버/두PC/브라우저/LAN 파일 전송은 미실행이다.

**후속:** 4번은 실제 현재 권한 콜백·동일 저장/원요청 복구/화면 소비 및 삭제 정책 차이를
기존 통합 댓글에 회신하고, 1번은 참조 DDL/잠금/운영 적용·복구를 검토한다.
3번은 그 결합본으로 실제 계정·공유 환경 검증을 진행한다. 로컬 제공부 통과를
공유 배포나 공지·채팅 최종 완료로 표시하지 않는다.

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
