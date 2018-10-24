@echo off

for %%X in (csc.exe) do (set CSC=%%~$PATH:X)

if not defined CSC goto :csc_not_found
if not defined DEBUG32 goto :debug32_not_defined
if not defined DEBUG64 goto :debug64_not_defined

set BinPath=%~dp0bin
set Bin32=%BinPath%\x86
set Bin64=%BinPath%\x64

mkdir %BinPath%
mkdir %Bin32%
mkdir %Bin64%

csc.exe /debug /target:library /pdb:%Bin32%\SharedLibrary.pdb /out:%Bin32%\SharedLibrary.dll %~dp0\Shared\SharedLibrary.cs
csc.exe /debug /target:library /pdb:%Bin64%\SharedLibrary.pdb /out:%Bin64%\SharedLibrary.dll %~dp0\Shared\SharedLibrary.cs

for %%c in (*.cs) do (
    csc.exe /unsafe /reference:%Bin32%\SharedLibrary.dll /platform:x86 /debug /pdb:%Bin32%\%%~nc.pdb /out:%Bin32%\%%~nc.exe %~dp0%%c
    csc.exe /unsafe /reference:%Bin64%\SharedLibrary.dll /platform:x64 /debug /pdb:%Bin64%\%%~nc.pdb /out:%Bin64%\%%~nc.exe %~dp0%%c
)


for %%c in (*.cs) do (
    %DEBUG32%\cdb.exe -g -G -c ".dump /ma %Bin32%\%%~nc_wks.dmp;.dump /ma %Bin32%\%%~nc_wks.dmp;.dump /m %Bin32%\%%~nc_wks_mini.dmp;q" %Bin32%\%%~nc.exe
    %DEBUG64%\cdb.exe -g -G -c ".dump /ma %Bin64%\%%~nc_wks.dmp;.dump /ma %Bin64%\%%~nc_wks.dmp;.dump /m %Bin64%\%%~nc_wks_mini.dmp;q" %Bin64%\%%~nc.exe
)


set COMPLUS_BuildFlavor=SVR
%DEBUG32%\cdb.exe -g -G -c ".dump /ma %Bin32%\types_svr.dmp;.dump /ma %Bin32%\types_svr.dmp;q" %Bin32%\types.exe
%DEBUG64%\cdb.exe -g -G -c ".dump /ma %Bin64%\types_svr.dmp;.dump /ma %Bin64%\types_svr.dmp;q" %Bin64%\types.exe
set COMPLUS_BuildFlavor=

goto :exit

:csc_not_found
echo Error: csc.exe not found on your PATH.
goto :exit

:debug32_not_defined
echo Error: DEBUG32 is not defined or does not have cdb.exe in that path.
goto :exit

:debug64_not_defined
echo Error: DEBUG64 is not defined or does not have cdb.exe in that path.
goto :exit

:exit
