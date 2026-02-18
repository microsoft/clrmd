@echo off
setlocal

set TESTPROJECT=%~dp0src\Microsoft.Diagnostics.Runtime.Tests\Microsoft.Diagnostics.Runtime.Tests.csproj
set EXITCODE=0

if "%1"=="" goto :both
if /I "%1"=="x86"   goto :32bit
if /I "%1"=="x32"   goto :32bit
if /I "%1"=="arm"   goto :32bit
if /I "%1"=="x64"   goto :64bit
if /I "%1"=="amd64" goto :64bit
if /I "%1"=="arm64" goto :64bit

echo Usage: Test.cmd [arch]
echo   No args: run both 32-bit and 64-bit tests
echo   x86, x32, arm:    run 32-bit tests only
echo   x64, amd64, arm64: run 64-bit tests only
exit /b 1

:both
echo === Running 64-bit tests ===
dotnet test "%TESTPROJECT%" --arch x64 %2 %3 %4 %5 %6 %7 %8 %9
if %ERRORLEVEL% neq 0 set EXITCODE=1

echo.
echo === Running 32-bit tests ===
dotnet test "%TESTPROJECT%" --arch x86 %2 %3 %4 %5 %6 %7 %8 %9
if %ERRORLEVEL% neq 0 set EXITCODE=1
goto :done

:64bit
echo === Running 64-bit tests ===
dotnet test "%TESTPROJECT%" --arch x64 %2 %3 %4 %5 %6 %7 %8 %9
if %ERRORLEVEL% neq 0 set EXITCODE=1
goto :done

:32bit
echo === Running 32-bit tests ===
dotnet test "%TESTPROJECT%" --arch x86 %2 %3 %4 %5 %6 %7 %8 %9
if %ERRORLEVEL% neq 0 set EXITCODE=1
goto :done

:done
exit /b %EXITCODE%