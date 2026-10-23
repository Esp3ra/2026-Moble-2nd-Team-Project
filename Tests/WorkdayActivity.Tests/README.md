# MainForm 정오·퇴근·활동 감시 연결 검사

2026-10-23 일반 WinForms EXE 연결 후 **170항목** 통과. 기본 LoginForm이 사용하는 공통
WorkspaceStartup→실제 MainForm 표시→기존 WorkspaceConnector/HTTP reader 경로를 검사한다.
설정 누락·손상·다른 서버에서 일반 로그인과 메뉴·지속 안내 유지, 표시 전 연결0회/표시 후1회,
서버404/503에도 지문 바 표시·지문 확인/퇴근 차단·monitor 시작0회, 최소 창에서 버튼 배치,
같은 계정 재로그인·설정 읽기 중 접속 교체·늦은 결과를 확인한다. 호환 실행기 명령 검사는 유지한다.
제품과 검사 호스트는 같은 DPI 모드이며 설정 파일·HTTP·계정·근무·monitor는 소유 시험 대역이다.
실제 장비·운영 계정·서버 기능 활성화는 이 검사에 포함하지 않는다. 기존 로그인 UI179항목도 통과했다.

2026-10-23 오후 업무 전 정책 반영 후 **124항목** 통과. 서버 KST11:00 이상12:00 미만에
한 번 안내하며 최초 시간 내 로그인, 10:59:59/11:00/11:59:59/12:00 경계, 시간 밖 무기록을
완료로 표시하지 않음, 다른PC 완료, Designer 평상시 조회 버튼 제거, 오류 전용 재연결 표시/
성공 후 숨김,12시 이후에도 같은 finalize UUID/한 번의 센서 결과로 원 저장 결과 복구를 확인한다.
로그인·퇴근·기존 monitor 수명 검사도 유지한다. 실제 DB·키·센서는 이 C# 기본 검사에서 명시 대역이다.
아래109/95/81/51 수치는 이전 검사 이력이다.

추가 실제 C#→공통 reader/Flask/Pi HTTP→소유 MySQL 검사는 **6통과/17.58초**다.
11시 진입 안내 정상/응답유실2개와 오후 업무 전·퇴근 폼 정상/응답유실4개를 실행했다.
UART/OLED·키·회원·monitor는 명시 대역이며 운영 서버·장비·두PC는 검증하지 않았다.
DLL SHA 전후 동일과 소유 DB/MySQL/runtime 정리를 확인했다. 시험 DLL SHA:
WorkdayActivity `bd815017b188e389909ede8d8db646f4eba233efe953cffc6bb734e985f522ae`,
IdentityLogin `cb5bae34640705be4f5c910677077cfae49b863b85c2ee37330d3878e4756e01`.

2026-10-23 퇴근 후 일반 접속 연결을 포함한 최신 검사는 **109항목**(기존95 포함)이다.
정확한 `409 ATTENDANCE_DAY_CLOSED`에서 정상 로그인·일반 메뉴를 유지하면서 감시를
시작하지 않거나 기존 감시를 종료한다. 동일 로그인에서 오래된 열린 근무로 재시작하지
않으며 다른409는 구분한다. 실제 MainForm→공통 WorkdayStateConnection→loopback HTTP
409 경로와 출퇴근·지문 요청 미발생을 함께 검사한다. 운영 서버/DB·센서는 명시 대역이다.

2026-10-23 R80-01 수정 후 기본 검사는 **95항목**(이전81 포함)이다. 실제 파일 Load와
Main이 사용하는 Run 명령 처리부에 소유 임시 경로만 공급해 연차 전용/Pi 설정 누락·손상,
근무 요청 시 누락·손상·다른 서버 거절, 정상 동시 설정을 검증한다. 명령 검사는
`--check-config` 경로이며 실계정 로그인/OS 프로세스 GUI/운영 설정은 사용하지 않는다.
임시 폴더는 종료 때 정리한다. 초기 명령 처리 회귀에서 필요한 Pi 설정 실패가
InvalidDataException으로 처리되지 않는 문제도 발견해 기존 안내/exit1 경계에 추가했다.

이전81은 `Tools/ConnectedWorkspace` 명시 조립·설정 및 아래 기존51을 포함한다.
실제 MainForm에서 연차/근무 각각1회 시도·한쪽 실패 격리·정책OFF 차단·동일 workflow/monitor
대여·로그인 종료를 확인한다. 서버/센서 대역 범위는 유지하며 실제 운영활성화 성공이 아니다.

