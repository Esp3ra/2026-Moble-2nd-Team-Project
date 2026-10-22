# 5번 퇴근 지문 → 공통 결과 복구 연결

2026-10-22 기준 원본 `7135a29`에 PR71/72가 병합됐다. 실제 `WorkdayStateConnection`을 명시 MainForm 연결의 기본 projection으로 소비한다. 기본 로그인/시작은 연결을 설치하지 않으며 새 지문 OFF를 유지한다.

1번 PR73은 공통 drain/결과 전용 수명을 제공하지만 검토 시점 Draft다. 처음 `b442b9b`를 소비했고 원본7692ce4 수신 후 최신 고정 head `5070911612d038d9e9395a52eaa469cc6623d14f`에서도 다시 결합 검증했다. 두 head의 C#/지문 서버/Pi 관련 파일 차이는0이다. 이번 제품은 그 Draft의 ApiClient/MainForm 코드를 복사하지 않고, 원 식별값/최소 delegate를 주입하는 `WorkdayClockOutConnection`으로 소비한다. 새 endpoint/DB/키/전역401 처리는 추가하지 않는다.

## 정확한 조립 (PR73 검토·반영 후)

```csharp
await main.ConnectWorkdayActivityAsync(options, prepareClockOut: async (request, stop, ct) =>
{
    var common = await main.PrepareClockOutFinalizationAsync(
        request.WorkSessionId, request.WorkDate, request.ChallengeId,
        request.OriginalFinalizeId, stop, ct);
    return new WorkdayClockOutConnection(
        new(common.WorkSessionId, common.WorkDate, common.ChallengeId, common.OriginalFinalizeId),
        () => common.IsCurrent,
        common.CompleteAsync,
        () => main.FinishConfirmedClockOut(common));
});
```

옵션의 장치 UUID/Pi 주소/카메라 범위는 실제 승인 설정을 사용한다. 정상 캡처 Session과 실제 서버 근무를 검사한 뒤 monitor 하나를 소유한다. callback 미설치 시 퇴근 버튼은 비활성이며 스캔하지 않는다. 예제를 기본 시작에 자동 설치하거나 시험 readiness/키를 운영에 쓰지 않는다.

## 흐름과 실패 경계

1. 같은 로그인/근무의 퇴근 operation과 장치를 호스트가 소유한다. 현재 서버 근무 재확인 → 기존 지문 relay의 verified/pending/clock_out과 request 근무/날짜 대조 후에만 factory를 호출한다. UI 준비 접점은 캡처한 UI 스레드로 보낸다.
2. factory는 공통 Prepare를 호출하고 전달된 stop을 사용한다. stop은 근무 poll을 차단·timer 중단하고 monitor와 진행 중 근무 조회를 기다린다. 호스트/정상 Session 자체는 종료하지 않으므로 원 결과만 복구할 수 있다. 준비 이후 정오·근무 재조회·감시 재시작은 차단한다.
3. 반환된 원 근무/날짜/challenge/finalize UUID와 현재 여부를 대조하고 그 연결의 Complete만 호출한다. 주입된 경로에서 기존 Session.PostAsync finalize로 우회하지 않는다. 고정 요청·결과는 일반/저장 응답 유실과 팝업 재오픈에도 보관하며 재스캔하지 않는다.
4. 사용자가 폼의 서버 저장 완료를 확인하고 창을 닫은 뒤 같은 공통 소유자의 Finish를 호출한다. Finish가 확정 영수증·현재 로그인과 MainForm 소유권을 재검사하고 종료한다. auth/logout 추가 전송은 없다. 빌린 폼/feature는 공통 소유자를 Dispose하지 않는다.
5. 준비·저장 불명은 같은 operation을 유지하고 일반업무 자동 재개/새 퇴근 작업으로 바꾸지 않는다. 실제 401·명시 로그아웃·다른 로그인/서버 변경은 공통 소유권을 따른다. 취소·재시작·회수 정책을 우회하지 않는다.

