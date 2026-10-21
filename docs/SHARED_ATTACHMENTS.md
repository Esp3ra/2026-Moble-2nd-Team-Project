# 3번 공용 첨부 제공 계약 — 로컬 구현, 소비/DB 반영 전 검토

사용자 확정 정책: 업무 등록 첨부와 제출 회차 첨부를 지원한다. 현재 담당자·업무 관리자·검토자만 다운로드하며 관계 변경 후 재검사한다. 업무 등록/교체/취소와 파일 연결/해제는 같은 DB 트랜잭션으로 처리한다. A의 연결 제거가 B를 지우지 않으며 마지막 연결이 없을 때만 파일을 정리한다. 업무 취소 시 업무 첨부만 해제하고 이전 제출 파일은 보존한다. 기존25MiB·10개·임시24시간 기준을 유지한다.

## 변경 경계와 협업 요청

- 3번 소유 `shared_attachments.py` 1개와 기존 `results.py` 연결 부분을 구현한다. 기존4폼, 공통 인증/통신/registry, 2번 제품 코드는 직접 변경하지 않는다.
- 2번은 업무 등록/수정/취소 TX 내부에서 아래 함수를 호출해야 한다. 3번 함수 자체는 commit/rollback을 하지 않는다. 업무 버전 증가·공통 receipt·업무 이력은 기존2번 처리와 함께 유지한다.
- 1번은 `migrations/drafts/shared_attachments.mysql.sql`의 추가3테이블/FK/인덱스를 검토해야 한다. 006과 공통 schema1은 변경하지 않는다. SQL 번호·공유 DB 적용·배포 ACK를 문서만으로 승인하지 않는다.
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
