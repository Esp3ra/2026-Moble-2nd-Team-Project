# 업무 조회 HTTP 어댑터 검증

Windows와 .NET 8 SDK에서 실행합니다.

```powershell
dotnet run --project .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release
```

기본 실행은 명시적 `HttpMessageHandler` 테스트 대역입니다. 공통 `ApiClient`를 통과하여 상대 경로·Bearer 전달·5상태 매핑·전체 목록·빈 목록·오류 분류·잘못된 응답·취소를 검증합니다. 요청 상태와 다른 단일/혼합 목록은 전체를 거절하고, 빈 필터 결과·전체5상태·목록 이후 상세 상태 변경은 허용하는지도 확인합니다. 실제 공통 ApiClient의 ClearIdentity 취소를 TasksControl까지 전달해 서버 오류로 표시하지 않는지, 이전 데이터 제거와 새 로그인 후 명시적 재시도 복구도 검사합니다. HTTP 전송은 대역이므로 실제 서버·DB 연결 증거로 취급하지 않습니다. 기존 `TaskQueries.Tests`의 WinForms 조회 경계 검증과 `ApiClient.Tests`의 공통 접속 수명 검증은 별도로 실행합니다.

### 2026-10-19 접속 변경·늦은 화면 반영 회귀

기본 실행 **12개 시나리오·132개 검사**에서 다음을 확인합니다. 실제 `ApiClient`와 WinForms 메시지 루프를 사용하되 HTTP 응답·계정·업무는 명시적 대역입니다.

- 요청이 없는 상태에서도 로그아웃하면 이전 계정의 목록·상세·실행 가능한 선택을 지웁니다. 새 로그인만으로 조회하지 않고, 명시적 목록 새로고침/재시도에서 새 계정의 준비 서비스·등록 workflow·현재 역할·첫 선택을 다시 연결합니다.
- HTTP 응답 처리와 목록/상세 DTO 매핑이 끝난 뒤 UI continuation만 남겨 두고 로그아웃합니다. 이전 성공 응답은 화면을 다시 채우거나 새 접속으로 상세를 후속 요청하지 못합니다.
- 이전 계정의 무효화 callback이 대기하는 동안 새 계정 로그인·명시적 새로고침을 완료해도 늦은 callback이 새 화면을 지우지 못합니다. 이전 행 선택도 새 계정의 Bearer로 상세를 요청하지 못합니다.
- 로그아웃 직후 DataGridView의 다음 행을 즉시 선택하는 재진입과, 조회 완료 후 화면 폐기를 각각 검사합니다. 이전 응답은 폐기한 화면에 반영되지 않습니다.

경합 시점은 수동 continuation 큐·HTTP 완료 신호·UI callback barrier로 정하며 새 경합 검사에 고정 sleep을 사용하지 않습니다. 기존 HTTP·필터·오류 검사를 포함한 수치이므로 따로 합산하지 않습니다. 실제 서버/DB·공유 계정·두 PC·수동 전체 UI 검수는 이 실행의 검증 범위가 아닙니다.

## 격리 서버의 실제 HTTP 검증

### MySQL 전용 서버·DB를 자동 준비하는 실행기

주요 DB 검증은 MySQL을 기준으로 합니다. Windows, MySQL 8.0.16 이상(이 환경은 8.2), 서버용 Python 의존성, 빌드한 C# 검증 DLL이 필요합니다. **기존 MySQL 서비스나 Start-MySQL.cmd는 실행하지 않습니다.** 아래 두 옵션으로 새 임시 MySQL을 만드는 검증에 명시적으로 동의하고 실제 MySQL 설치 경로를 전달합니다.

```powershell
dotnet build .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskQueries.Http.Tests\run_mysql_http_check.py --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin'
```

다른 빌드 출력은 `--client-dll <TaskQueries.Http.Tests.dll 절대 경로>`로 지정합니다. 외부 DB URL은 받지 않습니다. 상속된 실제 API/DB/프록시/Python 설정과 `.env`, MySQL 사용자·시스템 기본 설정을 사용하지 않습니다. SQLite DB도 열지 않습니다. MySQL 설치 파일은 읽기만 하며 새 임시 datadir·프로세스·loopback 포트·직접 생성한 고유 DB만 사용합니다.

`--no-defaults`를 첫 MySQL 옵션으로 전달하고 X Plugin을 비활성화합니다. 접속한 서버의 실제 datadir·포트·MySQL 버전·InnoDB·REPEATABLE-READ를 검사한 뒤에만 DB를 생성합니다. 포트 경쟁이나 정리 중 주소 변경이 있으면 다른 서버에 DROP/SHUTDOWN을 보내지 않습니다. 성공한 CREATE에 대해서만 DB 삭제 소유권을 기록합니다. MySQL·C# 자식은 중지 상태로 생성해 Windows Job에 등록한 뒤 공식 WinAPI로 재개하므로 등록 전에 하위 프로세스가 생기지 않습니다. 중단·타임아웃·등록 실패도 이 실행의 프로세스 트리 안에서 정리합니다. 재귀 삭제 직전에는 임시 폴더의 절대 경로·부모·접두사·reparse 여부를 확인합니다.

