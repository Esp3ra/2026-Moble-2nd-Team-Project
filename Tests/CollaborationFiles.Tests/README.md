# 공지·채팅 첨부 실제 소비 검증

1·3번 제공 접점을 4번 제품에 연결한 상태를 검사한다. SQLite·공유 DB·사용자 환경파일을 읽지 않으며 native MySQL 소유 helper가 독립 DB/프로세스를 만들고 종료·삭제한다. 공통 테이블만 명시 ORM으로 만들고 002/003/004/005/006/013 및3번 `docs/sql/notice_chat_attachments.mysql.sql` 원문을 적용한다. 새 파일 DDL 번호나 공유 적용을 선언하지 않는다.

먼저 `dotnet build Tests/CollaborationFiles.Tests -c Release` 후 Python `run_check.py`를 실행한다. `--node`, `--node-modules`, `--browser`, `--report-dir`에 절대 경로를 지정하면 실제 Edge/Playwright cookie2계정 UI 검사도 한다. 실제 C# `ApiClient`/공지·채팅 서비스와 전송 클래스, TCP HTTP/MySQL, 실제 웹 폼·모듈을 사용한다. C# 및 브라우저 검사는 같은 DB의 HTTP 검사와 겹치므로 건수를 합산하지 않는다.

- 같은 TX 자원/파일/감사 rollback, UUID 재시도 단일효과와 변경본문409, 현재 부서/방/작성자 권한, 정확한 자원 경로, IO 후 숨김 재검사, 본인 숨김 후 상대 다운로드.
- C# 16KiB/여러 조각/Base64+ 경계, 빈 파일, 한글명, 전체 크기/SHA, 잘못된 hash 차단, 다른 계정 및 재로그인·원 로그인 종료.
- 브라우저 실제 폼 업로드·공지 등록·다운로드·재조회, C# 생성 메시지 파일 열람, 웹 채팅 업로드/상대 자동수신·다운로드, 커밋 후 응답 유실 조회복구/POST1회, 데스크톱/390px 화면, pageerror0. 해시는 Node crypto와 13개 벡터로 대조한다.

`run_notice_regression.py`는 기존 API/웹 공지 검사를 별도 소유 MySQL에서 실행하고 SQLite를 하드코딩한 cold 검사8개를 제외한다. 기존 웹채팅 native runner, 공지 WinForms·채팅 source/session·자동 수신 검사도 별도 실행한다. 실제 공용 서버 파일 업로드, 두 PC 네트워크 전송, 사용자가 선택하는 Windows 파일 대화상자/대형25MiB 브라우저 UI·장시간/다중 서버 부하, 미합의 공지 원결과 W1·삭제정책은 완료로 표시하지 않는다.
