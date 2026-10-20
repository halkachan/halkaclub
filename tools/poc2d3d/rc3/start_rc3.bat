@echo off
rem HARUKA WORLD ver3.0 RC3 - starts the local server and opens the staging page.
rem   start_rc3.bat        this PC only (http://127.0.0.1:8899/halkaworld/)
rem   start_rc3.bat lan    also phones on the same Wi-Fi (see DEVICE_TEST_PROCEDURE_RC3.md)
cd /d "%~dp0"
where python > nul 2>&1 || (echo Python 3 is required: https://www.python.org/ & pause & exit /b 1)
start "" /min cmd /c "ping -n 3 127.0.0.1 > nul & start http://127.0.0.1:8899/halkaworld/"
if /i "%~1"=="lan" (python serve_rc3.py --lan) else (python serve_rc3.py)
pause
