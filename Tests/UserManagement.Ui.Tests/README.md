# 회원 관리·새 회원 지문 등록 연결 검사

Windows/.NET8 Desktop에서 실행한다. 앱 프로젝트를 참조하여 실제 WinForms 메시지 루프와 `UserManagementForm`/`FingerprintEnrollmentForm`을 사용한다.

```powershell
dotnet run --project Tests/UserManagement.Ui.Tests/UserManagement.Ui.Tests.csproj -c Release
```

- 기존 회원 관리: 14시나리오46검사. 생성 응답ID 유지·목록 밖 회원·갱신 실패·늦은 생성·동일계정 재로그인/401·종료/Dispose·중복 저장·응답 유실을 확인한다.
- 추가 연결: 112검사. 명시 버튼/생성ID 고정, 두 장치 중 선택한 주소, 주소 누락의 **발급 POST 이전 차단**, 시작 전 수정, 현재 API 서버 불일치, 실제 등록창→서버 완료 확인, 창 닫기/로그아웃 뒤 늦은 결과 거절, `PendingOperation` 대기 뒤 채널 해제를 확인한다. 엄격 설정/중복 필드·잘못된 주소·HTTP 명시 선택·다른 장치 작업 거절도 포함한다.

API는 통제 `HttpMessageHandler`, Pi는 완료를 지연시킬 수 있는 대역이다. 실제 서버/MySQL·Pi HTTP·서명·UART/센서·TLS·두PC 검증이 아니다. 실제 C#→Python loopback의 별도114검사는 [5번 중계 검사](../FingerprintEnrollment.Tests/README.md)를 따른다. 서로 합산하지 않는다.

검사는 임시 설정 파일만 생성/삭제하고 실제 `%LOCALAPPDATA%` 설정이나 계정/서버/DB를 바꾸지 않는다. 실제 운영 설정은 [연결 안내](../../docs/DEVICE_ENROLLMENT_API.md)를 따른다.
