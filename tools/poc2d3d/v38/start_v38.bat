@echo off
rem HARUKA WORLD ver3.8 (local check, not published) - starts the local server and opens the page.
rem   start_v38.bat        this PC only (http://127.0.0.1:8950/halkaworld/)
rem   start_v38.bat lan    also phones on the same Wi-Fi
cd /d "%~dp0"
where python > nul 2>&1 || (echo Python 3 is required: https://www.python.org/ & pause & exit /b 1)
start "" /min cmd /c "ping -n 3 127.0.0.1 > nul & start http://127.0.0.1:8950/halkaworld/"
if /i "%~1"=="lan" (python serve_v38.py --lan) else (python serve_v38.py)
pause
