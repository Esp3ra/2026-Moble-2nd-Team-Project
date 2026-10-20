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
