# 지문 출근·로그인 조립 검사

2026-10-22 후속: `Workday.cs`/실제 서버 모드4개를 추가했다. 아래77/13은 PR69 당시 결과이며, 현재 기본 명령은 정오·퇴근 검사를 함께 실행한다. [연결 경계](../../RemoteWork.WinForms/Features/Attendance/WORKDAY_IDENTITY.md)를 따른다. 서버 runner의 현재 선택 모듈은 기존10 + 로그인3 + 정오/퇴근4 = **17 pytest 시나리오**다. 실제 실행 결과는 후속 검증 기록과 구분한다.

최종 `a02bd5a`/원본 `5594f2a`: 기본 명령 **179검사 PASS**, Release0오류0경고. 같은 DLL 고정의 실제 서버 검사 **17 passed in51.00s / exit0**, 소유 MySQL/DB/runtime 정리true. 두 검사 수치는 합산하지 않는다. 정오/퇴근은 공개 synthetic fixture의 기존 열린 근무를 명시 준비하며 현재 근무 조회/메인 자동연결을 구현한 것으로 취급하지 않는다.

```powershell
dotnet run --project Tests/IdentityLogin.Ui.Tests -c Release
# 선택: 합성 화면 PNG
dotnet run --project Tests/IdentityLogin.Ui.Tests -c Release -- --render "<외부 출력 폴더>"
```

**통제한 HTTP/센서 대역 + 실제 C# 폼/ApiClient/pending/relay** 검사77항목이다. 기존 로그인 성공/OFF·첫 출근409만 전환·다른 오류 우회 금지·지문 원문·verified만으로 업무 열기 금지·finalize/auth/me 전 정상token 부재·정상 인수·응답 유실/5분 뒤 원 UUID 복구·변한 token/영수증·다른 challenge·늦은응답/다른접속·동일 token의 세대 구분·닫힌 창·출근 저장 중 닫기/회수 경합·명시revoke를 확인한다. 실제 서버/DB/하드웨어를 사용한 결과와 구분한다.

## 실제 서버·MySQL·Pi HTTP + C# 폼

Release 빌드 후 기존 `Tests/FingerprintIdentity.Server.Tests/run_check.py`에 `--identity-login-client "<이 프로젝트 bin/Release/net8.0-windows/IdentityLogin.Ui.Tests.dll>"`을 추가한다. 기본 runner의 고정 source export·명시 `--allow-disposable-mysql`, 검증한 MySQL binary·외부 보고 폴더가 필요하다. 실행 절차는 기존 서버 검사 README를 따른다. 새 runner는 DLL SHA256도 보고한다. 운영 venv/.env·실제 DB/서버·키·장치를 사용하지 않는다.

새3시나리오:

1. **matched:** 실제 LoginForm409→IdentityPending→장치 하나의 자동 시작→실제 지문 view→실제 Pi HTTP/서명·승인→서버 complete/finalize·MySQL→auth/me→업무 진입 콜백. 같은 날 재로그인에서 정상 경로를 사용하고 센서를 반복하지 않는다.
2. **unregistered:** 실제 Pi가 no_match를 서명하고 서버 rejected, 정상 token/업무 진입/근무 저장0.
3. **matched-loss:** 실제 finalize 응답을 C# handler가 소비 후 버린다. 정상 token을 먼저 설치하지 않고 같은 finalize UUID로 실제 서버의 암호화 결과를 다시 받아 로그인한다. 센서 검색/근무/영수증은 각각1개다.

기존10개와 함께 **13개 pytest 시나리오**이며 위77검사 항목과 합산하지 않는다. 최초 서버 조합은 alpha `5dcd66e`/서버 `95ef89d`였으며, 최신 결합은 upstream `dc525d0`을 받은 alpha `3c59f46` 위에서 이어간다. 실행별 source-manifest와 DLL hash로 정확한 소스를 구분한다. export 충돌0이다. 센서/UART·OLED·계정/키와 readiness는 시험용 대역이고 서버/DB·C#·HTTP는 실제 제품 코드다. `ready=True`를 운영 적용 승인으로 사용하지 않는다. Windows Job은 dotnet 생성 전부터 소유하고 모든 자식/소유 MySQL/임시 runtime을 종료·정리한다. 물리 센서·실제 회원의 출근 성공을 확인한 검사가 아니다.

최종 `3ec9d5a` + 원본 `dc525d0` / 최신 DLL 고정 실행: **13 passed in38.83s**, exit0·DLL 변경 없음·소유 MySQL/DB/runtime 정리True. 이 결과와 C#77항목/Release0오류0경고를 구분한다. 실제 공유 서버·장치에는 적용하지 않았다.
