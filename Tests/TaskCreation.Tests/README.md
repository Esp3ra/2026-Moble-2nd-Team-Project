# 신규 업무 등록 검증

Windows / .NET 8에서 신규 등록의 HTTP 계약·요청 복구·실제 WinForms 동작을 검사합니다. 기본 실행은 명시적인 HTTP 대역을 사용합니다. 별도 MySQL 실행기만 실제 공통 API와 DB에 연결합니다. 제품 동작과 남은 합의는 [TASK_CREATION](../../docs/TASK_CREATION.md)을 따릅니다.

## 제어 HTTP와 실제 WinForms

저장소 루트의 PowerShell에서 실행합니다.

```powershell
dotnet run --project .\Tests\TaskCreation.Tests\TaskCreation.Tests.csproj -c Release
```

STA WinForms 메시지 루프에서 실제 등록 창과 업무 목록 화면을 만들고 제어 HTTP 응답을 주입합니다. 요청 형식·Unicode·불변 본문, 입력 확인과 명시적 등록 구분, 중복 실행 차단, 결과 불확실과 명시 조회·같은 키/본문 재전송, 401·403·접속 변경·늦은 응답을 검사합니다. 같은 로그인에서 창·메뉴를 다시 열어도 요청과 권한 차단이 유지되는지, 재로그인/다른 클라이언트가 이전 요청을 이어 쓰지 않는지도 검사합니다.

POST/재전송의 키 만료 410 이후에도 보관된 성공 결과를 조회할 수 있고, 결과 조회의 410이면 새 키·자동 등록 없이 수동 확인으로 남는지 구분합니다. 재전송 전에 취소된 요청은 POST하지 않고 기존 본문과 키를 보관합니다. 성공 후에는 현재 업무 범위·상태 필터를 유지하며 목록/상세를 새로 받습니다.

이는 자동화된 실제 컨트롤·모달 검사입니다. 사용자가 직접 모든 화면을 조작한 수동 GUI 검수나 실제 서버/DB 검증은 아닙니다.

## 새 소유 MySQL에서 실제 HTTP/UI

서버 의존성이 설치된 전용 Python 가상환경과 MySQL 8 설치의 바이너리 경로가 필요합니다. Python은 **존재하는 실행 파일의 절대경로로 고정**하고 WindowsApps의 `python`/`py` 별칭은 사용하지 않습니다. 아래 예시는 저장소 서버용 가상환경이 이미 준비된 경우입니다. 실행기는 패키지를 설치하거나 기존 가상환경을 수정하지 않습니다.

```powershell
$serverPython = (Resolve-Path '.\RemoteWork.Server\.venv\Scripts\python.exe').Path
dotnet build .\Tests\TaskCreation.Tests\TaskCreation.Tests.csproj -c Release
& $serverPython .\Tests\TaskCreation.Tests\run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin'
```

`--mysql-bin-dir`은 설치된 바이너리 폴더의 절대경로입니다. 기존 서버나 DB 주소를 지정하는 옵션이 아닙니다. `--allow-disposable-mysql`을 생략하면 프로세스와 DB를 시작하지 않고 실패합니다. Release DLL을 먼저 빌드해야 합니다.

실행기는 기존 조회 테스트의 소유 자원 도구를 재사용해 다음 범위만 준비합니다.

- 새 MySQL 프로세스·임시 datadir·loopback 포트·고유 `remote_work_test_m2http_...` DB. 기존 서비스·사용자 `.env`·공유 DB·SQLite는 사용하지 않습니다.
- 공통 테이블과 검토용 raw002/raw004를 분리해 초기화하고 전역 schema version1을 유지합니다. query/common-write/create 세 개발 플래그는 이 fixture에서만 켭니다.
- 합성 관리자·직원과 무작위 비밀번호. 클라이언트 설정은 비공개 stdin으로 전달하고 토큰·비밀번호·행 원문·자식 출력은 로그에 내보내지 않습니다.
- 소유 프로세스와 경로·포트·DB 이름을 확인한 후에만 정리합니다. Windows Job으로 자식 수명을 관리하며 정상 종료와 실패 시 자신이 만든 HTTP/MySQL·DB·임시 runtime만 정리합니다.

실제 C# 공통 ApiClient와 TCP·MySQL을 사용하는 시나리오는 세 건입니다.

| 제목 | 실제 검증 |
|---|---|
| `creation-live-ui` | 자동화된 등록 모달에서 입력 확인·등록 → 서버 저장 → 관리 업무 목록 재조회·생성 ID의 행/상세 표시 |
| `creation-live-lost` | 서버 응답을 받은 뒤 클라이언트 HTTP 처리기에서 응답 유실을 주입 → 결과 조회 `completed` → POST 재전송 없이 성공 복구 |
| `creation-live-retry` | 첫 POST를 서버에 보내기 전에 클라이언트 HTTP 처리기에서 전송 실패 주입 → 결과 조회 `not_recorded` → 명시적으로 같은 키·본문 재전송 |

응답 유실과 전송 실패는 테스트 처리기가 주입하며 물리적 네트워크 단절을 재현한 것은 아닙니다. 실제 HTTP 회차의 후보 ID 선택값은 테스트가 주입합니다. 후보 검색·페이지·선택 UI는 [TaskPreparation.Tests](../TaskPreparation.Tests/README.md)에서 별도로 검사합니다.

서버 프로세스의 독립 검사는 업무3·배정3·관리자6·`task.created` 감사3·receipt3을 대조합니다. 각 업무의 등록자 관리자 관계, 진행전·배정/버전1/1·수락 시각 NULL, 감사/receipt 최소 결과와 전역 schema version1을 확인합니다. 같은 키 복구가 업무·감사·receipt를 중복 생성하지 않는지도 이 행 수로 확인합니다.

실패 정리를 따로 점검하려면 같은 명령에 `--exercise-cleanup-failure`를 추가합니다. 소유 DB 준비 후 의도적으로 종료 코드1을 반환하는 옵션입니다. 정상 통과로 세지 않으며 종료 후 소유 자원 정리 여부를 확인합니다.

## 확인한 결과와 남은 범위

2026-10-19의 실행 결과입니다. 검사 수는 각 실행의 단위이며 합산하지 않습니다.

| 실행 | 결과 |
|---|---|
| TaskCreation 제어 HTTP/WinForms | 79개 통과 |
| TaskCreation 실제 C# HTTP/UI | 10개 통과 |
| 독립 MySQL 저장 결과 | 24개 통과 |
| 기존 후보 준비 회귀 | TaskPreparation 87개 통과 |
| 기존 업무 조회 회귀 | TaskQueries 171개 통과 |

실제 MySQL 실행은 Python 3.12.14·Flask 3.1.3·SQLAlchemy 2.0.54·PyMySQL 1.1.3·Waitress 3.0.2와 MySQL 8.2에서 확인했습니다. 정상 실행 및 의도적 실패의 소유 자원 정리를 확인했습니다. 서버 등록 단독 MySQL18개는 앞선 서버 구현 회차의 기록이며 위 실행 결과에 더하지 않습니다.

1번 공동 기술 검토·정식 migration·전역 버전 변경·공유 배포는 미완료입니다. 개발 API는 세 플래그를 명시할 때만 열리며 기본 비활성화입니다. 공유 서버·두 PC·수동 전체 GUI·앱 재시작 후 요청 복구는 검증하지 않았습니다. 업무 수정·수락·재작업·마감 연장 저장과 3번 제출·파일·승인/반려 기능도 이번 검사 범위가 아닙니다. 자동 결과 제출은 추가하지 않습니다.