fixture는 공통 7개 테이블만 명시적으로 준비하고, [현재 002 SQL 초안](../../RemoteWork.Server/migrations/002_task_queries.mysql.sql)의 실제 3문장으로 업무 테이블을 생성합니다. 업무 ORM의 `create_all`이나 공통 `init_schema()`로 SQL 실행을 대신하지 않습니다. 공통 이력은 유지하고 `schema_versions`는 **1**로 남깁니다. 이 경로는 공유 DB 적용 도구나 1번의 버전2 지원 구현이 아닙니다.

검사 범위는 아래 실제 HTTP·WinForms 항목과 같습니다. PK 순서로 모든 공통/업무 행을 해시하고, C# 조회·로컬 입력 준비 및 허용/거절 GET 전후에 행이 변하지 않는지 검사합니다. 관리자 회수·재배정은 임시 fixture의 stdin 제어에서만 수행하며 제품 쓰기 API를 추가하지 않습니다. 비밀번호·토큰·DB 원문·dump·자식 프로세스 출력을 표시하지 않습니다.

2026-10-17 실행 결과: 실제 C# 직원43검사·관리자64검사, MySQL 실행기301검사(권한 회수11검사 포함) 통과. 실행기301에는 C# 호출 성공 검사 등이 포함되므로 수치를 합산하지 않습니다. 실제 DB 생성 후 의도적으로 실패하는 아래 실행도 exit1과 `CLEANUP` 완료를 확인했습니다. 공유 DB·두 PC·수동 UI·등록/수락/마감/제출·승인·쓰기 경합은 실행하지 않았습니다.

```powershell
# 안전 가드 회귀: DB/서버를 시작하지 않음
.\RemoteWork.Server\.venv\Scripts\python.exe -m unittest discover -s .\Tests\TaskQueries.Http.Tests -p test_mysql_http_runner.py -v
# 실제 새 DB 생성 후 의도적으로 실패: exit1과 CLEANUP 안내가 예상 결과
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskQueries.Http.Tests\run_mysql_http_check.py --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin' --exercise-cleanup-failure
```

안전 가드 회귀16개는 opt-in 누락, 기존 datadir 보존, 잘못된 임시 삭제 경로, 포트/서버 불일치, 생성 실패 시 DROP 금지, 정리 전 서버 재대조, 생성한 DB만 정리, 상속된 설정 차단, 예외·중단 시 임시 경로 정리를 검사합니다. native 시작 중 SIGINT·등록 실패와 C# 중단/타임아웃 정리도 대역으로 검사합니다. 이 대역 검사는 실제 MySQL/C# 정상 실행 및 의도적 실패 실행 증거와 구분하며, 실제 C# 강제 중단·타임아웃을 재현한 결과로 표시하지 않습니다.

### 기존 DEMO SQLite 실행기

SQLite는 DEMO 종료 후 삭제 예정입니다. 아래 실행기는 기존 데모 참고용이며 주요 MySQL 검증·운영 관리 대상으로 확장하지 않습니다.

서버용 Python 3.11 이상과 `RemoteWork.Server/requirements.txt` 의존성, Windows·.NET 8 런타임/SDK가 필요합니다. [SETUP](../../docs/SETUP.md)의 서버 가상환경을 준비한 뒤 프로젝트 루트에서 실행합니다. 기존 서버를 켜거나 `.env`·공유 DB·테스트 계정을 따로 설정할 필요가 없습니다.

```powershell
dotnet build .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskQueries.Http.Tests\run_http_check.py
```

별도 빌드 출력은 `--client-dll <TaskQueries.Http.Tests.dll 절대 경로>`로 지정합니다. 실행기는 현재 Python으로 서버를 띄우므로 같은 환경에 서버 의존성이 설치돼 있어야 합니다. DLL이 없으면 실행을 중단하고 빌드 명령을 안내합니다.

실행기는 임시 디렉터리에 SQLite DB·합성 계정·5개 상태의 업무/배정/지정 관리자 fixture를 만들고 `127.0.0.1`의 임의 포트에서 실제 서버를 시작합니다. 그 주소만 직원·관리자의 C# `--live`에 전달합니다. 실제 사용자 설정·상속된 API/DB 설정과 `.env`를 사용하지 않으며 비밀번호·토큰을 출력하지 않습니다. 자식 프로세스와 임시 DB는 성공·실패 후 정리합니다. 이 fixture는 검증용이며 일반 프로그램에 업무 데이터를 자동 생성하지 않습니다.

