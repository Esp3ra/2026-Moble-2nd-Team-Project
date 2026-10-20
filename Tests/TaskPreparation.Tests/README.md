# W1 후보·쓰기 준비 연결 검증

Windows / .NET8의 STA WinForms 메시지 루프에서 실행합니다.

```powershell
dotnet run --project .\Tests\TaskPreparation.Tests\TaskPreparation.Tests.csproj -c Release
```

선택 인자로 캡처 폴더를 주면 새 후보 창·기존 등록 창을 PNG로 렌더링합니다. 제어 HTTP·실제 공통 Session과 화면으로 후보/동명이인 ID·BIGINT/UTC·잘못된 응답/권한/오류·키/결과·재로그인 후 이전 요청 차단·페이지/검색 선택 유지·늦은 응답/접속 변경 정리·부모 폼·최소 크기를 검사합니다. 기본 실행은 실제 서버/DB·제품 쓰기를 검증하지 않습니다.

신규 업무의 관리자 선택 규칙도 기본 실행에서 검사합니다.

- 관리자 계정의 신규 업무 준비 화면은 등록자를 필수 관리자로 자동 포함하고 최초 등록에서 제거할 수 없도록 표시합니다.
- 추가 관리자는 선택·제거할 수 있고, 페이지·검색·후보 종류를 바꿔도 선택을 유지합니다. 새로고침으로 기존 선택을 지운 뒤에도 등록자는 다시 포함합니다.
- 선택 확인과 부모 등록 폼 전달 시 등록자 ID를 보존하며, 인증·권한 오류나 로그인 변경 시 등록자를 포함한 이전 준비 정보를 지웁니다.
- 비관리자의 신규 준비 화면에는 관리자 자동 지정·후보 조회·확인 기능을 제공하지 않습니다.
- 기존 업무의 관리자 선택·제거에는 신규 등록자의 필수 포함 규칙을 적용하지 않습니다. 기존 업무 변경 규칙을 확정하는 검사가 아닙니다.

이 검사는 명시적인 테스트 대역과 실제 WinForms 화면을 사용합니다. 준비 화면이 요청 키 발급·쓰기 결과 조회를 호출하지 않는지도 확인합니다. 준비 UI 통과는 실제 등록 저장 검증이 아닙니다. 별도 [신규 등록 서버 개발 API](../../docs/TASK_CREATION.md)의 저장·관리자·트랜잭션은 MySQL 서버 테스트에서 구분해 검증합니다.

## 실제 새 소유 MySQL·HTTP/UI

서버 의존성이 설치된 Python과 MySQL8 바이너리를 지정합니다.

```powershell
dotnet build .\Tests\TaskPreparation.Tests\TaskPreparation.Tests.csproj -c Release
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskPreparation.Tests\run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir "C:\Program Files\MySQL\MySQL Server 8.2\bin"
```

기존 2번의 소유 프로세스/경로 검증 도구로 새 MySQL datadir·loopback 포트·고유 remote_work_test_ DB를 생성합니다. 기존 서비스·사용자.env·공유 DB를 사용하지 않습니다. 합성 관리자/직원/업무와 W1 opt-in을 준비해 관리자 후보/문맥·실제 후보 창·TasksControl의 선택 업무 연결·직원 본인 문맥/후보403·키/미기록 결과·접속 취소를 확인합니다.

업무/배정/관리자 행 전후 일치와 receipt0건을 대조합니다. 합성 비밀번호는 자식 stdin으로만 전달하고 출력하지 않습니다. 종료 시 소유 HTTP/MySQL·임시 DB/runtime만 정리합니다. 실제 업무 행동·제출/승인·공유DDL/버전·두PC·웹/장치·수동전체GUI는 별도입니다.

현재 계약과 다음 합의는 [TASK_WRITE_PREPARATION](../../docs/TASK_WRITE_PREPARATION.md)을 따릅니다.
