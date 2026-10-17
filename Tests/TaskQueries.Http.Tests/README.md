# 업무 조회 HTTP 어댑터 검증

Windows와 .NET 8 SDK에서 실행합니다.

```powershell
dotnet run --project .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release
```

기본 실행은 명시적 `HttpMessageHandler` 테스트 대역입니다. 공통 `ApiClient`를 통과하여 상대 경로·Bearer 전달·5상태 매핑·전체 목록·빈 목록·오류 분류·잘못된 응답·취소를 검증합니다. 요청 상태와 다른 단일/혼합 목록은 전체를 거절하고, 빈 필터 결과·전체5상태·목록 이후 상세 상태 변경은 허용하는지도 확인합니다. 실제 공통 ApiClient의 ClearIdentity 취소를 TasksControl까지 전달해 서버 오류로 표시하지 않는지, 이전 데이터 제거와 새 로그인 후 명시적 재시도 복구도 검사합니다. HTTP 전송은 대역이므로 실제 서버·DB 연결 증거로 취급하지 않습니다. 기존 `TaskQueries.Tests`의 WinForms 조회 경계 검증과 `ApiClient.Tests`의 공통 접속 수명 검증은 별도로 실행합니다.

## 격리 서버의 실제 HTTP 검증

### 임시 서버·DB를 자동 준비하는 실행기

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