- C# 실제 HTTP·WinForms: 내 업무·관리 업무의 정확한 ID/정렬, 전체/5상태 필터, 행 선택 상세, 직원의 관리 조회/타인 상세 거절, 서버에서 접속 폐기 후 다음 업무 재조회의 인증 만료·화면 초기화를 검사합니다. 실제 본인 업무에서 수락·연장 준비, 지정 관리 업무에서 수정 준비와 빈 등록 창을 실제 버튼으로 열어 원본 ID/DATE 전달·로컬 CheckedInput·입력 변경 시 무효화를 검사합니다. 등록/연장/수정 확인은 저장·접수 성공으로 처리하지 않습니다.
- 직접 실제 HTTP: 직원·관리자·빈 계정의 정확한 조회 집합과 정렬, 미인증401·권한403·업무 미존재404·로그아웃 접속 거절, 지정 관리자 회수·재배정 후 현재 권한을 검사합니다.
- 권한 변경은 임시 fixture에서만 수행합니다. 업무 등록·재배정·제출 등의 제품 API를 추가하거나 해당 쓰기 계약이 합의됐다고 표시하지 않습니다. 업무 GET 전후의 업무/배정/관리자 행과 감사 이력도 비교합니다.

이 실행의 DB는 SQLite입니다. MySQL·정식 migration/version2·공유 DB·두 PC·웹·제출/승인·장치 연동은 별도 검증 대상입니다. 화면은 자동 검증용 WinForms 메시지 루프에서 생성하며 실제 앱의 수동 사용 검수를 대신하지 않습니다.

### 직접 준비한 격리 서버 사용

운영 DB·계정을 사용하지 않습니다. 별도로 생성한 테스트 서버·DB·계정에만 다음 환경변수를 설정하고 `--live`를 전달합니다. 실행기는 자격증명·응답 본문을 출력하지 않습니다.

격리 서버는 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`로 업무 조회 개발 초안을 명시적으로 활성화해야 합니다. 기본 서버에는 업무 API가 등록되지 않아 `HTTP_404`가 발생하며 클라이언트는 연결 필요 상태로 표시합니다. API가 활성화된 서버에서 실제 요청이 성공한 경우에만 실제 데이터 조회 검증으로 기록합니다. 활성화하지 않은 공통 서버의 DB에는 업무 테이블을 자동 생성하지 않습니다.

| 환경변수 | 값 |
|---|---|
| `REMOTEWORK_TASK_TEST_ALLOW_LIVE` | 명시적 동의 표시 `1` |
| `REMOTEWORK_TASK_TEST_URL` | 격리 서버 루트 주소. `/api/v1` 제외 |
| `REMOTEWORK_TASK_TEST_LOGIN_ID` | 격리 테스트 계정 로그인 ID |
| `REMOTEWORK_TASK_TEST_PASSWORD` | 격리 테스트 계정 비밀번호 |
| `REMOTEWORK_TASK_TEST_FORBIDDEN_TASK_ID` | 선택 사항. 접근 금지를 기대하는 타인 업무 ID |
| `REMOTEWORK_TASK_TEST_EXPECTED_OWN_IDS` | 선택 사항. 정렬 순서대로 예상 내 업무 ID를 담은 JSON 문자열 배열 |
| `REMOTEWORK_TASK_TEST_EXPECTED_MANAGED_IDS` | 관리자 선택 사항. 예상 지정 관리 업무 ID의 JSON 문자열 배열 |

테스트 계정의 내 업무에는 5상태별 업무가 최소 1개씩 있어야 합니다. 관리자 계정은 본인과 업무별 지정 관리자 범위를 구분하며 관리 업무도 최소 1개 준비합니다. 직원 계정은 관리 업무 목록에 403을 기대합니다.

위 자동 실행기는 금지 ID와 예상 ID 배열을 항상 지정합니다. 수동 `--live`에서 배열을 생략하면 미리 준비한 fixture와 정확한 권한 집합을 대조한 증거로 취급하지 않습니다. C# 검증은 마지막에 별도 HTTP 호출로 자신의 테스트 접속을 서버에서 로그아웃한 뒤 다음 업무 조회가401·인증 만료로 처리되는지 확인합니다. 검증 전용 접속만 사용합니다.

```powershell
dotnet run --project .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release -- --live
```

실제 로그인·목록·상세·5상태 필터·관리 범위를 조회한 다음 STA WinForms 메시지 루프에서 `TasksControl`에 실제 서비스를 주입합니다. 목록과 첫 상세 표시, 행 선택, 전체/5상태 필터, 관리자 관리 범위 전환을 확인합니다. 첨부파일·제출·검토 등 3번 기능은 연결하지 않습니다. 종료 시 테스트 접속을 로그아웃합니다. 두 PC, 운영 데이터, 제출·승인 흐름은 이 검증 범위에 포함하지 않습니다.

현재 조회 경로·DTO·권한·오류·상태/날짜 표현은 1번이 수용했습니다. 상태 전환·마감·제출 쓰기 계약은 3번과 합의 필요이며, 정식 DDL·공통 버전 지원·공유 적용은 별도 확인 전입니다. 실제 조회 검증 결과가 쓰기/제출·공유 DB·원격 운영 검증 완료를 의미하지 않습니다. 경계를 변경할 때는 [2번 협업 규약](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/blob/LOG/2번/2026-10-17_2번_영역수정_협업규약.md)의 영향별 검증과 PR 전달 기준을 따릅니다.
