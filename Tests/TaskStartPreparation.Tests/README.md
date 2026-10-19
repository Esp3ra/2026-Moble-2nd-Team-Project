# 수락·재작업 시작의 읽기 전용 대상 확인 검증

Windows/.NET 8의 실제 WinForms 메시지 루프에서 검사합니다. 이 프로젝트는 수락·재작업 상태 저장을 구현하거나 호출하지 않습니다. [현재 범위와 합의 목록](../../docs/TASK_START_PREPARATION.md)을 먼저 확인합니다.

## 통제 HTTP와 실제 WinForms

저장소 루트의 PowerShell에서 실행합니다.

```powershell
dotnet run --project .\Tests\TaskStartPreparation.Tests\TaskStartPreparation.Tests.csproj -c Release
```

2026-10-19 실행에서 **149검사**가 통과했습니다. 두 행동×5상태×본인/타인 비교, 정확한 BIGINT·버전·UTC 검증, 기본 생성자 보존, 직원/관리자 본인 확인, 타인 지정 관리자 차단, 원본/현재 상태 불일치, 명시 확인과 불변 값, 재조회/실패/닫기에서 확인값 무효화, 401/403/404/503, 같은 회원 재로그인·이전 응답·닫힌 창의 응답을 검사합니다. 부모 화면의 버튼 상태와 변경 감지 후 기존 필터 유지 재조회, 이전 로그인 창의 새 접속 조회 차단도 확인합니다.

HTTP 처리기는 로그인 외의 모든 POST/PATCH 등 읽기 아닌 요청을 집계합니다. 이 대상 준비에서 업무 쓰기·키 발급·저장 결과 조회는 **0회**입니다. 제목/날짜와 write-context는 별도 시점이며, 과거 날짜가 있다고 준비 과정에서 임의 날짜 정책을 적용하지 않습니다. `CheckedTarget`은 읽은 대상 확인값이며 실행 권한이나 쓰기 DTO가 아닙니다.

경로 인자를 하나 더 주면 테스트 폼의 기본/최소 크기와 스크롤 하단을 PNG로 캡처합니다. 최소 크기의 버튼 경계는 실제 컨트롤 좌표로 검사합니다. DrawToBitmap은 스크롤 자식을 영역 밖에 그리는 캡처 왜곡이 있어 이 이미지를 실제 화면의 클리핑 검수로 사용하지 않습니다. 자동화된 실제 UI 검증과 사용자가 직접 조작하는 수동 전체 GUI 검수는 구분합니다.

## 실제 HTTP·MySQL 읽기

서버 의존성이 설치된 전용 Python 가상환경과 MySQL 바이너리의 절대경로가 필요합니다. Python은 존재하는 실행 파일로 고정하며 WindowsApps `python`/`py` 별칭을 사용하지 않습니다. 아래는 서버 가상환경이 준비된 경우의 예입니다.

```powershell
$startTestPython = (Resolve-Path '.\RemoteWork.Server\.venv\Scripts\python.exe').Path
dotnet build .\Tests\TaskStartPreparation.Tests\TaskStartPreparation.Tests.csproj -c Release
& $startTestPython .\Tests\TaskStartPreparation.Tests\run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin'
```

실행기는 기존 owned MySQL/Windows Job 도구를 재사용합니다. 새 datadir·loopback 포트·고유 테스트 DB·별도 HTTP 프로세스만 준비하며 기존 서비스·공유 DB·사용자 `.env`·SQLite는 사용하지 않습니다. 패키지를 자동 설치하지 않습니다. `--allow-disposable-mysql`이 없으면 기동 전 거절합니다. 설정·무작위 합성 비밀번호는 비공개 stdin으로 전달하며 토큰·암호·행 원문과 실패한 자식 출력을 표시하지 않습니다.

공통 테이블과 검토용 raw002/raw004만 준비하고 전역 schema version1을 유지합니다. 직원 본인의 진행전/반려완료 업무101/102, 관리자 본인의103/104, 다른 직원이 담당하고 관리자에게 조회만 허용한105를 준비합니다. 각 업무·배정·관리자는 5행입니다. `returned`와 고정 과거 `accepted_at`은 조회 검사 자료를 직접 seed한 것이며 실제 제출·반려·수락·재작업을 실행해 만든 자료가 아닙니다. 3번 제출 테이블은 만들지 않습니다.

2026-10-19 실행 결과는 다음과 같습니다. 각 검사 수를 다른 실행과 합산하지 않습니다.

| 실행 | 결과와 범위 |
|---|---|
| 실제 C# HTTP/WinForms | **20검사**. 직원·관리자 본인 수락/재작업 대상의 상세→문맥 GET→확인→재조회 무효화, 타인 지정 관리자 확인 차단, 제품 쓰기·키·결과 요청0 |
| 독립 MySQL 검사 | **13검사**. 업무·배정·관리자 세 테이블의 전행 해시 동일, 각각5행, 업무 감사·receipt0, schema1, 테이블 집합과 업무별 합성 초기값 |
| 기동/정리 가드 | opt-in 없는 기동 거절, 정상 종료와 의도적 실패 후 소유 HTTP/MySQL·DB·임시 runtime 정리 확인 |

실행 환경은 Python3.12.14, Flask3.1.3, SQLAlchemy2.0.54, PyMySQL1.1.3, MySQL8.2입니다. 실패 정리를 점검하려면 같은 명령에 `--exercise-cleanup-failure`를 추가합니다. 소유 DB 준비 직후 의도적으로 종료1을 반환하므로 정상 통과로 세지 않습니다.

## 미실행·경계

수락/재작업 실제 POST·버전 증가·accepted_at 저장·감사/receipt 성공·롤백·동시 상태 전환은 아직 구현/검증하지 않았습니다. 시작일 전·마감 후 허용과 재작업 action·API/결과·오류 계약은 합의 필요입니다. 3번 제출·차수·승인/반려·파일·화면용 이력, 공유 DB·두 PC·수동 전체 GUI도 이번 범위가 아닙니다. 실제 TCP HTTP를 사용했지만 장애 주입·동시 상태 쓰기나 원격 PC 네트워크 검증으로 확대하지 않습니다. 자동 결과 제출은 추가하지 않습니다.
