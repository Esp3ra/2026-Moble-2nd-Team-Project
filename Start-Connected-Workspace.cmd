@echo off
cd /d "%~dp0"
dotnet run --project ".\Tools\ConnectedWorkspace\ConnectedWorkspace.csproj" -c Release --artifacts-path "%LOCALAPPDATA%\RemoteWork\builds\connected-workspace" -- --run
