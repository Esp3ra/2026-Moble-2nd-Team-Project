# 팀원 1 구현본 실행 안내

팀장 저장소 alpha의 최신 업무 조회 준비 코드를 보존하고 공통 서버와 실제 로그인을 연결했습니다. 가장 먼저 데모로 실행을 확인하고, 팀 공동 개발은 MySQL 설정으로 진행하세요. 데모와 MySQL은 서로 다른 저장소를 사용합니다.

## 1 가장 빨리 실행하기

1. 공통 코드 PR의 소스 브랜치를 내려받아 프로젝트 루트에서 시작합니다. GitHub에는 실행 바이너리를 포함하지 않습니다.
2. `Start-Demo.cmd`를 더블클릭합니다. 첫 실행은 인터넷으로 서버 패키지를 설치합니다. Python 3.11 이상이 필요하며, 이 PC의 Codex에 포함된 Python도 자동 탐색합니다.
3. 서버 창에 표시되는 `admin1`, `staff1`, `staff2` 계정과 **각각의 무작위 비밀번호를 기록**하세요. 이미 생성된 계정은 비밀번호를 바꾸지 않습니다.
4. 서버 창을 열어 둔 채 `Start-Desktop.cmd`를 더블클릭하고 로그인합니다. 소스 실행에는 Windows와 .NET 8 SDK가 필요합니다. `Start-Desktop.cmd`는 빌드된 Desktop 실행본이 없으면 `dotnet run`을 실행합니다.
5. 웹은 `http://127.0.0.1:5000/login`에서 같은 계정으로 로그인합니다.

데모는 이 PC에서만 접속하는 SQLite 모드입니다. 저장 파일은 `RemoteWork.Server/instance/demo.db`이며 프로그램 재실행 후에도 사용자 정보가 보존됩니다. 비밀번호를 잊었다면 아래 CLI 명령으로 다른 관리자 계정을 만든 뒤 사용자 관리에서 초기화하세요. **기존 DB를 지워서 초기화하지 마세요.**

PowerShell에서 실행할 수도 있습니다.

```powershell
.\Start-Server.ps1 -Demo
# Python 위치를 직접 지정해야 한다면:
.\Start-Server.ps1 -Demo -Python 'C:\설치경로\python.exe'
```

서버 종료는 서버 창에서 Ctrl+C입니다. 서버가 꺼지면 데스크톱은 연결 끊김을 표시하고 재확인을 시도합니다.

## 2 팀에서 사용할 MySQL 설정

서버를 실행할 PC 한 대를 정합니다. 모든 데스크톱과 웹은 이 서버에 접속합니다. 클라이언트 PC는 MySQL에 직접 접속하지 않습니다.

MySQL 관리자 도구에서 **새 전용 DB와 전용 계정**을 만드세요. 아래 비밀번호 예시는 실제 값으로 바꿉니다. 기존 프로젝트 DB가 있다면 이름과 권한을 확인하고 백업한 뒤 적용하세요.

```sql
CREATE DATABASE remote_work CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'remote_work'@'127.0.0.1' IDENTIFIED BY '여기를_별도의_긴_DB비밀번호로_교체';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, INDEX, ALTER, REFERENCES
ON remote_work.* TO 'remote_work'@'127.0.0.1';
```

1. `RemoteWork.Server/.env.example`을 같은 폴더의 `.env`로 복사합니다.
2. `MYSQL_*` 항목에 실제 값을 입력합니다. 별도 항목을 쓰면 비밀번호의 `@`, `#`, `:` 등을 URL로 변환할 필요가 없습니다. `.env`에서 `#` 등이 포함된 값은 따옴표로 감싸세요.
3. `SECRET_KEY`를 다음 명령으로 생성한 값으로 바꿉니다. 서버 PC 한 대의 키를 재시작할 때도 유지하세요. `.env`는 팀 공유 압축이나 Git에 올리지 않습니다.

```powershell
python -c "import secrets; print(secrets.token_hex(32))"
```

4. 프로젝트 루트에서 처음 한 번 실행합니다.

```powershell
.\Start-Server.ps1 -Initialize
```

공통 테이블이 생성됩니다. 기존 테이블이나 데이터를 삭제하지 않습니다. `init-db`는 버전 1의 최초 생성용이며, 이후 테이블 구조 변경을 자동으로 이관하는 기능은 아닙니다.

5. 서버가 실행된 상태에서 별도 터미널을 열어 최초 관리자 계정을 만듭니다. 비밀번호는 화면에 표시되지 않는 입력창에서 두 번 입력합니다.

```powershell
cd .\RemoteWork.Server
.\.venv\Scripts\python.exe -m flask --app remotework create-user --login-id admin1 --display-name 관리자 --employee-no ADM-001 --role admin
```

6. 이후에는 프로젝트 루트에서 `.\Start-Server.ps1`로 실행합니다. 관리자 로그인 후 하단 **사용자 관리**에서 직원 계정과 추가 관리자 계정을 생성·수정할 수 있습니다.

## 3 다른 PC와 연결

