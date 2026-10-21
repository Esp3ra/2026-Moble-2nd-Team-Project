# 5번 C# 지문 등록 중계 검사

저장소 루트에서 실행한다.

```text
dotnet run --project Tests/FingerprintEnrollment.Tests/FingerprintEnrollment.Tests.csproj -c Release
```

통제 HTTP 대역86검증: 고정 직원ID/관리자 접속·허용장치·발급 응답 유실(by-request)·같은 요청키·서버 commit 전후 응답 유실·불확정 Pi 전송 후 읽기 전용 복구·동시 클릭·대상 변경/로그아웃/동일계정 재로그인·scope 바꿔치기·PC Bearer의 Pi 전달 금지·원문 바이트 중계를 확인한다. 장치용 MAC 키는 제품 PC 코드에 없다.

실제 로컬 HTTP로 C#→Python 작업 broker/서명 어댑터를 추가 대조하려면 기존 센서 가상환경과 별도의 테스트 Python 경로를 명시한다. 표준 라이브러리만으로 이 추가 대조를 실행할 수 있다.

```text
dotnet run --project Tests/FingerprintEnrollment.Tests/FingerprintEnrollment.Tests.csproj -c Release -- --pi-python <테스트용-python.exe-절대경로>
```

2026-10-21 MySQL 전용 클라이언트 후속은 추가 대조를 포함해283검증을 통과했다. 위86개/과거114개와 합산하지 않는다. `pi_loopback.py`는 자신이 생성한 loopback HTTP 포트·RAM 상태의 새 MySQL 어댑터·가짜 센서만 사용하고 종료 시 소유 서버/하위 프로세스를 정리한다. 합성 서버의 공개 시험 키로 capture/store/read 승인을 만들고 결과 바이트를 보관하는 대역을 사용한다. 실제 MySQL에 연결하는 검사가 아니다. 테스트의 합성 키/서명기를 실제 PC 중계에 넣지 않는다.

실제 서버/MySQL·운영 권한/키·실물 채취/전원장애·공유/두PC 검증은 아니다. 실제 WinForms 검사는 별도 `FingerprintEnrollment.Ui.Tests` 프로젝트다.
