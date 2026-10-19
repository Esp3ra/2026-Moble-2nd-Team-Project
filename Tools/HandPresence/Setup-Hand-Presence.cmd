@echo off
pushd "%~dp0"
py -3.13 -m venv .venv
if errorlevel 1 goto failed
".venv\Scripts\python.exe" -m pip install -r requirements.txt
if errorlevel 1 goto failed
".venv\Scripts\python.exe" download_model.py
if errorlevel 1 goto failed
echo Setup complete. Run Start-Hand-Presence.cmd.
popd
pause
exit /b 0
:failed
echo Setup failed. Keep this window open to read the error.
popd
pause
exit /b 1
