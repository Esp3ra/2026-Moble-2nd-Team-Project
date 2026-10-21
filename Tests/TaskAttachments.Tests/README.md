# 2번 업무 첨부 읽기 소비 검사

`dotnet run --project Tests/TaskAttachments.Tests -c Release`

공통 `ApiClient.Session`과 명시 HTTP 대역·실제 WinForms 상세 grid를 사용한다. 3번의 기존 `results/shared/tasks/{id}`·`files/{id}/chunks`를 소비하며 업로드·제출·DB를 대신 구현하지 않는다.

검사 범위: 정확한 UUID/크기/해시/개수·중복/추가 필드, 조각 순서·종료/크기/해시 변조, 다운로드 전후 및 저장 위치 선택 뒤 현재 업무 첨부 연결 확인, 재로그인 뒤 늦은 응답·401, 전송 중 로그아웃, 업무 행 변경의 늦은 첨부 응답, 검증 완료 파일만 저장·기존 파일 보존·실패 시 임시 파일 정리. 임시 저장 검사는 OS 임시 폴더에 전용 이름을 만들고 자신이 만든 파일만 삭제한다.

2026-10-21 직접 실행: 이 프로젝트 **247 checks 통과**, 기존 `TaskQueries.Tests` **172 assertions / 13 scenarios 통과**. Release 빌드 경고0·오류0. 두 프로젝트는 명시 테스트 대역을 사용하며 위 수치를 실서버/공용 DB 검증 결과로 합산하지 않는다.

실제 서버·공유 DB·두 PC·SaveFileDialog 수동 조작 검증과 구분한다. 공용 첨부 서버가 off이거나 013이 미적용인 경우를 빈 첨부 성공으로 대체하지 않는다. 현재 취소 업무의 등록 첨부는 조회하지 않고, 과거 제출 첨부는 3번 화면에서 처리한다. 세션 DELETE와 웹 cookie/CSRF 업로드 제공부는 해당 담당의 연결 사항이며 이번 읽기 소비가 업로드 UI 완료를 뜻하지 않는다.


<a id="actual-attachment-http-20261021"></a>
## 별도 실제 HTTP 소비 확인 — 2026-10-21

위247개는 명시HTTP대역/실제grid 검사입니다. 추가로 저장소 밖 임시 C# harness에서 실제 `ApiClient`·`HttpTaskAttachmentService`를 기존3번 공용 첨부 provider와 새 소유MySQL/raw013에 연결해 **10개 검사**를 통과했습니다. 실제목록·2조각 다운로드·SHA256·새파일저장·기존파일보존, 선택업무 링크회수 후목록0/다운로드409/보관된다운로드의저장409, 다른업무참조와파일보존을 확인했습니다.

근거는 `ES/tmp/remote_work_member2_remainder_20261021/attachment-live-report.json`입니다. 공통schema `[1]`,HTTP thread종료·MySQL/소유runtime정리를 확인했습니다. fixture는합성파일/참조만준비했고 제품·기존검사소스·공유DB·운영ACK를수정하지않았습니다. 업로드·등록/수정첨부본문·SaveFileDialog수동조작·두PC의완료근거는아닙니다.
