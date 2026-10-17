@echo off
chcp 65001 >nul
if exist "%~dp0Desktop\RemoteWork.WinForms.exe" (
    start "" "%~dp0Desktop\RemoteWork.WinForms.exe"
) else (
    dotnet run --project "%~dp0RemoteWork.WinForms\RemoteWork.WinForms.csproj"
    pause
)
