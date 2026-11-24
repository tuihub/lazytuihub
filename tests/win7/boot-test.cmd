@echo off
for %%d in (D E F G H) do if exist %%d:\test.cmd (
    echo LTH-GUEST-START>COM1
    call %%d:\test.cmd > C:\lth-result.txt 2>&1
    if errorlevel 77 exit /b
    type C:\lth-result.txt >COM1
    echo LTH-GUEST-END>COM1
    shutdown /s /t 0
    exit /b
)
