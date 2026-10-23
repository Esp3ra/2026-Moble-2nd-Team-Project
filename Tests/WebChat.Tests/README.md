# 웹 채팅 검증

Windows/MySQL8.2 `C:/Program Files/MySQL/MySQL Server 8.2/bin`과 서버 Python
의존성이 필요하다. 실행기는 원본 2번의 소유 runtime/Windows Job/MySQL 인스턴스
검증·정리를 재사용한다. 기존 `.env`/DATABASE_URL/서비스/운영 DB를 사용하지 않으며
SQLite fallback이 없다. 공개 합성 계정과 일회성 데이터만 사용한다.

```powershell
& .\RemoteWork.Server\.venv\Scripts\python.exe Tests/WebChat.Tests/run_mysql_check.py
```

웹 cookie/CSRF·페이지 접속 결합·동일 계정 재로그인·활성 사용자 최소 정보·개인/그룹,
웹↔기존 API 양방향·UUID 재시도·Unicode2,000/2,001·두 커서·비참여자/비활성 차단,
개인 숨김/다른 앱 재생성을 확인한다. 기존 저장·참여자 검사도 함께 실행한다.
기존 SQLite를 사용하는 cold등록/CLI 10개는 수집 후 제외하며 실행하지 않는다.
최초 실행 결과 **44 PASS, 10 deselected**. 다른 검사 수치와 합산하지 않는다.

실제 HTTP/브라우저 검사는 Node.js+Playwright와 설치된 Chromium/Edge가 필요하다.
Playwright 전용 브라우저가 없으면 `--browser`로 기존 실행 파일을 명시한다.
개인 브라우저 프로필은 사용하지 않고 독립 headless context 두 개를 만든다.

```powershell
& .\RemoteWork.Server\.venv\Scripts\python.exe Tests/WebChat.Tests/run_browser_check.py `
  --node '절대경로/node.exe' --node-modules '절대경로/node_modules' `
  --browser 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' `
  --report-dir '프로젝트밖/절대보고폴더'
```

이 검사는 공통 테이블 후 **원본 raw005**를 일회성 MySQL에 실행하고 실제 Flask TCP를
띄운다. 브라우저 두 계정에서 새 방 자동 수신/양방향·줄바꿈·초안·일반 텍스트
렌더링·이모지·원 UUID 재전송·저장 후 응답 유실의 읽기 복구·페이지 재조회·개인 숨김·
부서 초대/그룹 필터·같은 계정 재로그인 차단을 확인하고 PC/모바일 PNG를 저장한다.
기존 100건 초과 메시지는 시험 fixture이며, 실제 브라우저에서 cursor를 모두 소비한다.
선택 변경 전의 늦은 응답이 다른 방을 덮지 않는지도 검사한다.

최종 실제 브라우저 검사는 **18개 시나리오 통과**, 스크립트 오류0이다.
첫 실행의 전용 Chromium 미설치는 설치된 Edge를 명시해 해결했다.
브라우저 검사에서 부서 필터의 hidden이 label display에 덮이는 문제를 발견해
제품 CSS에 scoped hidden 규칙을 추가했고, 이후 부서/그룹을 포함해 통과했다.
공통 인증·API·005 SQL을 바꾸거나 실패 시 시험 데이터를 초기화해 숨기지 않았다.

운영 서버 배포·서버 재부팅·물리 두 PC·장시간 부하·첨부 업로드는 미실행/미지원이다.
shared DB/flags·1번 인증 코드·2번 업무·3번 파일·5번 근태는 이 검사를 위해 수정하지 않는다.
