# MainForm 정오·활동 감시 연결 검사

최신 후속은 **51항목**(이전26 포함)이다. 원본 PR71/72 병합 후 실제 WorkdayStateConnection을 직접 참조한다. 이전 외부 `Member1WorkdayClientSource` 옵션은 제거했다. 다음의 고정 PR71 외부 링크 설명은 최초 검증 이력이며 지금은 필요하지 않다.

```powershell
dotnet run --project Tests/WorkdayActivity.Tests -c Release
```

실제 C# 메시지 루프·폼·캡처 Session·MainForm과 명시 monitor/device/HTTP 대역을 사용한다. 정오 전→정오의 한 번 안내, 늦은 연결의 수동 확인, 동일 operation 보존, 상태 창 닫기/메뉴 이동, 중복 연결 거절, 조회 실패와 재개, 근무 교체, 동시 조회, 같은 계정 재로그인 뒤 늦은 결과, 백그라운드 만료, 종료 시 monitor drain을 검사한다. 활동 감지 알고리즘과 물리 센서는 기존 별도 검사 대상이다.

PR71의 고정 head `81eb88060d36767f5a50de74ea9cd40b2f7b52b5`에서 `WorkdayStateConnection.cs`를 외부 TEMP에 원본 바이트 그대로 내보내면, 아래 옵션으로 **시험 어셈블리에만** 실제 reader를 연결할 수 있다. Draft 파일을 제품이나 alpha에 복사하지 않는다.

```powershell
dotnet run --project Tests/WorkdayActivity.Tests -c Release `
  -p:Member1WorkdayClientSource=C:\reviewed-test-export\WorkdayStateConnection.cs
```

`Loopback.cs`/`test_real_server.py`는 이 옵션으로 빌드한 DLL을 기존 소유 MySQL 실행기의 `--workday-activity-client <WorkdayActivity.Tests.dll>`로 호출한다. `prepare_source.py`로 현재 alpha와 고정 PR71을 결합한 별도 export가 필요하다. 충돌은 기존 검토된 startup guard 두 곳만 허용하며 작업 폴더/.env/공유 DB를 사용하지 않는다. 기존 실행기가 시험 MySQL과 자식/HTTP/runtime을 소유·정리하고 DLL SHA를 전후 대조한다.

실제 서버/라파 HTTP·MySQL과 실제 C# reader/정오 폼/relay를 연결해 정상 정오 저장과 첫 finalize 응답 유실 뒤 원 결과 복구 두 경우를 검사한다. 시험 전용 열린 근무·서버 시각 전진 hook·합성 회원/키/readiness/UART/monitor를 사용한다. 각 경우 검색1회, 근무/정오 영수증1개, challenge1개, approval2개와 monitor 시작/종료1회를 대조한다. 새 운영 endpoint나 시간 설정은 추가하지 않는다.

이는 실물 카메라·OLED·지문이나 공유 서버 배포, 퇴근 시 일반 요청/401 종료 연결을 검증하지 않는다. 기본 시작 경로가 이 Draft 연결을 켜지 않는 경계도 유지한다.

## 퇴근 소비 후속

신규25항목은 verified 뒤 실제 UI 스레드 준비/stop 대기, 정오·poll 차단, 동일 operation/공통 owner 재사용, 응답 유실 뒤 재스캔/ordinary finalize 금지, 잘못된 공통 식별값 거부, 확인된 종료와 monitor1회 정리를 검사한다. 기본 검사의 common/device는 대역이다.

PR73 고정 `b442b9b20cc21c18f29f1073c2d2b3abeed6904b`와 현재 커밋을 `prepare_peer.py --alpha-ref <own> --peer-ref <fixed73> --output <새 외부폴더>`로 결합 export한 뒤, 그 폴더에서 `dotnet build Tests/WorkdayActivity.Tests -c Release -p:ClockOutPeer=true`로 빌드한다. 충돌이 있으면 자동 선택하지 않고 중단한다. tracked 소스만 export하며 .env/symlink 등은 거절한다. 현재 작업 폴더나 원본 alpha에 Draft를 병합하지 않는다.

기존 `Tests/FingerprintIdentity.Server.Tests/run_check.py`의 `--workday-clockout-client <고정 WorkdayActivity.Tests.dll>` 선택 인자는 `test_clockout_real_server.py`의 정상/성공 응답 유실2개를 추가한다. `--workday-activity-client`도 함께 주면 정오2개를 함께 검사한다. 실제 C# MainForm/폼/reader/common/relay → 실제 서버·Pi HTTP → 소유 MySQL을 연결한다. UART·회원/키/readiness·monitor는 시험 대역이다. 각 경우 센서 검색1회/공통 준비1회, 퇴근 receipt1개/ended, 정상 AuthSession0, auth/logout 전송0을 대조한다. 서버의 시험 전용 after_request가 commit 후 첫 성공 응답을 버리며 제품 endpoint/기능을 추가하지 않는다. runner는 자식/DB/runtime을 소유·정리하고 DLL SHA를 전후 확인한다.