```powershell
dotnet run --project Tests/WorkdayActivity.Tests -c Release
```

**51항목**(이전 정오/감시26 포함)이다. 실제 C# 메시지 루프·폼·캡처 Session·MainForm·원본에 병합된 WorkdayStateConnection을 사용한다. monitor/device/common 및 HTTP 데이터는 명시 대역이다. 실제 운영 로그인/장비/DB는 기본 검사에서 사용하지 않는다.

- 시간 전→11:00~12:00 한 번 안내, 시간 내 최초 연결, 같은 operation 유지.
- 상태 창 닫기/메뉴 이동·중복 연결 거절·조회 실패와 재개·근무 교체·동시 조회.
- 같은 계정 재로그인 뒤 늦은 결과, 백그라운드 만료, 정상 종료의 monitor drain.
- 퇴근 verified 뒤 UI 스레드 준비·감지/진행 poll 대기·정오/조회 차단.
- 응답 유실과 팝업 재오픈에도 원 common owner/UUID·본문 유지, 재스캔/ordinary finalize 금지.
- 잘못된 공통 식별값 거부, 준비 실패 뒤 일반업무 자동 재개 금지, 확인된 종료/monitor1회 정리.

PR71 reader는 이제 제품에 병합됐으므로 이전 외부 Member1WorkdayClientSource 링크 옵션은 제거했다. 최초26/12개 검증 이력은 Attendance의 WORKDAY_ACTIVITY.md에 보존한다.

## 실제 병합된 공통 접점·서버/Pi HTTP·소유 MySQL

PR73은 원본 f542902에 병합됐다. 이제 별도 Draft/ClockOutPeer 옵션 없이 현재 tracked 소스를 외부에 export하여 실제 기본 제품 연결을 검사한다.

```powershell
python Tests/WorkdayActivity.Tests/prepare_peer.py --alpha-ref <own-commit> `
  --peer-ref upstream/alpha --output <새-외부-폴더>
dotnet build <외부폴더>/Tests/WorkdayActivity.Tests -c Release
```

충돌 시 한쪽을 자동 선택하지 않고 중단한다. tracked 소스만 export하고 .env/symlink 등을 거절한다. 기존 Draft 외부 결합 설명은 초기 검증 이력이며 현재 제품의 공통 접점을 대신하는 shim은 사용하지 않는다.

기존 Tests/FingerprintIdentity.Server.Tests/run_check.py에 --workday-activity-client <고정 WorkdayActivity.Tests.dll>와 --workday-clockout-client <같은 DLL>을 함께 주면 **14개**(기존10+정오2+퇴근2)를 실행한다. runner의 --allow-disposable-mysql, 검증된 --mysql-bin-dir, 위 export의 --source-root, 외부 --report-directory를 명시한다. 원본 운영 .env/DB나 실제 장비를 사용하지 않는다. 자식/DB/runtime 소유·정리와 DLL SHA 전후 대조는 기존 실행기 책임이다.

실제 C# MainForm/reader/폼/common/relay → 실제 서버·Pi HTTP → 소유 MySQL을 연결한다. UART/OLED·키/회원/readiness·이미 열린 근무·monitor는 시험 대역이다. 정오2개는 시험 전용 시각 전진 hook, 퇴근2개는 commit 뒤 첫 성공 응답을 버리는 after_request를 사용한다. 제품 endpoint·시간 설정·실제 서비스는 바꾸지 않는다.

퇴근 정상/응답 유실 각각에서 센서 검색1회·공통 준비1회, 퇴근 receipt1개/ended, 해당 정상 AuthSession0, auth/logout 전송0을 확인한다. 응답 유실 후 일반 auth/me가 네트워크 전에 차단되고 같은 원 결과는 복구되는 것도 검사한다. 실제 카메라/센서·두PC·Linux 준비/운영 활성화 시험은 별도다.

최종 직접14PASS41.70초/정리 완료와 정확 source/DLL SHA는 Attendance의 CLOCK_OUT_CONNECTION.md에 기록했다. 실제 제품의 기본 overload가 공통 factory를 설치하는 경로를 사용한다. 공통35/MainForm11 검사와 기존 로그인179/연차115는 별도 수치다.
