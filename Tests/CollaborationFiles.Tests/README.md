# 공지·채팅 첨부 실제 소비 검증

1·3번 제공 접점을 4번 제품에 연결한 상태를 검사한다. SQLite·공유 DB·사용자 환경파일을 읽지 않으며 native MySQL 소유 helper가 독립 DB/프로세스를 만들고 종료·삭제한다. 공통 테이블만 명시 ORM으로 만들고 002/003/004/005/006/013 및3번 `docs/sql/notice_chat_attachments.mysql.sql` 원문을 적용한다. 새 파일 DDL 번호나 공유 적용을 선언하지 않는다.

먼저 `dotnet build Tests/CollaborationFiles.Tests -c Release` 후 Python `run_check.py`를 실행한다. `--node`, `--node-modules`, `--browser`, `--report-dir`에 절대 경로를 지정하면 실제 Edge/Playwright cookie2계정 UI 검사도 한다. 실제 C# `ApiClient`/공지·채팅 서비스와 전송 클래스, TCP HTTP/MySQL, 실제 웹 폼·모듈을 사용한다. C# 및 브라우저 검사는 같은 DB의 HTTP 검사와 겹치므로 건수를 합산하지 않는다.

- 같은 TX 자원/파일/감사 rollback, UUID 재시도 단일효과와 변경본문409, 현재 부서/방/작성자 권한, 정확한 자원 경로, IO 후 숨김 재검사, 본인 숨김 후 상대 다운로드.
- C# 16KiB/여러 조각/Base64+ 경계, 빈 파일, 한글명, 전체 크기/SHA, 잘못된 hash 차단, 다른 계정 및 재로그인·원 로그인 종료.
- 브라우저 실제 폼 업로드·공지 등록·다운로드·재조회, C# 생성 메시지 파일 열람, 웹 채팅 업로드/상대 자동수신·다운로드, 커밋 후 응답 유실 조회복구/POST1회, 데스크톱/390px 화면, pageerror0. 해시는 Node crypto와 13개 벡터로 대조한다.

`run_notice_regression.py`는 기존 API/웹 공지 검사를 별도 소유 MySQL에서 실행하고 SQLite를 하드코딩한 cold 검사8개를 제외한다. 기존 웹채팅 native runner, 공지 WinForms·채팅 source/session·자동 수신 검사도 별도 실행한다. 실제 공용 서버 파일 업로드, 두 PC 네트워크 전송, 사용자가 선택하는 Windows 파일 대화상자/대형25MiB 브라우저 UI·장시간/다중 서버 부하, 미합의 공지 원결과 W1·삭제정책은 완료로 표시하지 않는다.


## R78-01 추가 검사

원 실행기는 기본 C#11에 이어 `--partial` 모드를 실행한다. 실제 WinForms의 파일선택 후 제품 메서드→원 ResourceAttachmentClient/HttpChatService→실제 HTTP/MySQL을 사용하며 B chunk 성공 응답과 메시지 성공 응답만 명시 주입으로 유실시킨다.16검사는 A완료/B실패/C미시도·B만재시도후차단·직접 send 차단·C완료/ABC정확3개 연결·명시C제외/AB·불명원메시지 선택잠금·같은UUID/body재시도/참조2개를 검증한다. 파일 대화상자를 조작했다고 표시하지 않는다.

Edge 원 page/handler에도 같은 ABC chunk응답 유실을 주입한다. B만 재선택해도 C는 대기/전송차단이며, C완료 후 원3개ID와 실제 상대 파일목록3개를 검사한다. 명시 제외도 별도 검사한다. 기존9에3시나리오를 더한12이며 C#/HTTP와 같은DB범위를 합산하지 않는다.

## D3 4번 실제 삭제 소비 검사 — 2026-10-23

PR83 공개 reader에 의존한다. 실행기는 raw 정식015를 포함한 소유MySQL만 사용한다.
notice_delete_checks.py의24 assertion은 원4번 실제API/권한/공지TX와3번 공개접점을 연결해 감사/commit 실패 rollback, 제공부OFF·누락 차단,
A제거/B보존, 마지막참조 물리정리, commit후파일잠금의deleting/접근차단/명시cleanup재시도를 확인한다.
기존HTTP요청 검사55와 중복되므로 합산하지 않는다. C#전송11·R78부분복구16도 같은DB검사다.
--notice-delete의 실제 WinForms/HttpNoticeService→HTTP/MySQL7검사는 커밋응답유실 뒤 원행/ID보존·재POST0·목록GET복구·공지/첨부404를 확인한다.
Native 파일대화상자 조작이 아니다. noticeUI45는 명시대역으로 잘못된200응답·실패GET·403도 구분한다.
Edge14/pageErrors0은 기존12에 실제cookie/CSRF첨부공지삭제·수신자의삭제된공지/첨부목록부재2개를 포함한다.
기존notice서버55PASS/cold8제외·3번 원제공기188PASS도 별도 실행했다. 3번 검사말미의blocked 문구는 그 실행기의 시험consumer 설명으로,
이4번서비스연결상태를 판정한 것이 아니다. SQLite미사용/소유자원정리완료.
초기 새 C#검사의 Length 대신Count 사용 컴파일오류, 실제컨트롤초기GET busy완료를 기다리지 않은 검사실패,
삭제뒤웹index를404로 가정한 실패는 보존했다. 테스트만 교정했고 웹index의200+부재안내 계약이나 서버인가를 완화하지 않았다.
공유서버/실계정 삭제·배포·정책승인·동시두PC·대형UI/장시간부하는 미실행이다. 신규DDL/설정·타담당제품수정없음.
