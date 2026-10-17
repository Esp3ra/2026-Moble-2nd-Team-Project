# W1 후보·쓰기 준비 연결 검증

Windows / .NET8의 STA WinForms 메시지 루프에서 실행합니다.

```powershell
dotnet run --project .\Tests\TaskPreparation.Tests\TaskPreparation.Tests.csproj -c Release
```

선택 인자로 캡처 폴더를 주면 새 후보 창·기존 등록 창을 PNG로 렌더링합니다. 제어 HTTP·실제 공통 Session과 화면으로 후보/동명이인 ID·BIGINT/UTC·잘못된 응답/권한/오류·키/결과·재로그인 후 이전 요청 차단·페이지/검색 선택 유지·늦은 응답/접속 변경 정리·부모 폼·최소 크기를 검사합니다. 기본 실행은 실제 서버/DB·제품 쓰기를 검증하지 않습니다.

## 실제 새 소유 MySQL·HTTP/UI

서버 의존성이 설치된 Python과 MySQL8 바이너리를 지정합니다.

```powershell
dotnet build .\Tests\TaskPreparation.Tests\TaskPreparation.Tests.csproj -c Release
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskPreparation.Tests\run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir "C:\Program Files\MySQL\MySQL Server 8.2\bin"
```

기존 2번의 소유 프로세스/경로 검증 도구로 새 MySQL datadir·loopback 포트·고유 remote_work_test_ DB를 생성합니다. 기존 서비스·사용자.env·공유 DB를 사용하지 않습니다. 합성 관리자/직원/업무와 W1 opt-in을 준비해 관리자 후보/문맥·실제 후보 창·TasksControl의 선택 업무 연결·직원 본인 문맥/후보403·키/미기록 결과·접속 취소를 확인합니다.

업무/배정/관리자 행 전후 일치와 receipt0건을 대조합니다. 합성 비밀번호는 자식 stdin으로만 전달하고 출력하지 않습니다. 종료 시 소유 HTTP/MySQL·임시 DB/runtime만 정리합니다. 실제 업무 행동·제출/승인·공유DDL/버전·두PC·웹/장치·수동전체GUI는 별도입니다.

현재 계약과 다음 합의는 [TASK_WRITE_PREPARATION](../../docs/TASK_WRITE_PREPARATION.md)을 따릅니다.
