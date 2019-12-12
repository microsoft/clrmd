# ClrMD 2.0 Beta Release Notes

Over the last few weeks we have been rewriting a lot of the internal core of ClrMD to fix a lot of issues that have been problematic in the library.  ClrMD 2.0 is faster, uses less memory, and is thread safe (with exceptions), but it has a lot of breaking changes which will require you to update existing code to use it.

# Quick Notes

We plan to continue fixing bugs in ClrMD 1.1 for the forseeable future, so you are not forced to upgrade if you do not want to.  I do plan to only add new features to ClrMD 2.0, but the community is welcome to submit Pull Requests to backport or add features to ClrMD 1.1.

**Please note that the API surface area of ClrMD 2.0 is not finalized and there may be breaking changes between now and when the library is out of beta.**  I expect to release the non-beta version around March/April 2020.

# Changes from ClrMD 1.1

## Added features:

1.  **Memory Usage/Performance** - ClrMD has reduced its overall memory usage and improved performance.
2.  **Cache Control** - You can now control what ClrMD caches to via `DataTarget.CacheOptions`.  Note that disabling caches does greatly slow the library down but it also drastically reduces memory usage.  You can also call `ClrRuntime.FlushCachedData` at any time to empty ClrMD's caches and reclaim memory.
3.  **Thread Safety** - If `ClrRuntime.IsThreadSafe` returns true then you are allowed to call all functions from multiple threads.  Please note that you likely will not see much performance improvements by running algorithms in parallel.  The CLR debugging layer, mscordaccore.dll, takes a giant lock around most operations which means we do not get easy performance gains.  This should make your code much easier to write if you need to do work on another thread.
4.  **Nullable Reference Types** - ClrMD fully implements nullable reference types so you will know exactly what you need to null check from the library.
5.  **Architectural Overhaul** - ClrMD is now much less internally coupled.  Most implementation types are now public, allowing you to use those types directly or to use the I\*Builder interfaces to "mock" objects more easily.  While the changes aren't perfect for mocking the library, it's definitely better than were we were before.
6.  **Clean up Exceptions Thrown by ClrMD** - *This is planned but not yet completed.*
7.  **Provide a set of Benchmarks** -  ClrMD will provide a reasonable set of benchmarks so you know how much an operation should cost. *This is planned but not yet completed.*


## Removed:

1. **Removed Blocking Objects** - This was buggy in its initial implementation and never worked right.  We do not intend to support this or rebuild it in 2.0.
2. **SymbolLocator partially removed** - ClrMD still supports communicating with a symbol server to locate binaries but it's no longer intended to be a general purpose symbol server client.
3. **CLR v2 and v4 (prior to 4.5) support removed** - ClrMD supports all .Net Core versions and Desktop .Net 4.5 and beyond.  If you need to debug earlier versions of CLR, please use ClrMD 1.1.
4. **PDB Reading Removed** - The implementation I was carrying in ClrMD was buggy and did not support PortablePDBs.  I do not intend to keep this functionality in ClrMD 2.0.  It's just too difficult to keep working and it's not needed or used by the rest of the library.


# Why did we make these changes?

The first version of ClrMD is nearly 10 years old now, and it's starting to show its age.  Over the years I've made some bad design decisions and added some questionable features that I wish I could fix or move to another (supported) library, but I've tried to maintain compatibility for folks who already built tools on top of ClrMD and that's hamstrung development in a lot of areas.

Please feel free to file issues against this beta version of ClrMD if you have questions, a feature request, or a bug to report.

## Better performance, less memory.

We use Span<T> in place of most byte[] data reading operations, and use ArrayPool instead of doing our own allocating.  We have eliminated a ton of needless garbage generated at runtime.

## Better architecture

The old ClrMD implementation had a deep, confusing, and poorly thought out internal class heirarchy.  This mostly grew organically from years of development and not be cleaned up due to compatibility concerns.  The entire library has been refactored from the ground up to fix this.  We've also removed Lazy<T> usage throughout a lot of the codebase because it made debugging problems with ClrMD difficult to do.  We still evaluate most properties lazily, just without the use of that class.

# Thanks

Thank you to everyone who contributed feedback, issues, and code for ClrMD 2.0.  Especially NextTurn, who submitted countless changes and pull requests to make this possible.
