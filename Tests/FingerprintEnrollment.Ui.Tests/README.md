# 5번 지문 등록 전용 WinForms 검사

저장소 루트/Windows/.NET8 Desktop 환경에서 실행한다.

```text
dotnet run --project Tests/FingerprintEnrollment.Ui.Tests/FingerprintEnrollment.Ui.Tests.csproj -c Release
```

실제 WinForms 메시지 루프·UI 스레드 검사와 통제 HTTP/Pi 대역으로12검증을 수행한다. 생성된 직원ID 표시, 중복 시작/장치 변경 차단, 서버 completed 뒤 완료 표시, 창/대상 종료 뒤 늦은 결과 차단, 로그아웃 때 창 종료를 확인한다. 검사용 폼은 작업 표시줄에 노출하지 않고 투명하게 실행한다.

실제 관리자 화면 버튼/1번 `CreatedUserEnrollmentTarget` 주입, 실물 센서/라파·공유 서버/MySQL을 검증한 것은 아니다. 폼의 연결 접점은 근태 README와 Pi 등록 문서를 따른다.
