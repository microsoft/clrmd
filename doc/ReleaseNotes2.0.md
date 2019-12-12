# Introduction

The first version of ClrMD is nearly 10 years old now, and it's starting to show its age.  Over the years I've made some bad design decisions and added some questionable features that I wish I could fix or move to another (supported) library, but I've tried to maintain compatibility for folks who already built tools on top of ClrMD and that's hamstrung development in a lot of areas.

In the coming months, I am planning to build ClrMD v2 which will have a lot of breaking changes for the library.  I am planning to continue fixing critical ClrMD v1 issues for the forseeable future.  However, the ClrMD v2 codebase will be a *significant* refactor of the current codebase, and all new features and development will go into ClrMD v2 (with critical fixes and some stuff being backported).

This document briefly outlines my plans and roadmap for ClrMD v2.  I would love any feedback from the community about this plan, so **I have created Issue https://github.com/microsoft/clrmd/issues/311 for any suggestions, concerns, and feature requests**.  I also plan to do all development in the open, a ClrMD v2 branch will be created and active as I'm doing my refactor work.

This document is a bit sparse now, but it will be continually updated as I get more feedback and as I make changes to the v2 codebase.

# Goals

## Better performance, less memory.

I would like to use Span<T> in place of most byte[] data reading operations.  There's a ton of garbage generated that can just be eliminated if I do this.  I'd also like to take a hard look at the memory that gets allocated (but never freed) during the run of the process to see if there's some low hanging fruit to eliminate.

I do not have specific performance or memory goals other than "both must be better than v1".

## Better architecture

ClrMD has a deep, confusing, and poorly thought out class heirarchy.  This mostly grew organically from years of development and not cleaned up due to compatibility concerns but this is my first priority to fix.  For example, ClrRuntime -> RuntimeBase -> DesktopRuntimeBase -> LegacyRuntime (need a cleaner separation between the dac layer and the runtime...)

I am also considering removing Lazy<T> usage throughout most of the codebase because it makes it very difficult to debug ClrMD (plus all of the hanging lambdas).  Most things would still be lazily evaluated, just in a way that's easier to debug.

## Remove certain features that should have been in a separate library

Things like GCRoot, BlockingObjects, etc I don't want to carry forward in the base ClrMD library.  To be clear the plan for this is to put that code in either a side-library or as a code example you can put into your own project.  The functionality will still remain for those that use it today, but I want to make ClrMD more of a "building blocks" library and push these features that merely make use of ClrMD into their own codebase.

## Clean up inheritance related issues

From issue [313](https://github.com/microsoft/clrmd/issues/313): "(If you are not from Mono team) I believe the general conventions are exposing protected properties for private fields instead of exposing protected fields."

Additionally we need to clean up what is meant to be reimplmeneted by folks and what is not.

## Clean up poor design choices from 8 years ago.

Should we do our own metadata reading in a lot of cases?  Is all of this code really needed?  https://github.com/microsoft/clrmd/blob/bbde79ff1d816d15697ae12dc7a5982682a83919/src/Microsoft.Diagnostics.Runtime/src/Desktop/DesktopInstanceField.cs#L47  Maybe.  Probably.  I want to go review it and see if we should be pushing that to ISOSDac to provide data instead of doing a lot of this parsing...

Enumerating types should have been handled much differently.

## Build features in .Net Core v.next to support features.

Generics support is just broken and terrible and has been forever.  I am considering doing some work in .Net Core itself so that future versions of the runtime are more debuggable.



# Changes from ClrMD 1.1

Added features:

1.  **Memory Usage** - ClrMD has reduced its overall memory usage.  It also allows you to finely control what ClrMD caches to allow the user to trade performance for lower overall memory usage.  Finally, we also allow you to completely clear all cached memory in ClrMD at any time to prevent OOM.  This even works while in the middle of enumerating the heap.
2.  **Thread Safety** - If ClrRuntime.IsThreadSafe returns true then you are allowed to call all functions from multiple threads.  Please note that you likely will not see much performance improvements by running algorithms in parallel.  The CLR debugging layer, mscordaccore.dll, takes a giant lock around most operations which means we do not get easy performance gains.
3.  **Nullable Reference Types** - ClrMD fully implements nullable reference types so you will know exactly what you need to null check from the library.
4.  **Architectural Overhaul** - ClrMD is now much less internally coupled.  Most implementation types are now public, allowing you to use those types directly or to use the I\*Builder interfaces to "mock" objects.

Removed:

1. **Removed Blocking Object** code - This was buggy in its initial implementation and never worked right.  We do not intend to support this or rebuild it in 2.0.
2. **SymbolLocator partially removed** - ClrMD still supports communicating with a symbol server to locate binaries but it's no longer intended to be a general purpose symbol server client.
3. **CLR v2 and v4 (prior to 4.5) support removed** - ClrMD now only supports Desktop .Net 4.5+ and all .Net Core versions.
4. **PDB Reading Removed** - The implementation I was carrying in ClrMD was buggy and did not support PortablePDBs.  I do not intend to keep this functionality in ClrMD 2.0.  It's just too difficult to keep working and it's not needed or used by the rest of the library.

# Features rejected

Allow x64 process to debug x32 dumps - This is very, very difficult to acheive architecturally, because CLR's underlying debugging API is native code and deeply tied to architecture.  I took a look at doing this but ultimately could not come up with a viable approach for the 2.0 release.

# Feedback from the community

Here is a list of feedback I am tracking from the community (my summary):

1.  It is difficult to build unit tests for code which uses ClrMD because it requires hydrating an entire object tree.  It would be nice if the library was mock-able. - **Completed (somewhat)** - *Did not add interfaces for every type, but made it easier to construct your own Clr* objects.*
2.  Consider enabling nullable reference types. - **Completed**
3.  Have a mode where I can indicate what is most important to me speed or memory usage. - **Completed** - *I plan to have a way to limit the amount ClrMD allocates at the cost of performance.*
4.  Consider using native memory where it makes sense (basically limit small allocations and other memory thrashing.  **Completed** - *The library has moved to using Span with stackalloc/ArrayPool.Rent in the vast majority of allocation cases.  I've also removed the usage of most marshalled arrays in PInvoke signatures, preferring instead to pass pointers to byte arrays.*
5.  Clean up the design of a lot of weird parts of the library.  - **Approved** - *That is the goal of ClrMD 2.0.*
6.  Improve the messages and exceptions thrown by the library. - **Tentatively Planned**
7.  Document the expected 'cost' of main operations in memory/CPU.  - **Unsure** - *There's so many factors here I'm not sure what success looks like.  I think providing a set of benchmarks would be ideal for developers to understand what expected performance should look like.  This is not something I intend to tackle for the 2.0 release.*

