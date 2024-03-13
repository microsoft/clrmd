# Frequently Asked Questions (FAQs)

## What happened to ClrRuntime.EnumerateTypes?

We don't actually have a way to enumerate all "types" in the process.  ClrHeap.EnumerateTypes was an algorithm designed to help you find constructed types.  This was removed in ClrMD 2.0 because it's incredibly slow and is confusing as to what it's actually doing.  The Microsoft.Diagnostics.Runtime.Utilities NuGet package provides an extension method to add it back, but you can simply implement the algorithm yourself to do this.  The source for doing this can be found here:  https://github.com/microsoft/clrmd/blob/master/src/Microsoft.Diagnostics.Runtime.Utilities/EnumerateTypesExtension.cs.

## Why are static roots no longer enumerated by `ClrHeap.EnumerateRoots`?

Static variables are not "roots" in the strictest sense.  They are always rooted, but the GC does not consider them to be roots.  ClrMD 1.1 would report these as roots because it's convenient to treat them as roots when reporting to the user why an object is alive.  However, that code takes a very long time, and it wasn't providing an accurate view of the runtime.  If you need those back or to find what object addresses are you can use this method (or reimplement it yourself): https://github.com/microsoft/clrmd/blob/master/src/Microsoft.Diagnostics.Runtime.Utilities/StaticRootsExtension.cs.

## What platforms are supported?

ClrMD is fully supported on Windows and Linux.  We are currently working on OS X support but there is no ETA for when this will be complete.

## Can I use this API to inspect my own process?

Yes.  ClrMD allows you to create a snapshot of a running process and attach to that snapshot using `DataTarget.CreateSnapshotAndAttach`.  On Windows we use the `PssCreateSnapshot` API which is relatively fast to take an in-memory snapshot of a live process to debug.  On Linux (and eventually OS X when that is supported) you can also use `CreateSnapshotAndAttach` but since there isn't a fast in-memory OS API to take a snapshot we will temporarily drop a coredump to disk and "attach" to that, then delete the temporary coredump when `DataTarget.Dispose` is called.

Attaching to your own, unsuspended process using `DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, suspend: false)` is **not** supported.  In general, inspecting any running process that is not suspended is not supported, and we likely won't attempt to fix any bugs or issues in that scenario.

## Why do I need to match architecture of my process to the dump?

This library works by loading the private CLR debugging library
(mscordacwks.dll) into the process. This is a native DLL which is tied to the
architecture of the dump you are debugging. Unfortunately by using that native
DLL the C# code calling into CLR MD is then tied to the same architecture
requirement.

Theoretically you could get around this requirement yourself in a few ways. For
example, you can wrap your calls to the API into a separate process, then use
interprocess communication to relay the information. I do not plan on adding
anything to the API to do this automatically though.

## I am receiving `UnauthorizedAccessException` when attaching to a Linux process

You need `ptrace` capabilities to attach to the target process. The following are different ways to grant access to the inspecting process:
- Run the inspecting process as root.
- Run the target process from the inspecting process.
- If you are trying to [inspect your own process](#can-i-use-this-api-to-inspect-my-own-process), you may perform a P/Invoke call to `prctl` with the inspecting process ID and the `PR_SET_PTRACER` option.

