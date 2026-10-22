# 연차 입력·목록·검토 UI 회귀

```powershell
dotnet run --project Tests/Leave.Ui.Tests -c Release
```

실제 WinForms 메시지 루프와 합성 로그인/내부 ILeaveWorkflow 대역을 사용한다. 5개 신청 유형과 병가·공가 차감 제외 안내, 미연결 시 저장 완료 금지, 기간 검사, 중복 클릭/응답 유실의 요청 고정, 다른 신청의 영수증 거부, 지정 직원의 관리자 검토 제한, 접속 종료 뒤 늦은 결과를 확인한다. 실제 HTTP 휴가 계약·DB 정책을 만든 시험이 아니며 실제 회원/서버/DB/장치를 사용하지 않는다.

출력 폴더를 인자로 넘기면 신청·검토 폼 PNG를 생성한다. 폼 컨트롤은 제품의 Designer.cs/resx에서 관리한다. 서버 adapter의 담당 범위 권한·실제 차감·동시성과 API 협의는 별도다.
