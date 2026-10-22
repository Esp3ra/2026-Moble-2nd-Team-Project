# 현재 근무·정오 지문·활동 감시 연결 (5번)

최신 후속: PR71·72·73을 원본 `f542902`까지 수신했다. 실제 reader의 기본 projection과 실제 공통 퇴근 결과 소유권 연결은 [CLOCK_OUT_CONNECTION.md](CLOCK_OUT_CONNECTION.md)를 따른다. 아래 Draft71·PR72 전송 전 표시는 이전 단계의 이력이다. 기본 자동 연결/운영 OFF는 유지한다.

2026-10-22, 1번의 PR58 통합 회신과 Draft PR71 head `81eb88060d36767f5a50de74ea9cd40b2f7b52b5`를 기준으로 준비했다. MainForm이 현재 로그인·서버 근무 한 건에 활동 감시 서비스 하나와 정오 확인 작업을 소유한다. 기본 로그인/시작 경로에서는 설치하지 않는다. 원본 서버의 새 지문 OFF와 기존 로그인 동작은 유지한다.

## 연결과 소유권

`MainForm.ConnectWorkdayActivityAsync(readerFactory, options)`를 표시된 정상 로그인 화면의 UI 스레드에서 명시 호출한다. 실제 `AppServices.Api`의 캡처 Session을 factory에 넘기고 로그인 사용자를 대조한다. 화면의 회원 번호나 임의 UUID만으로 근무를 연결하지 않는다. 실제 열린 근무의 UUID·KST 날짜·서버 UTC 시각·정오 기록이 필요하다.

1번 reader는 아직 Draft다. 제품에 복사하거나 별도 endpoint를 만들지 않았다. PR71 병합·검토와 실제 장치 설정 준비 후 다음 형태로 조립할 수 있다. 이 예제는 기본 시작 코드에 설치되어 있지 않다.

```csharp
await main.ConnectWorkdayActivityAsync(session =>
{
    var reader = new WorkdayStateConnection(session); // PR71 병합 후 사용
    return async cancellation =>
    {
        var s = await reader.ReadAsync(cancellation);
        return new WorkdayActivityState(s.WorkSessionId, s.WorkDate,
            s.ServerTime, s.NoonAt, s.NoonDue);
    };
}, new WorkdayActivityOptions(registeredDeviceId, trustedPiAddress, cameraOptions));
```

메뉴 이동과 빌린 활동 상태 창 닫기는 monitor를 종료하지 않는다. 근태 페이지에도 같은 monitor를 넘긴다. 로그인 종료·만료·다른 근무/날짜로 교체될 때 소유자가 정리한다. 정상 로그아웃은 기존 동작 전에 monitor의 종료를 기다린다. 취소에 협조하지 않는 reader의 반환 자체를 무한 대기하지 않고 늦은 반환을 세대 검사로 버린다. 동시 조회는 같은 진행 Task를 사용한다.

Designer의 `WorkdayActivityBar`는 정오 지문 확인·활동 상태·근무 다시 확인을 제공한다. 현재 근무를 읽지 못하면 감시를 중단하고 확인 필요로 표시한다. 같은 근무 재확인 후 재개하며 다른 근무를 기존 monitor에 자동 인수하지 않는다. 활동 상태를 출퇴근 기록으로 저장하는 API/DB는 추가하지 않았다.

## 정오 동작

기본 15초마다 서버 근무를 조회한다. 서버 시각과 `NoonDue`가 기준이며 PC 시계로 근무를 만들지 않는다. 정오 전을 관찰한 같은 근무가 미확인 정오 상태가 되면 한 번 안내한다. 다른 modal 창 때문에 소유자가 비활성 상태면 다음 확인까지 미룬다. 처음 연결할 때 이미 정오를 지났다면 상태/수동 확인 버튼만 제공한다. 늦은 출근·미확인 보충·유예·강제 로그아웃 정책은 새로 정하지 않았다.

