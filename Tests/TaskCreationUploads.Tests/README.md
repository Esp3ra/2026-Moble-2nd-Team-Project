# 업무 등록용 임시 첨부 업로드 검증

2번 담당 `HttpTaskCreationUploadService` / `TaskCreationUploadAttempt`가 3번의 기존 공용 첨부 API를 소비하는 경계를 검사합니다. 제출·결과물 API나 다른 담당자의 구현을 수정하지 않습니다.

```powershell
dotnet run --project Tests/TaskCreationUploads.Tests/TaskCreationUploads.Tests.csproj -c Release
```

기본 실행은 **명시적인 HTTP 테스트 대역**입니다. 실제 공통 `ApiClient`와 캡처된 로그인 세션을 사용하고, 전송 핸들러에서 요청·응답만 통제합니다. 실제 서버·DB·스토리지에 접속하지 않습니다.

2026-10-22 위 명령으로 빌드 및 실행을 확인했습니다. 결과는 **14개 시나리오 그룹, 344개 검사 통과**입니다. 새 프로젝트와 솔루션 Release 빌드 모두 경고0·오류0입니다.

- 선택한 파일의 불변 사본·SHA256·파일명 준비, 준비 단계 HTTP 0회, 정확히25MiB 준비 허용·25MiB 초과 차단, 빈 파일.
- 시작·16KiB 청크·완료 요청의 경로·정확한 JSON 필드·실제 바이트·Bearer, W1 키와 DELETE 미사용.
- `FB EF BE` 반복 데이터로 base64 `+`가 많은 두 개 이상의 청크를 실제 공통 직렬화에 통과시켜 JSON 요청 본문이 32KiB 미만인지 확인.
- UUID·파일명·크기·해시 불일치, 잘못된 타입·중복·누락·추가 필드·깨진 JSON, 잘못된 청크 확인 위치 거부.
- 시작 응답 유실 시 신규 업로드 재생성 금지, 청크·완료 응답 유실 시 같은 UUID와 같은 요청으로 명시적으로 재시도.
- 동시 클릭·완료 후 재클릭·취소·폐기·401·403·로그아웃·서버 변경·이전 세션의 지연 성공/오류 차단.

## 실제 HTTP·MySQL 실행

```powershell
dotnet build Tests/TaskCreationUploads.Tests/TaskCreationUploads.Tests.csproj -c Release
& <프로젝트 Python 실행파일> Tests/TaskCreationUploads.Tests/run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir 'C:/Program Files/MySQL/MySQL Server 8.2/bin' --report-dir <checkout 밖의 새 보고서 폴더>
```

`--http`는 runner가 새 소유 MySQL/loopback HTTP·합성 계정·외부 파일 저장소를 준비한 뒤 비공개 stdin으로 설정을 전달하는 별도 실행 경로입니다. 기존 소유권·Windows Job·정리 helper를 재사용하며 기존 서비스·DB·.env·SQLite·운영 ACK를 사용하지 않습니다. raw002/004/006/009/013은 해당 새 DB에만 적용합니다.

2026-10-22 **C# HTTP26검사·독립 DB9검사 통과**를 확인했습니다. 준비→실제16KiB 청크→완료→첨부2개 업무 등록→같은 키 재응답/성공 결과 조회→담당 직원 목록·다운로드 저장 바이트를 확인했습니다. 빈 파일과 원본 변경 뒤의 불변 바이트도 포함합니다. 실제 성공 응답 뒤 청크·완료·시작 응답을 클라이언트에서 각각 유실시키는 명시 전송 대역을 사용하며 서버·제공 함수·파일·DB는 실제 코드입니다.

비담당 직원 목록403·직원 업무 없는 업로드403을 확인했습니다. 별도 관리자는 검토자 접점을 설치하지 않은 fixture에서503 `RESULTS_CONTEXT_MODE_REQUIRED`로 차단됩니다. 이를 검토자 정책/운영 권한 검증 완료로 확대하지 않습니다. 최종 업무1/배정1/관리자1/receipt1·파일3/청크3/참조2, 생성 감사1·초기1/1·schema1·물리 DDL 불변과 HTTP/MySQL/임시 DB·파일 정리를 확인했습니다. 응답을 잃은 시작1건은 미연결 uploading으로 남아 fixture 정리 때 제거됩니다.

초기 harness의 객체 참조 비교와 별도 관리자503 오류명 기대값을 실제 계약에 맞게 고친 뒤 전체를 재실행했습니다. 제품 수정으로 다른 담당자의 오류 코드를 바꾸지 않았습니다. 공유 서버/실계정·두 PC, WinForms 선택/삭제 UI, 공통 DELETE, 일반수정 첨부 교체,25MiB 전체 전송은 실행하지 않았습니다.
