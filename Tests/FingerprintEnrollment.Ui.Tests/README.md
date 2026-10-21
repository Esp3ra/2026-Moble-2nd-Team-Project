# 5번 지문 등록 전용 WinForms 검사

저장소 루트/Windows/.NET8 Desktop 환경에서 실행한다.

```text
dotnet run --project Tests/FingerprintEnrollment.Ui.Tests/FingerprintEnrollment.Ui.Tests.csproj -c Release
```

실제 WinForms 메시지 루프·UI 스레드 검사와 통제 HTTP/Pi 대역으로15검증을 수행한다. 생성된 직원ID 표시, 중복 시작/장치 변경 차단, 서버 completed 뒤 완료 표시, 중복 지문 안내·자동 재채취 차단, 창/대상 종료 뒤 늦은 결과 차단, 로그아웃 때 창 종료를 확인한다. 검사용 폼은 작업 표시줄에 노출하지 않고 투명하게 실행한다.

실제 관리자 화면 버튼/1번 `CreatedUserEnrollmentTarget` 주입, 실물 센서/라파·공유 서버/MySQL을 검증한 것은 아니다. 폼의 연결 접점은 근태 README와 Pi 등록 문서를 따른다.

2026-10-21 복구 UI 후속은 운영자 복구 모드/ID 확인→현재 생성 회원 확인→별도 시작 클릭을 검사한다. 조회 중에는 새 발급/센서 요청이 없고, 잘못된 장치·로그아웃 뒤 늦은 회신은 차단하며 새 job/claim과 서버 완료를 확인한다. 최종 수치는 기술 문서 후속 기록을 따른다. 이는 실제 새 서버·운영자CLI·센서 등록 검증이 아니다.

복구 후속 최종 실행:31 checks PASS(기존15 포함, 합산하지 않음).

## 확정 거절과 복구 전용 진입 — 2026-10-21

최종 **58 checks PASS**. 원래31검사에 ordinary issue의 정확409/ENROLLMENT_NO_FREE_CLAIM만 시도 해제하고 같은 회원의 명시 복구 선택으로 이어가는 검사를 추가했다. 다른409·같은code의400·503·응답유실/by-request503·발급 후 physical API 오류·Pi 전달 뒤 API 오류·복구 reissue 오류는 attempt를 유지하고 자동 추가 발급/scan/거짓 완료를 막는다. 이전31/5번 미게시68 보고와 합산하지 않는다.

1번이 주입하는 requiredRecoveryId 모드는 UserManagement.Ui.Tests의95검사에서 실제 owner/launcher/폼으로 확인한다. 이 검사들은 통제 HTTP/Pi이며 실제 서버 job 생성·MySQL 매핑·센서 성공 근거가 아니다.
