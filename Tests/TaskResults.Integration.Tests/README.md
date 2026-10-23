# 2번 업무 화면 → 3번 결과 제출 통합 검증

기존 공통 인증·업무 조회/등록·수락/재작업과 3번 결과 제출/판단 API를 연결합니다. 제품은 `.NET 8 WinForms` 프로젝트 참조로 빌드하며, 특정 PowerShell Roslyn·참조팩·설치된 런타임 버전을 하드코딩하지 않습니다. 다른 담당 제품 코드를 테스트용으로 바꾸지 않습니다.

## 실제 HTTP·소유 MySQL

Windows, .NET SDK, 서버 requirements와 MySQL 8.0.16 이상이 필요합니다. 저장소 루트에서 실행합니다. 아래 Python 경로는 서버 의존성을 준비한 환경에 맞춥니다.

```powershell
dotnet build .\Tests\TaskResults.Integration.Tests\TaskResults.Integration.Tests.csproj -c Release
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskResults.Integration.Tests\run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin'
```

`--client-dll <절대 경로>`로 다른 빌드 출력을 지정할 수 있습니다. opt-in·Windows·바이너리 경로·의존성·DLL을 먼저 확인합니다. 기존 2번 `TaskQueries.Http.Tests`의 검증된 소유 MySQL 프로세스/DB/runtime·Windows Job·삭제 경로 검사를 재사용합니다. 기존 MySQL 서비스·Start-MySQL.cmd·외부 DB URL·사용자 .env·SQLite를 사용하지 않습니다. 자격증명·토큰·HTTP 본문은 출력하거나 저장소 파일에 쓰지 않고 자식 stdin과 격리된 실행 환경에서만 전달합니다.

fixture는 새 DB에서 공통 테이블과 **raw002·raw004·raw006 SQL**을 적용하고 전역 schema version은1로 유지합니다. 합성 계정과 3번의 명시적 `local-development` 부서/상위 검토자 CLI 설정만 준비합니다. **업무·배정·수락·submitted/returned 상태·제출본·판단·화면 이력을 DB에 seed하지 않습니다.** 연장 대기는 명시적으로 선택한 개발용 저장소에만 의존하며 최종 조직/연장 어댑터의 통합 완료를 뜻하지 않습니다.

- 관리자가 실제 등록 API로 직원 업무2개와 관리자 본인 업무1개를 만듭니다. 하나는 선택 혼동을 검출하는 변경하지 않을 업무입니다.
- 직원과 관리자 본인 각각 **등록→2번 실제 수락 폼→2번 결과 제출 버튼→3번 실제 제출 폼→현재 검토자 반려 API→2번 재작업 폼→2번 버튼 재제출→현재 검토자 승인 API**를 실행합니다. 관리자 본인 업무는 명시적 다른 상위 검토자가 판단합니다.
- 실제 선택 ID 전달, 진행중에서만 제출, 관리 업무의 제출 버튼 숨김, 취소 시 부모 목록 GET·제출 POST 없음, 성공 뒤 서버 재조회, 진행중 필터에서 제출 후 행 제거·필터 유지, 전체/5상태 결과를 검사합니다.
- 첫 회차는 실제 파일을 업로드하고, 재제출 뒤 이전 설명/첨부·최초 수락·배정 revision과1차/2차를 보존하는지 확인합니다. 2번이 별도 submitted 상태 요청을 보내지 않는지도 실제 요청 수로 대조합니다.
- 클라이언트 성공 표시와 독립적으로 SQL에서3개 업무·15개 receipt·4개 제출·4개 판단·8개 결과 이벤트, 버전7과 schema1·DDL 무변경을 확인합니다. 최초 accepted_at은 원래 수락 receipt의 서버 시각과 비교합니다.

정상·실패 모두 소유 HTTP/MySQL·DB·runtime 정리를 확인합니다. 아래는 실제 새 DB를 만든 뒤 의도적으로 실패하는 별도 경로이며 **exit1과 CLEANUP 안내**가 예상 결과입니다.

```powershell
.\RemoteWork.Server\.venv\Scripts\python.exe .\Tests\TaskResults.Integration.Tests\run_mysql_check.py --allow-disposable-mysql --mysql-bin-dir 'C:\Program Files\MySQL\MySQL Server 8.2\bin' --exercise-cleanup-failure
```

2026-10-20 직접 실행: **실제 C# HTTP·WinForms 100검사**, **독립 SQL 효과26검사** 통과. MySQL8.2·실제 loopback HTTP·raw002/004/006을 사용했고 정상 종료 후 소유 HTTP/MySQL·DB·runtime 정리를 확인했습니다. opt-in 누락의 시작 전 거절과 실제 새 DB 생성 후 의도적 실패의 **예상 exit1·정리 완료**도 각각 확인했습니다. 반복한 중간 실행을 새 검사 수에 합산하지 않습니다.

검증기 준비 중 발생한 runtime 첨부 폴더 누락과 두 번째 off-screen 호스트의 명시적 최초 조회 누락을 테스트에서 수정했습니다. DDL 비교는 MySQL이 빈 자동증가 테이블에서 생략하는 `AUTO_INCREMENT=<숫자>` **테이블 allocator 옵션만 제거**해 대조합니다. 열의 AUTO_INCREMENT 속성·타입·키·제약·그 밖의 옵션은 유지해 비교하며 이 준비 오류 때문에 제품 코드를 변경하지 않았습니다.

## 제어된 화면 수명 검사

```powershell
dotnet run --project .\Tests\TaskResults.Integration.Tests\TaskResults.Integration.Tests.csproj -c Release -- --lifetime-only
```

실제 TasksControl·SubmissionForm·ApiClient·WinForms 메시지 루프를 쓰되 HTTP와 자식 창의 최종 `DialogResult.OK`는 명시적 대역입니다. 취소, 접속 A→B 변경 후 늦은 자식 OK, 부모 폐기 후 늦은 자식 OK의3개 시나리오·26개 검사를 실행합니다. 이전 계정의 부모 GET·상태 POST·화면 오류가 추가되지 않는지 검사하며, 이 경로를 실제 제출 성공·서버/DB 시험으로 표시하지 않습니다. 실제 HTTP 검사 수치와 합산하지 않습니다.

2026-10-23 회귀: 현재 선택 상세의 업무 첨부 요약 조회인 `GET /api/v1/results/shared/tasks/1`에만 빈 목록 대역을 추가했습니다. 요청 순서·알 수 없는 요청 거부·취소·계정 변경·부모 폐기 검증을 유지한 원본 실행에서 **26검사·3시나리오 PASS**를 확인했습니다.

공유 DB·실계정·물리 두 PC·최종 조직/연장 모델·실장치·웹·수동 전체 UI 검수는 별도입니다. 이 검증은 3번 구현을 대신 작성하거나 업무 수정/재배정/연장 정책을 새로 확정하지 않습니다.
