# 정오·퇴근 지문 확인 (5번)

후속 2026-10-22: 현재 근무 reader 주입·MainForm 소유 monitor·정오 안내 연결은 [WORKDAY_ACTIVITY.md](WORKDAY_ACTIVITY.md)에 추가했다. 아래 공통 접점 미제공 설명은 당시 이력이며, 현재 PR71은 Draft로 시험에만 연결했다. 퇴근 중 일반 요청/401/결과 복구 공통 수명은 계속 1번 후속이다.

2026-10-22 PR69 병합·1번 기술 수용 이후, 원본 alpha5594f2a 기준의 후속이다. `FingerprintWorkdayOperation`과 Designer/resx `FingerprintWorkdayForm`을 추가했다. 1번의 기존 정상 Bearer용 `noon_check`/`clock_out` challenge·완료·finalize를 사용하며 서버/DDL/공통 ApiClient/MainForm/실제 Pi는 변경하지 않는다.

## 이번 구현

- 실제 캡처 `ApiClient.Session`, 서버가 확인한 근무 UUID/날짜와 등록 장치를 받는다. 임의 근무 ID를 만들어 연결하면 안 된다. 실제 challenge의 근무·날짜·목적을 **센서 호출 전에** 비교하고 finalize 전에도 다시 대조한다. 요청 원문은 복사해서 읽으며 MAC 검증은 기존 서버/Pi 책임이다.
- 기존 attempt/relay로 지문 확인 후, `verified/pending`와 목적·challenge가 맞을 때만 finalize한다. verified만으로 정오/퇴근 저장 성공을 표시하지 않는다.
- finalize UUID/challenge를 I/O 전에 고정한다. 응답 유실 뒤 같은 객체의 `CompleteAsync`와 같은 결과 확인 버튼은 원 finalize만 재전송하며 지문을 재호출하지 않는다. 현재 근무/날짜와 일치하는 정상 영수증만 완료로 표시한다. 엉뚱한 토큰·빈 영수증/다른 근무/날짜를 거절한다.
- 폼은 operation을 **빌린다**. 정상 종료 뒤에도 로그인/근무 소유자가 같은 operation을 보관해야 한다. 창을 다시 열 때 원 객체를 넘겨 결과를 복구한다. 저장 중 닫기는 응답/시간초과까지 제한하고, 저장 불명 뒤 첫 닫기에서 안내한다. 저장 요청 후 cancel로 기록을 되돌리지 않는다. Dispose는 로컬 정리이며 원격 취소/퇴근/로그아웃이 아니다.
- 접속 교체/취소 뒤 늦은 결과는 완료로 인수하지 않는다. 정상 token을 설치·삭제하거나 다른 로그인·근무를 종료하지 않는다.

## 실제 자동 연결 전 필요한 1번 접점

`LoginResult`와 `auth/me`의 현재 공개 DTO는 근무 UUID·근무 날짜·정오 확인 여부를 제공하지 않는다. 최초 출근 finalize에는 근무 정보가 있지만, 당일 비밀번호 재접속/앱 재시작/다른 PC까지 로컬 기억으로 대체할 수 없다. **현재 접속에 대해 현재 열린 근무/근무 날짜/정오 기록 상태를 읽는 권위 있는 서버 접점**을 1번과 맞춰야 한다. 새 endpoint/필드/DDL을 이번 단계에서 임의로 만들지 않았다.

퇴근 finalize는 서버의 정상 AuthSession을 삭제한다. 저장 응답 유실 뒤 같은 캡처 Bearer의 결과전용 복구는 이미 제공된다. 다만 그 사이 MainForm의 주기 `auth/me`/다른 요청이401을 받아 공통 접속을 폐기하면 기존 캡처 Session도 끝난다. **퇴근 저장/결과 확인 중 공통 화면의 요청·로그아웃·만료·활동 감시 수명 조립**을 1번과 대조해야 한다. 공통401 무시·일반 인증 우회·다른 세대에 옛 token 재설치를 추가하지 않았다. 이번 폼을 기본 MainForm에 붙여 운영 완성으로 표시하지 않는다.