기존 독립 WorkdayOperation의 직접 finalize 경로는 하위 API 회귀용으로 유지된다. **MainForm에서의 퇴근은 위 주입된 공통 소유자 경로를 사용해야 한다.** 일반 Logout/창 닫기를 지문 퇴근으로 표시하지 않는다.

## 검사·실제 적용 구분

`Tests/WorkdayActivity.Tests`는 실제 폼/캡처 접속과 명시 common/device/monitor 대역을 연결한다. 기본 모드에서 현재 병합된 reader도 실제 사용한다. PR73의 실제 common/MainForm 접점은 `prepare_peer.py`로 별도 Git 결합 export를 만들고 `-p:ClockOutPeer=true`로 시험 빌드한 뒤 같은 runner의 선택 HTTP/MySQL 검사를 실행한다. Draft를 작업 alpha에 병합하지 않는다.

실제 공유5000/37테이블·옵션/키, 라파의 서비스/지문·매핑, 기본 시작은 이번 변경으로 바꾸지 않는다. 현재 개발·시연은 같은 내부망의 사설IP+PORT 직접 접속이며 Tailscale/VPN/터널·외부 공용 HTTPS는 선행 조건이 아니다. 실제 운영키/6테이블/서버 실행기·Linux 준비/재시작 대조·두PC 실물은 별도이며, 코드·대역 검사 통과를 운영 승인으로 기록하지 않는다.

## 직접 실행 결과 (2026-10-22)

- 자체/최신 결합 `WorkdayActivity.Tests`: 각각 **51항목 PASS**. 제품 MainForm·폼·병합된 reader, common/monitor/device HTTP 대역.
- PR73 실제 소스 결합 `ClockOutRecovery.Tests`: **35항목/12시나리오 PASS**; `ClockOutOwner.Tests`: **11항목 PASS**. 첫 결합에서 직접 실행했고 최신507과 해당 C# 소스 차이0을 대조했다.
- 기존 `IdentityLogin.Ui.Tests`: **179항목 PASS**, `Leave.Ui.Tests`: **115항목 PASS**. 서로/대역 검사와 합산하지 않는다.
- 실제 C# MainForm/reader/폼/common/relay → 실제 서버·Pi HTTP/소유 MySQL8.2: **14 passed in41.97s / exit0**. 기존10+정오2+새 공통 퇴근 소비2. source alpha `e0192a1022671fd46416e017cc01f14bcb2c1b50` + PR73 `5070911`, 결합 tree `80dc01c5832a308ec6fa459ac26666196828b86a`. DLL SHA `dcedd6ba43ba3c9a9334b20ba11ae833552ff33c249e787a89a6da1cb5fa4c7d` 전후 동일. MySQL/DB·소유 자식·HTTP/runtime 정리 완료다.
- 그 전 결합 f024bc6+b442b9b도 **14PASS42.12초**였으나 최신 결과에 합산하지 않는다. UART/OLED·키/회원·열린 근무/readiness·monitor는 fixture이며 실물/두PC가 아니다. 새 테스트 endpoint/실제 환경을 만들지 않았다.
- 솔루션과 각 결합 Release 경고0·오류0, diff check 통과.

서버·클라이언트의 구현 제공과 실제 운용 준비를 나눈다. 1번 서버 백업/리허설·6테이블·실제 loader/분리 키 공급, 5번 Linux 비공개 설정·단일 UART/OLED·재시작 불명 작업 대조 및 실물 공동 검증은 아직 미완료다. 기존 완료 회원/등록 지문을 재등록·삭제하지 않는다. 인증 기록1년/탈퇴 센서 삭제의 운영 정리도 이번에 구현하지 않았다. 협업 상태는 기존 PR58 대표 댓글에 취합하고 날짜 LOG는 팀장 취합 대상이다.
