@echo off

rem set csc="c:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
rem set cdb32="C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\cdb.exe"
rem set cdb64="C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\cdb.exe"

if not defined csc goto :csc_not_defined
if not exist %csc% goto :csc_not_found
if not defined cdb32 goto :cdb32_not_defined
if not exist %cdb32% goto :cdb32_not_found
if not defined cdb64 goto :cdb64_not_defined
if not exist %cdb64% goto :cdb64_not_found

set SourcePath=%~dp0
set BinPath=%SourcePath%\Bin
set Bin32=%BinPath%\x86
set Bin64=%BinPath%\x64

rmdir /S /Q %BinPath%
mkdir %BinPath%
mkdir %Bin32%
mkdir %Bin64%

%csc% /debug /target:library /out:%Bin32%\SharedLibrary.dll /pdb:%Bin32%\SharedLibrary.pdb %SourcePath%\Shared\SharedLibrary.cs
%csc% /debug /target:library /out:%Bin64%\SharedLibrary.dll /pdb:%Bin64%\SharedLibrary.pdb %SourcePath%\Shared\SharedLibrary.cs

for %%c in (%SourcePath%\*.cs) do (
    %csc% /unsafe /reference:%Bin32%\SharedLibrary.dll /platform:x86 /debug /out:%Bin32%\%%~nc.exe /pdb:%Bin32%\%%~nc.pdb %%c
    %csc% /unsafe /reference:%Bin64%\SharedLibrary.dll /platform:x64 /debug /out:%Bin64%\%%~nc.exe /pdb:%Bin64%\%%~nc.pdb %%c
)

for %%c in (%SourcePath%\*.cs) do (
    %cdb32% -g -G -c ".dump /ma %Bin32%\%%~nc_wks.dmp;.dump /ma %Bin32%\%%~nc_wks.dmp;.dump /m %Bin32%\%%~nc_wks_mini.dmp;q" %Bin32%\%%~nc.exe
    %cdb64% -g -G -c ".dump /ma %Bin64%\%%~nc_wks.dmp;.dump /ma %Bin64%\%%~nc_wks.dmp;.dump /m %Bin64%\%%~nc_wks_mini.dmp;q" %Bin64%\%%~nc.exe
)

set COMPLUS_BuildFlavor=SVR
%cdb32% -g -G -c ".dump /ma %Bin32%\types_svr.dmp;.dump /ma %Bin32%\types_svr.dmp;q" %Bin32%\types.exe
%cdb64% -g -G -c ".dump /ma %Bin64%\types_svr.dmp;.dump /ma %Bin64%\types_svr.dmp;q" %Bin64%\types.exe
set COMPLUS_BuildFlavor=

goto :exit

:csc_not_defined
echo Error: csc is not defined.
goto :exit

:cdb32_not_defined
echo Error: cdb32 is not defined.
goto :exit

:cdb64_not_defined
echo Error: cdb64 is not defined.
goto :exit

:csc_not_found
echo Error: csc.exe (%csc%) not found.
goto :exit

:cdb32_not_found
echo Error: cdb.exe (x86) (%cdb32%) not found.
goto :exit

:cdb64_not_found
echo Error: cdb.exe (x64) (%cdb64%) not found.
goto :exit

:exit
