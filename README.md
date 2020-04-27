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

`Microsoft.Diagnostics.Runtime.dll` (nicknamed "CLR MD") is a process and crash
dump introspection library. This allows you to write tools and debugger plugins
which can do thing similar to SOS and PSSCOR.

For more details, take a look at the [documentation] and [samples].

[documentation]: ./doc/ClrRuntime.md
[samples]: https://github.com/Microsoft/dotnetsamples/tree/master/Microsoft.Diagnostics.Runtime/CLRMD

## FAQ

Please see the [FAQ](./doc/FAQ.md) for more information.

## Tutorials

Here you will find a step by step walkthrough on how to use the CLR MD API.
These tutorials are meant to be read and worked through in linear order to teach
you the surface area of the API and what you can do with it.

1. [Getting Started](./doc/GettingStarted.md) - A brief introduction
   to the API and how to create a CLRRuntime instance.

2. [The CLRRuntime Object](./doc/ClrRuntime.md) - Basic operations
   like enumerating AppDomains, Threads, the Finalizer Queue, etc.

3. [Walking the Heap](./doc/WalkingTheHeap.md) - Walking objects on
   the GC heap, working with types in CLR MD.

4. [Types and Fields in CLRMD](./doc/TypesAndFields.md) - More
   information about dealing with types and fields in CLRMD.

5. [Machine Code in CLRMD](./doc/MachineCode.md) - Getting access to
   the native code produced by the JIT or NGEN
