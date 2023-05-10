# Migrating to 2.1

Here is a summary of breaking changes in ClrMD 2.1:

## Microsoft.Diagnostics.Runtime.Architecture -> System.Runtime.InteropServices.Architecture

Use System.Runtime.InteropServices.Architecture instead.  Note that the values have shifted, since ClrMD defined an "Unknown" enum member as 0, which is not in the new architecture list.

## IBinaryLocator -> IFileLocator

We have removed `IBinaryLocator` and replaced it with `IFileLocator`.  Similarly the properties on `DataTarget` and `CustomDataTarget` have changed accordingly.

If you originally implemented the `IBinaryLocator` interface, you will now need to implement `IFileLocator` instead.  This interface is meant to fully capture the SSQP conventions for making symbol server requests.  The specs for which can be found at [SSQP Key Conventions](https://github.com/dotnet/symstore/blob/main/docs/specs/SSQP_Key_Conventions.md) and [SSQP CLR Private Key conventions](https://github.com/dotnet/symstore/blob/main/docs/specs/SSQP_CLR_Private_Key_Conventions.md).

Most folks don't need to implement these interface specifically, and ClrMD provides a reasonable default implementation.  Unfortunately, the best way to see how these are implemented is to look at ClrMD's implementation if you need to change behavior.

### Why did we make this change?

`IBinaryLocator` did not provide the proper interface surface to fully capture Elf and Mach-O symbol server requests and the underlying interface could not be made to work without resorting to some really bad hacks.

It was a lot cleaner to replace `IBinaryLocator` than to try to hack around the original implementation.

## PEImage and Elf related classes are now internal

TODO:  Provide stream so this can be passed to other libraries.

### Why did we make this change?

ClrMD was designed 10 years ago as a monolithic API that does everything for the user, such as parsing files that are unrelated to .Net diagnostics.  It was originally designed this way because there wasn't a healthy NuGet ecosystem at the time where these kinds of APIs existed.

We've found some very tough to solve problems in the internals of PEImage where what ClrMD needs is a PE image reader which understands that data could be missing from a dump file, that we may need to go request the file from a symbol server mid-operation, and that requesting from a symbol server should only be done on demand and not eagerly.

We cannot reasonably make those fixes changes without another breaking change to PEImage.  At this point it makes sense to have folks use a REAL PE image (and elf image) reader instead of ClrMD's half-baked one.

Some alternatives:

1.  https://www.nuget.org/packages/Marklio.Metadata/
2.  System.Reflection.Metadata
3.  https://github.com/dotnet/symstore/tree/main/src/Microsoft.FileFormats


## ModuleInfo has changed slightly

If you implement your own IDataTarget and need to produce ModuleInfo you will need to define an implementation here.  I will likely mark my implementations of ModuleInfo for pefiles, elf files, and mach-o files as public at some point, but I wanted to make sure they are actually ready (design-wise) to be marked public first.

## VersionInfo is removed

Use `System.Version` instead.


## ClrInfo.DacInfo -> ClrInfo.DebugLibraryInfo

Additionally we added ClrInfo.IsSingleFile.

### `ClrInfo.DacLibrary` -> `ClrInfo.DebuggingLibraries`

Instead of providing a single "DacLibrary", we now enumerate all debugging libraries we know to exist for this CLR runtime.  The resulting list is stored in `DebuggingLibraries` instead.  Most folks didn't use `DacLibrary` directly, but if you did then you will need to enumerate `DebuggingLibraries` and find all libraries which match your current platform and architecture.  Additionally, we also enumerate DBI libraries even though ClrMD does not use them (this is `DebugLibraryInfo.Kind`).

You will still find the original file `DacLibrary` pointed to in this list of dacs.


### ClrRuntimeInfo is no longer marked public

This is an odd struct that probably will change over the lifetime of .Net Core.  It should not have been exposed as public to begin with.  Instead all of this information is provided by `ClrInfo.DebuggingLibraries`.

### ClrInfoProvider was removed

This functionality has been wrapped into `ClrInfo`, and probably shouldn't have been marked public to begin with.


### Why did we make this change?

Creating `ClrInfo` was a strange, multi class process and involved a lot of moving pieces spread over the codebase.  We now consolidated all of the relevant code into ClrInfo.cs.  We've also pulled together all of the various ways of finding the DAC and DBI libraries all in one place and provided a way for the user to locate all of the various binaries.

This also lets ClrMD enumerate through all possible matching DAC libraries and query the symbol server for all of them.  This is especially helpful in case one of the dacs happens to be missing from the symbol server (which should be rare but isn't unheard of).
