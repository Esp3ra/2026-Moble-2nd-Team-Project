# PR70 실제 HTTP 소비 검증

1번 제공 구현2a50535/병합cca5b62, 기준alpha7135a29를 소비한다. SQLite를 사용하지 않는다.

```powershell
dotnet run --project Tests/DeadlineConnection.Tests/DeadlineConnection.Tests.csproj -c Release
python Tests/DeadlineConnection.Tests/run_binding_mysql_check.py
```

첫 명령은 실제 소스의 명시 HTTP 대역75검사다. BIGINT 문자열·signed INTEGER JSON정수,
정규 UUID/원 target, 최소 영수증4필드, 새 배정과 같은UUID 확인 분리, 다른업무 UUID별칭
차단·원키/원본문 재시도·미기록/만료/권한/미준비·성공 후 목록 실패를 대조한다.

두 번째는 새 소유 MySQL8.2 RR에서 **원본1번 HTTP21검사**와 별도 새DB의
**4번 통합4시나리오**를 각각 실행한다. SQL002/004/006/009·실제journal/3열PK확인을
fixture가 일회성DB에 준비한다. 테스트별 새 Flask TCP와 실제 C#source·공통CapturedSource·
scope/panel, 정상/저장 후 응답 유실/성공 뒤 GET 실패/실제MainForm 자동조립·메뉴수명·
확인·재로그인을 검사한다. task와기한기록 호출은fixture이며2번제품hook검사가 아니다.
4개 안의8/9/9/5개 C#assert를 별도의MySQL검사 수로 합산하지 않는다.

기존 마감 UI166·공통 MainForm 탐색24검사도 통과했다. Release경고0/오류0.
최초4시나리오의 DLL fixture경로 오류는 기대 repo 경로를 고치고 정리/재실행했다.
실패이력은 외부실행기 보고서에 보존하며 서버/계약을 바꿔 숨기지 않았다.

공유서버/실사용DB·운영옵션·DDL적용/backup복원·baseline·실제2번 writer hook·
물리두PC/장시간부하는미실행이다. 이전합성콜백/2열PK 실행기는 현재 소비의 검증이 아니다.


## 배포 서버 읽기 최종 확인

`--shared-read`는 환경 변수 `DEADLINE_TEST_URL`, `DEADLINE_SHARED_LOGIN`, `DEADLINE_SHARED_PASSWORD`를 메모리로 받아 실제 서버를 조회한다. 원 source → 기존 캡처 wrapper/scope → 실제 panel, 원 binding, 재조회, 로그아웃 무효화, 재로그인을 확인한다. 업무 확인/키 발급/receipt 쓰기0이다. 계정/비밀번호를 문서나 로그에 남기지 않는다. **`--live`/`--main`은 확인 저장을 실행하므로 실제 사용자 서버에 테스트 용도로 실행하지 않는다.** 이 모드는 소유 MySQL fixture 전용이다.


## 승인된 새 시연 업무의 실제 확인 검증 — 2026-10-23

`--shared-demo`는 사용자가 명시 지정한 **새 시연 업무ID**(`DEADLINE_DEMO_TASK_ID`) 하나만 실제 확인한다. 같은 비공개 로그인 환경변수를 사용하며 실제 MainForm/source/scope/panel→업무 화면의 정확한 ID 탐색→확인 저장→별도 로그인 세션과 재로그인의 DB 반영을 검사한다. 기존 다른 알림의 확인 상태가 유지됐는지도 대조한다. 원 MainForm은 최소화/비표시 taskbar로 동작해 다른 기존 알림을 임의 확인하지 않는다. 일반 `--live`/`--main` fixture 모드와 구분한다. 이 모드를 실제 기존 사용자 업무에 재사용하지 않는다.

10/23 사용자 승인 신규 task5에서11 PASS. 확인 POST200/request_id `10250a21945d37ac`, 원 업무 GET200/request_id `1ec814fc80af460a`. 실제 서버는1번 보고0628902이며 API의SHA 직접표시는 없다. 요청번호만 남기는 loopback pass-through가 실제 서버로 전달했고 응답 대역/DB fixture는 아니다. 두 독립 세션을 같은PC에서 실행했으므로 두 물리PC 검증은 별도다. 종료 때 취소된 구 화면의 HTTP 연결1건에서 proxy write ConnectionAborted 로그가 있었으나 전체검사는exit0이며 원 확인 저장을 다시 실행하지 않았다.
