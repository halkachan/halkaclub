@echo off
rem HARUKA WORLD ver3.9 (local check, not published) - starts the local server and opens the page.
rem   start_v39.bat        this PC only (http://127.0.0.1:8960/halkaworld/)
rem   start_v39.bat lan    also phones on the same Wi-Fi
cd /d "%~dp0"
where python > nul 2>&1 || (echo Python 3 is required: https://www.python.org/ & pause & exit /b 1)
start "" /min cmd /c "ping -n 3 127.0.0.1 > nul & start http://127.0.0.1:8960/halkaworld/"
if /i "%~1"=="lan" (python serve_v39.py --lan) else (python serve_v39.py)
pause
