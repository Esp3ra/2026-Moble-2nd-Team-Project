# 협업 요청의 업무·직원 목록 연결 검사

`dotnet run --project Tests/CollaborationDirectory.Tests/CollaborationDirectory.Tests.csproj -c Release`

Windows/net8-windows. 실제 WinForms 메시지 루프와 명시 HTTP/데이터 대역 **6시나리오/25검사**.
기존 own/in_progress·참여자 GET 경로, 로그인 고정, 상태·취소 필터/본인 제외/동명이인,
직원404에서도 업무 유지와 재조회 복구, 반대쪽 실패/빈 목록, 늦은 오류·로그아웃·종료를 확인한다.
요청 보내기는 미연결이며 이 변경에서 저장·수락·알림/DB를 추가하지 않는다.

이번 실제 서버 읽기는 별도다: 직원 로그인200, 본인 진행중 업무200/1건,
참여자 GET404/HTTP_404(요청번호8d6befb3ea12e4a3), 점검 접속 로그아웃200.
직원 실제 조회 성공/수동 GUI·물리 두PC는 미실행이다. 실제 서버 준비는1번 협의 대상이다.
