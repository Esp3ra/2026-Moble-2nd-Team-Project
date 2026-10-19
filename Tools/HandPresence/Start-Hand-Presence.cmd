@echo off
pushd "%~dp0"
if not exist ".venv\Scripts\python.exe" goto missing
if not exist "models\hand_landmarker.task" goto missing
".venv\Scripts\python.exe" hand_presence.py
if errorlevel 1 pause
popd
exit /b
:missing
echo Run Setup-Hand-Presence.cmd first.
popd
pause
exit /b 1
