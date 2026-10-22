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
