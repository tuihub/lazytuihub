@echo off
copy /y "%~dp0boot-test.cmd" C:\boot-test.cmd
schtasks /create /tn LazyTuiHubCompatibility /sc ONSTART /ru SYSTEM /tr C:\boot-test.cmd /f
if errorlevel 1 exit /b 1
echo LTH-BASE-READY>COM1
shutdown /s /t 0
