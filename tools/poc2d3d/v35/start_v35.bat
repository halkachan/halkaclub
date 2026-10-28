@echo off
rem HARUKA WORLD ver3.5 (local check, not published) - starts the local server and opens the page.
rem   start_v35.bat        this PC only (http://127.0.0.1:8938/halkaworld/)
rem   start_v35.bat lan    also phones on the same Wi-Fi
cd /d "%~dp0"
where python > nul 2>&1 || (echo Python 3 is required: https://www.python.org/ & pause & exit /b 1)
start "" /min cmd /c "ping -n 3 127.0.0.1 > nul & start http://127.0.0.1:8938/halkaworld/"
if /i "%~1"=="lan" (python serve_v35.py --lan) else (python serve_v35.py)
pause
