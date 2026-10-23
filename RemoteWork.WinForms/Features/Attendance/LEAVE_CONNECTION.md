# 5번 연차 화면 연결

`MainForm.ConnectLeaveWorkflowAsync(factory)`는 표시된 실제 로그인에서 한 번만 호출한다. 같은 captured Session과 workflow를 가진 `LoginLeaveConnection`을 반환해야 한다. MainForm이 이를 소유하고, 출퇴근·연차 메뉴 재진입 시 동일 workflow를 빌려준다. 페이지 종료는 adapter의 원 요청 캐시나 로그인·활동 monitor를 종료하지 않는다. 기본 실행/운영 정책 활성화는 별도다.

1번 PR79의 `HttpLeaveWorkflow`는 아직 Draft이므로 이 변경에 복사하거나 alpha에 병합하지 않는다. 해당 adapter가 포함된 검토용 결합 소스에서 아래 구성을 사용한다. 별도 HTTP DTO·endpoint·DB·재시도 로직을 만들지 않는다.

```csharp
await main.ConnectLeaveWorkflowAsync(session =>
{
    var adapter = new HttpLeaveWorkflow(session);
    return new LoginLeaveConnection(session, adapter, async ct =>
    {
        var r = await adapter.ReadinessAsync(ct);
        return new(r.SchemaReady, r.WriteReady, r.Pending, r.Year, r.ServerTime);
    }, async (year, ct) =>
    {
        var b = await adapter.BalanceAsync(year, ct);
        return new(b.Year, b.GrantedUnits, b.ReservedUnits, b.UsedUnits, b.RemainingUnits);
    });
});
```

연차 조회는 설정·잔여일과 목록을 함께 조회한다. 서버의 준비 응답 연도를 사용하고, 0.5일 단위를 일수로 표시한다. 설정 오류/잔여일 실패는 이전 숫자나 임의 0일로 표시하지 않는다. 준비되지 않은 상태에서는 새 신청 버튼을 잠근다. 관리자 자신의 개인 연차/담당자 미지정과 배정 직원 검토 권한은 별개다. 관리자 검토는 해당 서버 행의 `CanReview`와 최신 로그인으로 판단하며 최종 서버 검증을 대체하지 않는다.

신청/검토 폼은 기존 원 UUID·본문·선택 버전을 보존하고, 결과 미확인 시 같은 작업만 명시 재확인한다. 동일 adapter가 PR79의 intent→W1 결과 조회를 수행한다. 창을 두 번 명시 닫거나 앱을 종료한 뒤 폼의 RAM 식별자가 자동 복원되는 기능은 없다. 이 경우 새 신청 전에 목록을 확인해야 한다. 연차 취소·승인 취소 계약은 추가하지 않았다.

병가·공가의 연차 차감 제외는 사용자 요구다. 나머지 연차 정책/휴일 원본/개인 부여·검토 담당자는 팀장 결정과 1번의 비공개 설정이 필요하다. 이 코드는 정책 승인, 실제 계정 생성·배정, 운영 write 설정 또는 실제 장비 검증을 뜻하지 않는다. MainForm의 수정은 5번 메뉴의 연결 소비에 한정한다.

검증 명령과 범위는 [연차 UI 검사](../../../Tests/Leave.Ui.Tests/README.md)에 기록한다. 관련 협업은 [PR58 대표 댓글](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309485288)에서 관리한다. 팀장 전용 날짜 LOG나 별도 브랜치 문서는 코드 PR에 섞지 않는다.

## 2026-10-22 직접 검증

- Windows/.NET 8 Release 빌드: 오류 0, 경고 0. 실제 Designer 화면의 PNG도 확인했다.
- 연차 UI: 144항목 통과. 합성 신원/metadata/workflow로 기존115 + 새 연결/로그아웃 경합29를 검사했다.
- 기존 WorkdayActivity 51항목, LeavePreview 11항목 통과. 이들은 합성 장치/메모리 기반이며 실물 검사와 다르다.
- 실제 MainForm/폼·PR79 adapter·Flask API/W1·소유 MySQL8.2: 4시나리오 통과, 13.21초. 정상 승인, 신청/승인 응답 유실, 신청/반려 응답 유실, 정책 OFF. POST는 각 작업당 한 번, 원 intent 결과 복구, 단일 신청·최종 revision2·예약 해제/사용량을 SQL로 확인했다. 소유 MySQL/DB/runtime 정리 완료.
- 결합 기준: 5번 `5cb7ab7d7d26ca0d671cc1b614d7c25628795d3f` + PR79 `88f78fa8364a2e50129c20ef3e5ea14739c96cbc`, tree `7c07653f6063bfc336f991a21397563f37807167`. 결합 DLL SHA256 `5c7ac8badcf0296016acffacfdc8cdf762d79abef0e5a4d9877e28f925fa3675`.
- 미실행: 팀 운영 서버/실제 계정/실물 Pi·지문·카메라, 두 PC 동시 접속, 실제 D1/D2 설정. Draft PR79는 alpha에 미병합이며 제품 기본 시작에서 연차 adapter를 자동 설치하지 않는다. 검토용 명시 연결만 검사했다.
