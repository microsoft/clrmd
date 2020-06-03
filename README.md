# Microsoft.Diagnostics.Runtime

[![ClrMD release](https://img.shields.io/nuget/v/Microsoft.Diagnostics.Runtime)](https://www.nuget.org/packages/Microsoft.Diagnostics.Runtime/)
[![ClrMD pre-release](https://img.shields.io/nuget/vpre/Microsoft.Diagnostics.Runtime)](https://www.nuget.org/packages/Microsoft.Diagnostics.Runtime/)

| |Debug|Release|
|-|:-:|:-:|
|**Windows**|[![Build Status][windows-debug-status]][latest-build]|[![Build Status][windows-release-status]][latest-build]|
|**Linux**|[![Build Status][linux-debug-status]][latest-build]|[![Build Status][linux-release-status]][latest-build]|

[windows-debug-status]: https://dev.azure.com/dnceng/public/_apis/build/status/Microsoft-clrmd-github?configuration=Windows_NT%20debug&label=tests
[windows-release-status]: https://dev.azure.com/dnceng/public/_apis/build/status/Microsoft-clrmd-github?configuration=Windows_NT%20debug&label=tests
[linux-debug-status]: https://dev.azure.com/dnceng/public/_apis/build/status/Microsoft-clrmd-github?configuration=Linux%20debug&label=tests
[linux-release-status]: https://dev.azure.com/dnceng/public/_apis/build/status/Microsoft-clrmd-github?configuration=Linux%20release&label=tests

[latest-build]: https://dev.azure.com/dnceng/public/_build/latest?definitionId=255

Latest package is available from Azure DevOps public feed: `https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json` ([browse](https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet-tools)).

`Microsoft.Diagnostics.Runtime.dll` also called "ClrMD" is a process and crash
dump introspection library. This allows you to write tools and debugger plugins
which can do thing similar to SOS and PSSCOR.

For more details, take a look at the [GettingStarted] guide, [FAQ], and [Samples].

[GettingStarted]: ./doc/GettingStarted.md
[FAQ]: ./doc/FAQ.md
[Samples]: ./src/Samples
