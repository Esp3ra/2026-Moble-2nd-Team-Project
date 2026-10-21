# 3번 결과물 HTTP·MySQL·WinForms 검증

사용자 DB와 분리한 새 소유 MySQL 프로세스/임시 DB만 생성하고 종료 시 소유를 재확인하여 정리합니다. 실제 직원·업무·제출 자료를 제품 DB에 자동 입력하지 않습니다.

Windows, Python 서버 requirements, MySQL 8.0.16 이상, PowerShell 7 및 .NET 8 참조팩이 필요합니다. C# 검사는 로컬 검증 환경의 8.0.29 참조팩과 PowerShell에 포함된 Roslyn을 사용합니다. 설치 경로가 다르면 검증 스크립트의 참조팩/런타임 경로를 해당 설치와 맞춰야 합니다. 제품 csproj를 변경하지 않습니다.

저장소 루트에서:

```powershell
$env:PYTHON_DOTENV_DISABLED='1'
$env:RESULTS_MYSQLD='C:/Program Files/MySQL/MySQL Server 8.2/bin/mysqld.exe'
python Tests/Results.Http.Tests/verify_cold.py
python Tests/Results.Http.Tests/verify_mysql.py --client
dotnet build RemoteWork.sln -c Release --nologo
```

`verify_mysql.py`는 기존 2번의 소유 MySQL 실행 도구를 읽기 전용으로 재사용합니다. 자격증명은 실행 중 무작위 생성하고 출력하지 않습니다. `verify_server.py` 단독 실행은 별도 SQLite 검증이며 실제 MySQL 검증을 대신하지 않습니다.

이번 alpha 결합본 직접 실행: MySQL 185개, C# 41개 통과. 기본 off 새 프로세스의 경로/모델 미등록 확인. Release 경고 0·오류 0. 로컬 빌드는 설치된 참조팩을 선택하는 외부 검증 설정을 사용했습니다.

실제 공유 DB, 물리 두 PC, 최종 조직/연장 모델 검증은 미실행입니다. 같은 검사를 앞선 로컬 회차와 합산하지 않습니다.

## 2026-10-20 웹 후속 결합 검증

기준 alpha `43f69c99632b1a084e0bc1c441f46d7f1a8eb26d`를 보존한 후속 게시 준비본에서 다음을 직접 실행했습니다.

```powershell
$env:PYTHON_DOTENV_DISABLED='1'
python Tests/Results.Http.Tests/verify_web.py
python Tests/Results.Http.Tests/verify_cold.py
node --check RemoteWork.Server/remotework/static/results.js
```

`verify_web.py`는 같은 소유 MySQL 실행 도구를 사용하여 기존 서버185개, 실제 C# HTTP/WinForms41개, 쿠키/CSRF 웹177개를 확인했습니다. 현재 C# 소스114개 컴파일 오류0·경고0이며 전체 솔루션 Release 빌드와 구분합니다. 초기 웹149개나 이전185/41 회차를 중복 합산하지 않습니다. 사용자 DB·원본 폴더·실제 계정/업무/파일을 변경하지 않고 소유 테스트 HTTP/MySQL/임시 자료를 정리했습니다.

웹 검사는 현재 접속 범위·CSRF·같은 계정 재로그인·직원/관리자 본인/상위 검토자·실제 제출/판단/과거6차·파일 바이트와빈 파일·마감/개발용 연장gate·명시적2번 재작업·응답 유실receipt·100건 이후 이력cursor를 포함합니다. 기존 일반 API 형식/동작도 같은 실행으로 확인했습니다.

`--preview`는 격리된 TEST 자료로만 loopback 브라우저 점검 서버를 열고 종료 후 정리합니다. 앞선 웹 구현 회차에서 직접 브라우저 제출/다운로드 해시 비교·반려/빈 승인 의견·재작업/2차·과거 회차·이력 필터/상세·응답 유실 결과 확인을 실행했습니다. 그 브라우저 조작은 최신alpha 결합 후 전부 재실행한 결과로 표시하지 않습니다. 이번 후속 결합에서는 위 자동 MySQL/실제HTTP 검사를 재실행했습니다.

