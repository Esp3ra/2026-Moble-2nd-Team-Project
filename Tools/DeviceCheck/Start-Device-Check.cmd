@echo off
pushd "%~dp0"
python device_check.py
if errorlevel 1 pause
popd
