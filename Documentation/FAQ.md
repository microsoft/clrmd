# Frequently Asked Questions (FAQs)

## The API is in beta, how stable is it?

The API is in beta for two reasons: Not all planned features are implemented and
I may change the public surface of the API.

Currently all implemented features are fairly well tested, and the API itself is
used in PerfView for real world diagnostics.

## Does this API work with live processes or only dump files?

Currently, this API is only supported with dump files. There is experimental
support for attaching to a live process (without suspending that process) to
collect live samples of the heap. However, this is currently very experimental,
and not at all supported.

In a future release, I will be adding support for suspending a live process to
collect data. This is not yet implemented.

## This API was written in C#. How should I use it in my C++ code?

CLR MD exposes COM bindings for its API. Please see the C Example for getting
started with that.

Please note that the COM bindings have a bit of overhead (having to marshal data
in and out of CLR), but are still in the ballpark for performance of pure C#
code.

## Does this API have any dependencies (what other dlls do I need to bundle with my tool)?

CLR MD has no dependencies (other than the correct version of mscordacwks.dll
for the crash dump you are debugging, which is required for all .Net debugging
tools). You literally only need to ship ClrMemDiag.dll with your program (and
have the code written to request the right mscordacwks.dll from the symbol
server).

## Does this work with any architecture? (x86/x64?)

Yep, this API works with crash dumps of both x86 and amd64 processes.

However, you must match the architecture of your program to the crash dump you
are reading. Meaning if you are debugging an x86 crash dump, your program must
run as an x86 process, similar for an x64 dump, you need an x64 process. This is
usually done with the /platform directive to C#.

## Wait, why do I need to match architecture of my process to the dump?

This library works by loading the private CLR debugging library
(mscordacwks.dll) into the process. This is a native DLL which is tied to the
architecture of the dump you are debugging. Unfortunately by using that native
DLL the C# code calling into CLR MD is then tied to the same architecture
requirement.

Theoretically you could get around this requirement yourself in a few ways. For
example, you can wrap your calls to the API into a seperate process, then use
interprocess communication to relay the information. I do not plan on adding
anything to the API to do this automatically though.

## I can't find the equivalent of `!gcroot` and `!objsize` in SOS, does the API support this?

These two commands are not something I would consider fundamental to the API. I
intentionally do not give a function which runs the equivalent of `!gcroot` and
`!objsize`. Instead, I have provided the building blocks for these functions so
that you can implement them yourself. Since it's actually tough to implement
these functions, I have provided reference examples of the algorithms here:
`!objsize` and `!gcroot`.

I do not implement these in the API because different people have different
requirements for these functions. For example, you may want a list of objects
(with aggregate type statistics) when running `!objsize`. Others might only want
a total size and number of objects. Similarly for `!gcroot` some people only
want one path from a root to the target object, others might want all paths, or
only the shortest path, etc.

I have provided reference implementations of both so they can be modified to
suit your needs.

## How do I get the MethodTable of an object?

I purposefully do not expose the MethodTable of any given object for a few
reasons:

1. This is an implementation detail of CLR you should not take a dependency on.
2. MethodTables are per-appdomain. This means if you use a MethodTable as a type
   identifier you will have to deal with the fact that there are multiple
   `System.Foo.Bar` method tables for that type. Contrast this with
   `GCHeapType`, which has only one instance per class in the process.
3. Everything you would need to do with a MethodTable is neatly wrapped into
   `GCHeapType`.

## How do I get all pinned objects in the process?

Walk the handle table and filter it by
`GCHeapHandle.Type == HandleTypes.Pinned`. Objects returned here are pinned by
the runtime (meaning they will not be reloacted by the GC until unpinned).