최신2번 취소/연장 모델의 실제3번 소비·경합/배포확인, 직급 자동 선정, 최종/공유DB·물리두PC·실제장비·운영배포는 미실행/후속입니다.2번 자체 대역 검증을3번 통합 검증으로 쓰지 않으며 실제소비 확인gate를 설치/해제하지 않습니다.

## 2026-10-21 후속 검사

`verify_client.ps1`은 dotnet 실행 위치에서 설치된 .NET8 참조팩과 실행 PowerShell의 같은 major WindowsDesktop runtime을 자동 탐색합니다. 특정 패치 버전8.0.29/10.0.9를 강제하지 않습니다. 필요한 계열이 없으면 설치 필요를 명시하며 제품 csproj를 바꾸지 않습니다.

```powershell
node Tests/Results.Http.Tests/verify_ui.js
python Tests/Results.Http.Tests/verify_lifecycle.py
python Tests/Results.Http.Tests/verify_web.py
python Tests/Results.Http.Tests/verify_cold.py
```

직접 결과: JS handler 대역52, 실제 MySQL/loopback 취소·연장·첨부258, 서버185, C#HTTP41/115소스 컴파일0오류0경고, 최종 웹183통과. 세부 범위와 이전177/183·반복185 중복 합산 금지는 docs/RESULTS.md의2026-10-21절을 따릅니다. JS대역52를 실제브라우저/DB 검사로 표시하지 않습니다. 실패fixture 보완 뒤 최종 결과이며 초기SQL주석/응답envelope/ACK선행조건을 잘못 읽은 테스트 실행은 통과 수치에 포함하지 않습니다. 제품/공유 환경의 lifecycle ACK는 설치하지 않았습니다.


## 2026-10-21 공용 첨부 제공부

`python -B Tests/Results.Http.Tests/verify_shared.py`: 실제 소유 MySQL과 실제 loopback HTTP134검사 통과. 테스트용 업무 소비 route는 2번 제품 코드가 아닌 명시 fixture이며, 실제 업무 등록/교체/취소 화면 연결 완료로 표시하지 않습니다. 실제 SQL 초안3테이블을 전용 DB에만 적용했고 사용자 DB/계정/파일은 변경하지 않았습니다.

범위: 현재 역할·다른 업무 공유 권한, A 삭제 후 B/과거 회차 보존, 신규 업무+첨부 같은TX와 rollback, 중복 제출receipt, 첫/두번째 회차·최신순, 등록 취소·마지막참조·만료정리·삭제실패재시도, 파일크기/개수/이름/해시, 취소 중 업로드/완료 차단·과거 제출 보존. 공유 DB 스키마/2번 소비 연결/두PC/실제 장비는 미검증입니다.

이번 전송함수 재사용 변경 뒤 기존 서버191·웹183·C#41/115소스0오류0경고·취소연장258검사도 통과했습니다. 마지막 권한 조건 보완 뒤 서버191·웹183을 재실행했고 C# 소스는 바꾸지 않았습니다.134 이전의91/112/126·반복191 등을 합산하지 않습니다. 초기 소비fixture 경로/내부 정수ID 보완 과정의 실패 실행은 통과 수에 포함하지 않습니다.

### R52-03 현재 참조 회귀

같은 `python -B Tests/Results.Http.Tests/verify_shared.py`에 실제 인증 snapshot 이후/guard 이전에 요청을 멈추는 테스트 전용 스케줄러를 추가했다. 별도 원본 HTTP 요청이 B 연결 또는 같은 업무의 첨부 교체를 commit한 뒤 앞 요청을 재개한다. 일반 조회가 옛 snapshot을 유지함과 실제 helper의 현재 읽기·다운로드·rollback·최종 정리를 검사한다. 미연결 snapshot 이후 다른 요청에서 연결된 업로드를 삭제하지 못하는 검사도 포함한다.

이번 직접 결과는239개(추가105, 기존134)이며 수정 전 원본은 새 B 참조의 ready 상태 검사에서 실패했다. 실행 명령·범위는 위와 같으며 상세 수정·다른 직접 검사 결과 및 미검증 항목은 [공용 첨부 계약의 R52-03 기록](../../docs/SHARED_ATTACHMENTS.md#r52-03-수정검증--2026-10-21)에 있다. 대역 UI/소유 DB 검증을 실제2번 제품 화면·공유 DB 검증으로 표시하지 않는다.
