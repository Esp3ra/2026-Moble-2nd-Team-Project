# 업무 수락·재작업 시작 클라이언트 검사

Windows / .NET 8에서 저장소 루트의 PowerShell로 실행합니다.

```powershell
dotnet run --project .\Tests\TaskStart.Tests\TaskStart.Tests.csproj -c Release
```

기존 공통 `ApiClient`의 실제 로그인·captured session을 사용하고, HTTP 응답만 명시적인 메모리 대역으로 제어합니다. 실제 서버·MySQL·두 PC 또는 수동 GUI 검수가 아닙니다.

- 수락/재작업 각각의 action·경로·정규 BIGINT 문자열·두 필드 본문·공통 Bearer/Idempotency-Key 헤더
- 정확한 5필드 응답, task/row+1/배정 차수/진행중/UTC 일치와 미지원·중복 필드 거절
- 결과 유실 → 명시적인 결과 조회 → `not_recorded` 뒤에만 동일 키·본문 재전송
- POST 키 만료 뒤 보관된 결과 확인과 결과 조회 만료 뒤 수동 확인 구분
- 명확한 업무 오류, 연결 미준비, 모순 수락 기록, 버전 상한과 날짜 오류의 안내
- 메뉴·창 복귀 시 불확실 요청 찾기, 같은 로그인 403 재진입 차단, 다른 로그인·클라이언트와 늦은 응답 격리
- 동시 실행 차단, 성공 확인 후 초기화, 이미 성공한 오래된 확인값 재사용 방지

모든 데이터·계정·비밀번호 문자열은 명시적 합성 테스트 값입니다. 대역이 돌려준 403은 실제 서버의 타인 업무 접근 차단 증거가 아니며, direct 성공 HTTP 상태는 기존 공통 클라이언트에서 노출되지 않아 서버/실제 HTTP 검사에서 별도로 검증해야 합니다.

## 준비된 소유 검증 서버에 실제 HTTP 연결

`--http`는 별도로 준비한 **격리된 소유 MySQL/API fixture**와 합성 직원 계정, 현재 본인 `not_started` 업무와 최초 수락 기록이 있는 `returned` 업무가 필요합니다. 서버의 query/common-write/start opt-in과 한국 시간의 시작~마감 기간도 fixture에서 먼저 준비해야 합니다. 이 모드는 서버·DB·계정을 만들거나 사용자 환경을 수정하지 않습니다.

실행기는 `server_root`, `login_id`, `password`, `accept_task_id`, `rework_task_id` 문자열 다섯 개만 담은 JSON 한 줄을 비공개 stdin으로 전달하고 `TaskStart.Tests.dll --http`를 실행합니다. 실제 연결 정보·비밀번호를 명령줄·저장소·테스트 로그에 쓰지 않습니다. 이 옵션을 생략한 일반 실행은 실제 서버에 연결하지 않습니다.

실제 공통 로그인, 업무 상세·현재 배정 조회에서 생성한 CheckedTarget, 최초 수락·재작업의 실제 HTTP200/버전+1/배정 유지, 커밋 후 응답 유실의 결과 조회 복구, 공통 receipt 확인과 같은 키·본문 재응답, 현재 상태·본인 업무 상태 필터를 검사합니다. 수락 기록과 감사/receipt의 DB 증거는 소유 MySQL 실행기에서 별도로 확인해야 하며, 이 검사는 사용자 공유 서버나 두 PC 연결 검증이 아닙니다.
