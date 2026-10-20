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

실제 공유 DB, 물리 두 PC, 최종 조직/연장 모델, 웹 화면 검증은 미실행입니다. 같은 검사를 앞선 로컬 회차와 합산하지 않습니다.