같은 정오 operation을 창 닫기·결과 재확인에도 보관한다. finalize UUID/결과를 바꾸거나 응답 유실 뒤 재스캔하지 않는다. 서버에서 이미 정오 기록이 보이더라도 보관된 결과불명 작업의 명시 재확인은 허용한다. 저장 시도 전 확정 취소/거절/만료된 작업만 사용자가 다시 확인할 때 새로 시작할 수 있다. 폼 생성만으로 결과불명 finalize를 자동 재전송하지 않는다.

## 남은 공통 연결과 실제 적용

**퇴근 저장 중 일반 요청/401/정상 토큰 종료와 결과 전용 복구의 공통 수명은 1번 담당이며 아직 연결하지 않았다.** 이 변경의 정상 Logout은 필수 지문 퇴근 완료가 아니다. 공통 ApiClient의 401 처리를 무시하거나 옛 토큰을 되살리지 않는다.

공유 서버 옵션/DDL/키, Linux 단일 UART/OLED와 재시작 복구, 실제 카메라/라파·두 PC, 지문 인증 기록 보관/탈퇴 정리와 연차 실제 서버 연결은 별도다. 이번에 실제 장치·서비스·공유 DB·키를 변경하지 않았다. 독립 시험의 열린 근무·회원·키·readiness·UART·monitor 대역을 실제 출근/카메라 성공으로 기록하지 않는다.

검사 절차와 범위는 `Tests/WorkdayActivity.Tests/README.md`를 따른다. 협업 기준은 [1번 기존 통합 회신](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309878130), [5번 기존 요청](https://github.com/Esp3ra/2026-Moble-2nd-Team-Project/pull/58#issuecomment-6309485288)이다. 이번 결과의 원격 회신과 새 코드 PR은 사용자 전송 승인 뒤 별도로 진행한다.

## 2026-10-22 직접 검증 기록

팀장 alpha `57a9b08c35c0c3a5b6aae6249fa97074211500d1`를 로컬 alpha에 병합한 결합 상태다. 아래 수치를 서로 합산해 실물 성공으로 표시하지 않는다.

| 검사 | 결과와 범위 |
|---|---|
| `dotnet run --project Tests/WorkdayActivity.Tests -c Release` | 26항목 통과. 실제 폼/MainForm·명시 대역. Draft reader를 외부 링크한 별도 실행도 26항목 통과 |
| 기존 `Tests/IdentityLogin.Ui.Tests` | 179항목 통과. 실제 폼/ApiClient·HTTP/장치 대역 |
| 기존 `Tests/ActivityPresence.Tests` | 297항목/12시나리오 통과. 실제 Python stdio·loopback·합성 영상/장치 |
| 기존 `Tests/Leave.Ui.Tests` | 89항목 통과. 실제 폼·합성 workflow |
| 소유 MySQL/서버·Pi HTTP/실제 C# | 12통과/36.02초. 기존10개와 정오 정상/응답유실2개. server/Pi export alpha `6aa8816` + PR71 고정 head `81eb880`, 시험 DLL SHA `f8d23d3e14420a14d26e760ab184062ee27ab0c8171625e85e1c9d22f79df065` 전후 동일. 소유 MySQL·DB·프로세스/runtime 정리 완료 |
| Release 솔루션 / diff | 경고0·오류0 / 통과 |

실패 이력도 보존했다. 카메라 회귀 첫 실행은 저장소 밖 산출물 경로에서 기존 Python 탐색이 실패했으며, 소스 변경 없이 저장소 안의 별도 무시된 bin 폴더로 실행해 통과했다. 정오 통합 첫 세 번의 시도는 모델리스 폼 Shown 이전의 완료 Task를 읽은 시험 조립 문제로 각10통과/2실패였다. 시험에서 Shown을 기다리고, 검증 실패를 제품의 예상 예외 처리에 삼키지 않게 구분하며 stderr UTF-8 처리를 보완한 뒤 위12개가 통과했다. 앞선 소유 MySQL/runtime도 각각 정리 완료다. 제품의 인증/서버 계약을 완화하거나 실제 환경을 바꿔 통과시키지 않았다.
