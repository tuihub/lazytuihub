@echo off
if exist "%~dp0updates\loader.msu" if not exist C:\lth-patched (
    powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0updates.ps1"
    if errorlevel 1 goto finished
    type C:\lth-result.txt >COM1
    echo updated>C:\lth-patched
    echo LTH-PREREQUISITES-REBOOT>COM1
    shutdown /r /t 0
    exit /b 77
)
type nul > C:\lth-checks.txt
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0probe.ps1"
type C:\lth-checks.txt
:finished
exit /b 0
