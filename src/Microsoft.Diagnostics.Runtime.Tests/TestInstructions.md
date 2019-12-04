Since moving to .NET Core we had to remove references to CodeDOM, which handled compiling tests for us. As a result, we now need a one-off step to generate test assets for use during testing.

## Windows

First install both the x86 and x64 version of Debugging Tools for Windows (windbg, cdb, etc).

Then run `dotnet build` with optional path properties or environment variables if the debugging tools are not in the default locations.

```console
dotnet build TestTargets.csproj -p:CscPath="path\to\csc.exe";Cdb32Path="path\to\32bit\cdb.exe";Cdb64Path="path\to\64bit\cdb.exe"
```

-or-

```console
set CscPath="path\to\csc.exe"
set Cdb32Path="path\to\32bit\cdb.exe"
set Cdb64Path="path\to\64bit\cdb.exe"
dotnet build TestTargets.proj
```

## Linux

```console
dotnet build TestTargets.proj
```
