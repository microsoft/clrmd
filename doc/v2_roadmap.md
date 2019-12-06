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

I erased the concept of "Assembly" even though it really exists...  That was a bad call.

Should we do our own metadata reading in a lot of cases?  Is all of this code really needed?  https://github.com/microsoft/clrmd/blob/bbde79ff1d816d15697ae12dc7a5982682a83919/src/Microsoft.Diagnostics.Runtime/src/Desktop/DesktopInstanceField.cs#L47  Maybe.  Probably.  I want to go review it and see if we should be pushing that to ISOSDac to provide data instead of doing a lot of this parsing...

Enumerating types should have been handled much differently.

##  Allow ClrMD to take dependencies on other libraries instead of being a monolithic DLL.

ClrMD was built before NuGet existed.  It used to be very difficult to take dependencies on other DLLs effectively.  I will likely take some dependencies on other NuGet pacakges and push some of the code out of the library.  For example, I fully plan to eliminate "DefaultSymbolLocator" and use the CLR diagnostics team's implementation.

## Drop CLR v2 support?

I'm considering dropping clr V2 support ("mscorwks.dll" aka CLR 3.5).  If people are still actually using this I will leave it in.  Right now it's very poorly tested and likely broken in some cases.  If there is interest in keeping it around then I plan to add more testing.

## Build features in .Net Core v.next to support features.

Generics support is just broken and terrible and has been forever.  I am considering doing some work in .Net Core itself so that future versions of the runtime are more debuggable.

## Testing

I really hope to find a better solution for testing the product. Since we require a crash dump/core dump to debug it's very difficult to have a sane checked-in test environment.


# Features slated for removal

1.  Blocking Objects - will be either reference code or in a separate library.
2.  GCRoot - I want folks to share an implementation but it will likely be pushed to a separate library.
3.  PDB reading - This is out of date, doesn't support portable pdbs, and there are better libraries to do it.  It was added back before there was really any PDB reading code to use (this was before NuGet) so it's a good time to axe this implementation.

# Features under consideration (not commited to)

1.  Allow x64 process to debug x32 dumps.  This is very, very difficult to acheive architecturally, because CLR's underlying debugging API is native code and deeply tied to architecture.  I don't believe I will be able to implement this reasonably, but I intend to take a close look at it.
2.  Thread safety.  Is it possible to inspect the heap from multiple threads without too much of a perf penalty?  The real issue with this is the DbgEng based IDataReader does not support access from multiple threads.  This is theoretically possible using a custom dump reader.
3.  A way to control the amount of memory ClrMD caches.

# Feedback from the community

Here is a list of feedback I am tracking from the community (my summary):

1.  It is difficult to build unit tests for code which uses ClrMD because it requires hydrating an entire object tree.  It would be nice if the library was mock-able. - **Completed (somewhat)** - *Did not add interfaces for every type, but made it easier to construct your own Clr* objects.*
2.  Consider enabling nullable reference types. - **Completed**
3.  Have a mode where I can indicate what is most important to me speed or memory usage. - **Approved** - *I plan to have a way to limit the amount ClrMD allocates at the cost of performance.* 
4.  Consider using native memory where it makes sense (basically limit small allocations and other memory thrashing.  **Completed** - *The library has moved to using Span with stackalloc/ArrayPool.Rent in the vast majority of allocation cases.  I've also removed the usage of most marshalled arrays in PInvoke signatures, preferring instead to pass pointers to byte arrays.*
5.  Clean up the design of a lot of weird parts of the library.  - **Approved** - *That is the goal of ClrMD 2.0.*
6.  Improve the messages and exceptions thrown by the library. - **Tentatively Planned**
7.  Document the expected 'cost' of main operations in memory/CPU.  - **Unsure** - *There's so many factors here I'm not sure what success looks like, but I'll take a look.*

# TODO reminders

(Notes to myself about what I need to revisit.)

1.  SymbolLocator related code.
2.  Offsets for string and exception fields when we have no metadata.
3.  Document all functions
4.  consider reworking ClrField.GetValue
5.  Enum related code in ClrType
6.  clean up exception usage
7.  Add more tests now that we can easily mock up objects.
8.  All caching and parallel support was stripped out of GCRoot.  Need to add it back.
9.  SafeWin32Handle -> SafeAccessTokenHandle
10.  Consider removing GetFieldForOffset


# .Net Core 5 Dac Wishlist

1. Get ElementType for method table data.
2. All method tables => type handle.
3. Generics.
4. Images report size.
5. TypeSpec enumeration
6. Change modulemaptraverse to fill an array
