@echo off
:: Launches Windows Sandbox with the project root mapped to C:\source.
:: Uses %~dp0 (this script's own directory) so it works on any machine
:: without hardcoding paths. The generated .wsb is written to %TEMP% and
:: never checked in to source control.

set SCRIPT_DIR=%~dp0
:: Strip trailing backslash that %~dp0 always appends
if "%SCRIPT_DIR:~-1%"=="\" set SCRIPT_DIR=%SCRIPT_DIR:~0,-1%

set WSB_FILE=%TEMP%\AURASchedulerSandbox_%RANDOM%.wsb

(
  echo ^<Configuration^>
  echo   ^<vGPU^>Disable^</vGPU^>
  echo   ^<MappedFolders^>
  echo     ^<MappedFolder^>
  echo       ^<HostFolder^>%SCRIPT_DIR%^</HostFolder^>
  echo       ^<SandboxFolder^>C:\source^</SandboxFolder^>
  echo     ^</MappedFolder^>
  echo   ^</MappedFolders^>
  echo   ^<LogonCommand^>
  echo     ^<Command^>C:\source\InstallerSandboxStartupScript.cmd^</Command^>
  echo   ^</LogonCommand^>
  echo ^</Configuration^>
) > "%WSB_FILE%"

start "" "C:\Windows\System32\WindowsSandbox.exe" "%WSB_FILE%"
