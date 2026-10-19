@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0RemoteWork.Server"
if not exist ".venv\Scripts\python.exe" (
    echo 먼저 서버 실행으로 Python 환경을 준비해 주세요.
    pause
    exit /b 1
)
set "REMOTEWORK_ENABLE_CHAT_DRAFT=1"
".venv\Scripts\python.exe" -m flask --app remotework chat init-tables
if errorlevel 1 (
    echo 채팅 테이블 준비에 실패했습니다. 위 오류와 .env의 DB 설정을 확인해 주세요.
    pause
    exit /b 1
)
echo 채팅 테이블 준비가 끝났습니다. 서버와 클라이언트를 다시 실행해 주세요.
pause
endlocal
exit /b 0
