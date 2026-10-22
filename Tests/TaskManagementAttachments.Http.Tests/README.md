# 일반수정 첨부 실제 HTTP·MySQL 검증

2번의 `TaskManagementSessionStore`·수정 workflow·첨부 선택 모델에서 실제 공통 인증, HTTP, 업무 API, 3번 공개 첨부 provider까지 연결하는 통합 검증입니다. 제품 코드와 기존 검증 runner는 수정하지 않습니다.

## 실행

Windows, .NET 8 SDK, MySQL Server 8.x 바이너리와 `RemoteWork.Server/requirements.txt` 의존성이 설치된 Python이 필요합니다. 저장소 루트에서 실행합니다.

```powershell
dotnet build Tests/TaskManagementAttachments.Http.Tests/TaskManagementAttachments.Http.Tests.csproj -c Release -m:1
python Tests/TaskManagementAttachments.Http.Tests/run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir 'C:/Program Files/MySQL/MySQL Server 8.2/bin' --report-dir 'C:/outside-checkout/new-management-attachment-report'
```

`report-dir`는 저장소 바깥의 새 경로여야 합니다. 새로 소유한 MySQL 프로세스·DB·임시 디렉터리와 임의 포트의 loopback HTTP 서버를 사용합니다. 기존 `TaskCreationUploads.Tests` runner의 바이너리 확인, Windows Job, raw 002/004/006/009/013 DDL, 종료·정리 코드를 재사용합니다. 합성 계정만 seed하고 업무·파일·수정 데이터는 실제 API로 생성합니다. 사용자 설정, 기존 실행 서버, 공용 DB, SQLite, 운영 ACK는 사용하지 않습니다.

## 검증 범위

- 기존 첨부 공개 GET과 수정 context의 버전 확인 후 snapshot 연결. 변경 전 `attachment_ids` 생략, 기존 첨부 제외는 로컬 선택만 변경하며 DELETE 0회.
- 파일 준비만으로 HTTP를 보내지 않음. 명시 업로드의 `task_id`, 업로드 전 원본 변경과 무관한 캡처 바이트, 인코딩된 청크 본문 크기 확인.
- 실제 성공한 PATCH의 응답 한 건만 **클라이언트 테스트 핸들러에서 유실 처리**. 결과 불명 상태의 원래 요청·첨부 선택 보존, 직접 변경·업로드·삭제 차단, 동일 세션 workflow 재사용, 실제 receipt 조회로 복구. PATCH 재전송과 자동 DELETE 없이 row_version 한 번 증가.
- 현재 첨부 다운로드 바이트·SHA-256, 다른 업무가 참조하는 기존 파일 보존.
- 일반 필드 수정 시 `attachment_ids` 실제 JSON 생략과 기존 목록 유지. `[]`는 실제 전체 연결 해제. 동일 요청은 변경 없음으로 거부.
- 독립 DB 조회로 최종 행 수, 감사의 before/final IDs·변경 필드, 성공당 receipt 하나, 마지막 참조 해제 파일 정리, 업무 상태·배정 revision 유지 확인.

2026-10-22 실행: **C# 48검사 + DB 14검사 통과**, 빌드 경고 0·오류 0. raw DDL 적용 이후 물리 구조와 `schema_versions=[1]` 유지, HTTP·소유 MySQL·DB·파일·runtime 정리를 확인했습니다. `verification.json`에는 안전한 요약과 정리 결과만 남깁니다.

이 검증은 WinForms 컨트롤 직접 조작, 실제 사용자 서버 배포, 두 PC 연결, 운영 HTTPS·계정·권한 범위 전체 검증을 대신하지 않습니다. 응답 유실은 명시적인 클라이언트 결함 주입이며 실제 네트워크 장애를 재현했다는 뜻이 아닙니다.
