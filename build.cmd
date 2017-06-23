
set scriptRoot=%~dp0
set binDir=%scriptRoot%bin

if not exist %binDir% mkdir %binDir%
call %scriptRoot%init-tools.cmd
call %scriptRoot%Tools\dotnetcli\dotnet restore %scriptRoot%src\Microsoft.Diagnostics.Runtime.sln --packages %scriptRoot%packages
call %scriptRoot%Tools\dotnetcli\dotnet msbuild %scriptRoot%src\Microsoft.Diagnostics.Runtime.sln /p:OutDir="%scriptRoot%\bin" /m /v:m /flp:verbosity=normal;LogFile=%binDir%\msbuild.log %*
