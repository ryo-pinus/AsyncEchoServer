@echo off

setlocal

if "%VS140COMNTOOLS%" == "" goto :noVS2015

call "%VS140COMNTOOLS%vsvars32.bat"


InstallUtil.exe "%~dp0AsyncEchoServer\bin\Release\AsyncEchoServer.exe"
if errorlevel 1 goto error

goto exit

:noVS2015
@echo Visual Studio 2015 not installed.
goto exit

:error

goto exit

:exit
endlocal
