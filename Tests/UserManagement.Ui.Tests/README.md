# 회원 관리·새 회원 지문 등록 연결 검사

Windows/.NET8 Desktop에서 실행한다. 앱 프로젝트를 참조하여 실제 WinForms 메시지 루프와 `UserManagementForm`/`FingerprintEnrollmentForm`을 사용한다.

```powershell
dotnet run --project Tests/UserManagement.Ui.Tests/UserManagement.Ui.Tests.csproj -c Release
```

- 기존 회원 관리: 14시나리오46검사. 생성 응답ID 유지·목록 밖 회원·갱신 실패·늦은 생성·동일계정 재로그인/401·종료/Dispose·중복 저장·응답 유실을 확인한다.
- 추가 연결: 명시 버튼/생성ID 고정, 두 장치 중 선택한 주소, 주소 누락의 **발급 POST 이전 차단**, 시작 전 수정, 현재 API 서버 불일치, 실제 등록창→서버 완료 확인, 창 닫기/로그아웃 뒤 늦은 결과 거절, `PendingOperation` 대기 뒤 채널 해제를 확인한다. 엄격 설정/중복 필드·잘못된 주소·HTTP 명시 선택·다른 장치 작업 거절도 포함한다.

## MySQL 등록 계약을 사용하는 중계 코드와 결합할 때

[5번 PR56](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/56)의 중계는 Pi 호출 전 `GET physical-result`, 원문 수신 후 `POST physical-result`, 그 다음 `POST complete`를 사용한다. 옛 모의서버의 경로 누락으로 발생했던 `Modal registration driver` 시간초과를 보완했다. 모의 응답은 `enrollment-mysql-v1`의 15필드와 미기록/기록 상태를 제공하며, revision·발급 작업·정확한 원문 선보관·동일 재전송/충돌을 검사한다. 제품 중계의 필수 조회나 서버 검증을 완화하지 않는다.

두 PR의 코드를 결합한 작업본에서는 **필수 모드**로 실행한다.

```powershell
dotnet run --project Tests/UserManagement.Ui.Tests/UserManagement.Ui.Tests.csproj -c Release -- --require-mysql-enrollment
```

필수 모드는 Pi 호출 전에 physical 조회가 없거나 원문 선보관 없이 완료를 요청하면 실패한다. 기본 모드는 아직 병합 전인 이전 중계의 회귀도 실행할 수 있지만, 이를 새 계약 검증으로 보고하지 않는다. 기본 모드에서도 physical 경로가 사용되면 원문 선보관 검사를 적용한다. 강제 모드에서 이전 중계를 실행하면 의도적으로 종료값1로 거절한다.

2026-10-21 직접 실행: 1번 `0871b2c`에 이 테스트 보완을 적용한 기본 모드 **연결153검사**, 같은 작업본에 고정된 5번 `0bf93bca7891c3ae73e00a5ceb3f3b9465f85dd6`의 PR56 변경24파일만 겹친 독립 작업본의 기본 명령 **연결173검사**·필수 모드 **연결177검사** 통과. 각 실행의 기존 회원관리 **14시나리오46검사**도 통과했다. 수치에는 모의서버 자체의 미기록/다른 원문 완료 거절·동일 재전송 검사도 포함되며 서로 합산하지 않는다. 수정 전 동일 결합본의 시간초과와 수정 후 통과를 대조했다.

API는 통제 `HttpMessageHandler`, Pi는 완료를 지연시킬 수 있는 대역이다. 실제 서버/MySQL·Pi HTTP·서명·UART/센서·TLS·두PC 검증이 아니다. fixture의 상태 보관은 메모리이며 MySQL 영속성 검사로 취급하지 않는다. 실제 C#→Python loopback은 [5번 중계 검사](../FingerprintEnrollment.Tests/README.md)를 따른다. 서로 합산하지 않는다.

검사는 임시 설정 파일만 생성/삭제하고 실제 `%LOCALAPPDATA%` 설정이나 계정/서버/DB를 바꾸지 않는다. 실제 운영 설정은 [연결 안내](../../docs/DEVICE_ENROLLMENT_API.md)를 따른다.