서버의 `.env`에서 `HOST=0.0.0.0`으로 설정하고 서버를 재시작합니다. 클라이언트의 **서버 연결 설정**에 `http://서버PC의실제IP:5000`을 입력하고 **연결 확인 → 저장**을 누릅니다. 브라우저도 같은 주소 뒤에 `/login`을 붙입니다. `0.0.0.0`은 접속 주소로 입력하지 않습니다.

Windows 방화벽은 필요한 사설 네트워크와 포트만 허용합니다. MySQL은 서버와 같은 PC라면 `127.0.0.1`에서만 사용하면 됩니다. 인터넷이나 신뢰하지 않는 네트워크에서 사용하려면 HTTPS를 제공하는 역방향 프록시를 앞에 두고 클라이언트 주소를 `https://...`, 서버 `COOKIE_SECURE=true`로 설정하세요. 로그인 비밀번호와 접속 증표는 HTTP에서는 암호화되지 않습니다.

클라이언트 서버 주소는 `%LOCALAPPDATA%\RemoteWork\client.json`에 저장하며 비밀번호와 접속 증표는 저장하지 않습니다. 환경변수 `REMOTEWORK_API_URL`이 있으면 저장된 주소보다 먼저 적용됩니다.

## 4 계정과 접속 동작

- 아이디는 영문·숫자·점·밑줄·하이픈 3~50자이며 소문자로 통일합니다. 비밀번호는 10~128자입니다.
- 관리자와 직원은 동일한 로그인 창을 사용합니다. **관리자 여부는 서버 DB의 역할**로 결정합니다.
- 기본 접속 시간은 8시간입니다. `.env`의 `SESSION_HOURS`로 1~168시간 사이에서 설정할 수 있습니다.
- 로그아웃 시 현재 접속 증표를 폐기합니다. 비밀번호 변경·초기화, 역할 변경, 계정 비활성화는 해당 사용자의 모든 접속을 폐기합니다.
- 로그인 실패는 DB에 기록합니다. 같은 계정 5회 또는 같은 IP 50회 실패가 15분 안에 누적되면 잠시 차단합니다. 만료되면 다시 시도할 수 있습니다.
- 메인 화면은 30초마다 사용자 정보와 접속을 재확인합니다. 기능 요청 중 받은 401도 재로그인을 안내합니다.
- 로그아웃 시 서버가 꺼져 있으면 이 PC의 접속 증표를 지우고, 서버 접속 종료를 확인하지 못했다는 안내를 표시합니다. 서버 접속은 원래 만료 시각까지 남을 수 있습니다.
- 지문 로그인은 장치 담당 기능 연결 대기 상태입니다. 인증 없이 입장시키는 우회 기능은 없습니다.

## 5 점검과 문제 해결

```powershell
cd .\RemoteWork.Server
.\.venv\Scripts\python.exe -m pip install -r requirements-dev.txt
.\.venv\Scripts\python.exe -m pytest -q
```

기본 테스트는 임시 SQLite DB를 사용합니다. MySQL 테스트를 하려면 **삭제 가능한 별도 DB**를 만들고 `MYSQL_TEST_URL`로 전달합니다. 테스트는 DB 이름이 `remote_work_test_`로 시작하는지 확인한 뒤 그 안의 사용자·접속·이력을 비웁니다. 팀 DB 주소를 넣으면 안 됩니다.

```powershell
$env:MYSQL_TEST_URL='mysql+pymysql://전용테스트계정:비밀번호@127.0.0.1:3306/remote_work_test_team1?charset=utf8mb4'
.\.venv\Scripts\python.exe -m pytest -q
Remove-Item Env:\MYSQL_TEST_URL
```

| 증상 | 확인할 내용 |
|---|---|
| Python을 찾지 못함 | Python 3.11 이상 설치 또는 `-Python`으로 실행 파일 지정 |
| 서버 연결 실패 | 서버 창이 켜져 있는지, 주소·포트·방화벽 확인 |
| DB 연결 또는 초기화 오류 | `.env`의 계정·비밀번호·DB 이름, MySQL 실행, `-Initialize` 여부 확인 |
| SECRET_KEY 오류 | 예시 문구를 남기지 않고 무작위 32자 이상 입력 |
| 웹 로그인 후 다시 로그인 화면 | HTTP 데모에서는 `COOKIE_SECURE=false`; HTTPS 서비스에서는 `true` |
| 로그인이 잠시 차단됨 | 최근 실패로부터 15분 지난 뒤 재시도 |
| exe 실행 시 .NET 안내 | .NET 8 Desktop Runtime 설치; 개발 시 .NET SDK와 Windows Forms 개발 환경 사용 |

만료된 접속과 하루 이상 지난 로그인 실패 기록은 다음 명령으로 정리할 수 있습니다. 사용자와 감사 이력은 삭제하지 않습니다.

```powershell
.\.venv\Scripts\python.exe -m flask --app remotework cleanup-auth
```

실행용 파일은 저장소에 포함하지 않습니다. 필요하면 `Desktop/`에 로컬 게시합니다. 소스를 변경한 뒤 루트에서 `dotnet publish .\RemoteWork.WinForms\RemoteWork.WinForms.csproj -c Release -o .\Desktop`로 다시 만드세요.
