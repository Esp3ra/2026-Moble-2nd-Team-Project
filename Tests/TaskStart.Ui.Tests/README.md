# 수락·재작업 WinForms 연결 검사

```powershell
dotnet run --project .\Tests\TaskStart.Ui.Tests\TaskStart.Ui.Tests.csproj -c Release
```

실제 WinForms 메시지 루프와 명시 서비스 대역을 사용한다.20개 검사는 첫 대상 확인의 무전송·별도 실행, 불명확 응답 뒤 창 재진입/같은 요청 재송, 처리 중 일반 닫기, 로그인 교체/늦은 응답,403 안내, pending 선택 직후 취소 경합과 목록0건·필터 밖 복구, 성공 후 기존 필터 재조회/ack를 확인한다. 대역은 현재 서버 권한이나 DB 단일 효과를 증명하지 않는다.

실제 HTTP 서비스/계약은 [TaskStart.Tests](../TaskStart.Tests/README.md)의 제어 검사·명시적 실제 HTTP 모드로, 실제MySQL/동시성은 별도 소유 서버 검사로 나눈다. 수동 전체 GUI·다중DPI·3번 후속 제출/판단·공유 DB·두 PC는 별도 검증이다.
