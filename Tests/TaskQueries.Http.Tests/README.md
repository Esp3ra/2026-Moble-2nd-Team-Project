# 업무 조회 HTTP 어댑터 검증

Windows와 .NET 8 SDK에서 실행합니다.

```powershell
dotnet run --project .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release
```

기본 실행은 명시적 `HttpMessageHandler` 테스트 대역입니다. 공통 `ApiClient`를 통과하여 상대 경로·Bearer 전달·5상태 매핑·전체 목록·빈 목록·오류 분류·잘못된 응답·취소를 검증합니다. 실제 서버·DB 연결 증거로 취급하지 않습니다. 기존 `TaskQueries.Tests`의 WinForms 조회 경계 검증과 `ApiClient.Tests`의 공통 접속 수명 검증은 별도로 실행합니다.

## 격리 서버의 실제 HTTP 검증

운영 DB·계정을 사용하지 않습니다. 별도로 생성한 테스트 서버·DB·계정에만 다음 환경변수를 설정하고 `--live`를 전달합니다. 실행기는 자격증명·응답 본문을 출력하지 않습니다.

격리 서버는 `REMOTEWORK_ENABLE_TASK_QUERY_DRAFT=1`로 업무 조회 개발 초안을 명시적으로 활성화해야 합니다. 기본 서버에는 업무 API가 등록되지 않아 `HTTP_404`가 발생하며 클라이언트는 연결 필요 상태로 표시합니다. API가 활성화된 서버에서 실제 요청이 성공한 경우에만 실제 데이터 조회 검증으로 기록합니다. 활성화하지 않은 공통 서버의 DB에는 업무 테이블을 자동 생성하지 않습니다.

| 환경변수 | 값 |
|---|---|
| `REMOTEWORK_TASK_TEST_ALLOW_LIVE` | 명시적 동의 표시 `1` |
| `REMOTEWORK_TASK_TEST_URL` | 격리 서버 루트 주소. `/api/v1` 제외 |
| `REMOTEWORK_TASK_TEST_LOGIN_ID` | 격리 테스트 계정 로그인 ID |
| `REMOTEWORK_TASK_TEST_PASSWORD` | 격리 테스트 계정 비밀번호 |
| `REMOTEWORK_TASK_TEST_FORBIDDEN_TASK_ID` | 선택 사항. 접근 금지를 기대하는 타인 업무 ID |

테스트 계정의 내 업무에는 5상태별 업무가 최소 1개씩 있어야 합니다. 관리자 계정은 본인과 업무별 지정 관리자 범위를 구분하며 관리 업무도 최소 1개 준비합니다. 직원 계정은 관리 업무 목록에 403을 기대합니다.

```powershell
dotnet run --project .\Tests\TaskQueries.Http.Tests\TaskQueries.Http.Tests.csproj -c Release -- --live
```

실제 로그인·목록·상세·5상태 필터·관리 범위를 조회한 다음 STA WinForms 메시지 루프에서 `TasksControl`에 실제 서비스를 주입합니다. 목록과 첫 상세 표시, 행 선택, 전체/5상태 필터, 관리자 관리 범위 전환을 확인합니다. 첨부파일·제출·검토 등 3번 기능은 연결하지 않습니다. 종료 시 테스트 접속을 로그아웃합니다. 두 PC, 운영 데이터, 제출·승인 흐름은 이 검증 범위에 포함하지 않습니다.

업무 API와 DB 스키마는 1·3번과 최종 합의 전인 2번 개발 초안입니다. 실제 조회 검증 결과가 팀 전체 규약 확정이나 원격 운영 검증 완료를 의미하지 않습니다.
