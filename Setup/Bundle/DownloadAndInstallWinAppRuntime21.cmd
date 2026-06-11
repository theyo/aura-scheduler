@echo off
rem Downloads and silently installs the Windows App Runtime 2.1 redistributable.
rem
rem Run by Bundle.wxs as the InstallWinAppRuntime21 ExePackage's SourceFile
rem (instead of an embedded or hash-pinned remote payload) so the bundle stays
rem small and always fetches whatever Microsoft currently publishes at this
rem URL. Windows App Runtime 2.1.x is backwards compatible, so grabbing
rem "latest" satisfies this app's minimum-2.1.3 requirement without pinning a
rem SHA-512 hash that would go stale whenever Microsoft updates the
rem redistributable in place at the same URL (this has already happened once:
rem the file served here changed size between two recent checks).
rem
rem curl.exe has shipped in Windows since the 1803 update, well below this
rem app's Windows 11 (build 22000+) requirement, so it is always available.

setlocal

set "URL=https://aka.ms/windowsappsdk/2.1/2.1.3/windowsappruntimeinstall-x64.exe"
set "OUT=%TEMP%\WindowsAppRuntimeInstall-x64.exe"

curl.exe -fsSL -o "%OUT%" "%URL%"
if errorlevel 1 (
    echo Failed to download Windows App Runtime 2.1 from %URL% 1>&2
    exit /b 1
)

"%OUT%" --quiet
set "EXITCODE=%ERRORLEVEL%"

del /f /q "%OUT%" >nul 2>&1

exit /b %EXITCODE%
