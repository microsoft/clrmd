# Introduction

The first version of ClrMD is nearly 10 years old now, and it's starting to show its age.  Over the years I've made some bad design decisions and added some questionable features that I wish I could fix or move to another (supported) library, but I've tried to maintain compatibility for folks who already built tools on top of ClrMD and that's hamstrung development in a lot of areas.

In the coming months, I am planning to build ClrMD v2 which will have a lot of breaking changes for the library.  I am planning to continue fixing critical ClrMD v1 issues for the forseeable future.  However, the ClrMD v2 codebase will be a *significant* refactor of the current codebase, and all new features and development will go into ClrMD v2 (with critical fixes and some stuff being backported).

This document briefly outlines my plans and roadmap for ClrMD v2.  I would love any feedback from the community about this plan, so **I have created Issue https://github.com/microsoft/clrmd/issues/306 for any suggestions, concerns, and feature requests**.  I also plan to do all development in the open, a ClrMD v2 branch will be created and active as I'm doing my refactor work.

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