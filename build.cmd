@ECHO OFF

if not exist bin mkdir bin

"%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" src\Microsoft.Diagnostics.Runtime.sln /p:OutDir="%~dp0\bin" /nologo /m /v:m /nr:false /flp:verbosity=normal;LogFile=bin\msbuild.log %*
