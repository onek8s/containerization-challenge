@echo off
rem Ends a Remote Desktop connection to the desk station and gives the session back to the
rem console, so the desk runner keeps a visible, unlocked desktop. Run as administrator.
for /f "skip=1 tokens=3" %%s in ('query user %USERNAME%') do (
    %windir%\System32\tscon.exe %%s /dest:console
)
