@echo off
dotnet run --project "%~dp0LeavePreview.csproj" -c Release
if errorlevel 1 pause