후속 소유자는 서버에서 열린 근무를 확인한 뒤 같은 로그인당 operation/활동 monitor를 소유하고, 정오12시(KST) 대상을 계산·동일 확인 작업을 재사용하며, 실제 퇴근 영수증 뒤 monitor 정리/정상 접속 종료·화면 종료를 조립해야 한다. 자정·늦은 출근·이미 정오 확인·다른PC 퇴근·결과불명·취소·재로그인도 검증한다. 이 자동 일정/메인 수명 연결은 이번 단계에 포함하지 않는다.

## 검사와 운영 구분

`dotnet run --project Tests/IdentityLogin.Ui.Tests -c Release`는 기존 로그인77항목과 정오/퇴근을 함께 실행한다. Workday.cs는 실제 C# 폼/attempt/relay/ApiClient와 구조용 HTTP/센서 대역이며 MAC 인증·물리 센서를 검증하지 않는다. 정상 저장, 재스캔 없는 원 UUID 복구·재오픈, 위조/다른 근무 영수증 거절, 센서 전 근무 불일치, 새 로그인 뒤 늦은 결과, 저장 중 닫기, 저장 후 취소 차단을 확인한다.

기존 실제 서버 검사 runner의 `--identity-login-client`를 사용하면 추가4개(정오/퇴근 × 정상/응답유실)를 실행한다. 소유 MySQL에 이전 출근이 확정된 열린 근무를 명시 fixture로 준비하고, 실제 정상 로그인→실제 C# 폼/relay→실제 Pi HTTP→서버 MAC/승인→실제 MySQL finalize를 확인한다. 이전 출근 fixture는 운영 조회 API가 구현됐다는 뜻이 아니다. UART/OLED·키·회원/readiness는 시험용이고 실제 공유 DB·장비는 사용하지 않는다.

Linux 실물 loader/비공개 프로필·readiness/재시작 대조는 운영 자료 미수신으로 미실행이다. 실제 새 지문 기능 OFF 유지, 기존 등록·센서 템플릿/회원 매핑 보존, 원본 직접 push·자가 병합 없음. 1번 협업 회신 초안에는 현재 근무 reader/퇴근 수명 경계를 함께 묶어 전달할 준비를 한다. 날짜LOG는 팀장 취합용이며 팀원은 직접 게시하지 않는다.

## 2026-10-22 실행 결과

제품/검사 소스 `a02bd5a` + 원본 `5594f2a`(source export 충돌0), C# 검사 DLL SHA256 `4bb3469cfd69abffbd9d5456ac1bf5b5007a645198834b6a92c0998d73097e0c` 고정. Windows/.NET10 SDK·net8-windows/Python3.13·MySQL8.2에서 C# **179검사 PASS**, 전체 Release **오류0/경고0**, 실제 서버/Pi HTTP·MySQL/C# **17 passed in51.00s / exit0**, DLL 실행중 변경 없음·소유 MySQL/DB/runtime 정리true. 최초17PASS/50.84초 실행의 UTC 생성함수 폐기예정 경고4개는 검사 fixture에서 명시 UTC 생성으로 바꾼 뒤 위 최종 실행에서 없어졌다. 제품 시간 규칙/서버 시계는 바꾸지 않았다.

정오/퇴근 폼을 실제 WinForms 메시지 루프에서 표시하고 PNG로 저장·시각 대조했다. 테스트 화면/회원·가짜 UART이며 실제 계정/장비의 정오/퇴근 성공을 의미하지 않는다. 현재 근무 reader·MainForm 자동일정/종료·활동monitor 연동/실물2PC는 아직 미구현·협의 대상이다. 기존 PR69의 병합과 이번 후속 변경의 리뷰/병합 상태를 구분한다.
