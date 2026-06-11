@echo off
setlocal

start appwiz.cpl
start services.msc
start "" "C:\source\Setup\Bundle\bin\x64\Release"
start "" "C:\Program Files"
start "" "C:\Users\WDAGUtilityAccount\AppData\Local"
start "" "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup"

endlocal
