# Frequently Asked Questions (FAQs)

## What platforms are supported?

ClrMD is fully supported on Windows and Linux.

## Can I use this API to inspect my own process?

Using ClrMD to inspect its own process is not supported and not recommended.  The library does not prevent you from attaching to your own process, and some functionality may work but none of the CLR Diagnostics API that ClrMD was built on top of expects to be inspecting an "live" (un-suspended) process.

The fundamental problem here is data consistency.  The CLR runtime uses locks and other mechanisms to ensure that it sees a consistent view of the world (like any software project, really) and the CLR debugging layer (mscordaccore.dll) ignores those locks by design.  This means that if you are inspecting an unsuspended process you can get all kinds of weird behavior.  Exceptions, infinite loops, etc.

We only support inspecting suspended processes, which obviously won't work with You are always required to suspend a live process before inspecting it, which obviously doens't work for your own process.  (Note that passing AttachFlags.Invasive and AttachFlags.NonInvasive suspend the process on your behalf, only AttachFlags.Passive does not.)


## Does this work with any architecture? (x86/x64?)

Yep, this API works with crash dumps of both x86 and amd64 processes.

However, you must match the architecture of your program to the crash dump you
are reading. Meaning if you are debugging an x86 crash dump, your program must
run as an x86 process, similar for an x64 dump, you need an x64 process. This is
usually done with the /platform directive to C#.

## Why do I need to match architecture of my process to the dump?

This library works by loading the private CLR debugging library
(mscordacwks.dll) into the process. This is a native DLL which is tied to the
architecture of the dump you are debugging. Unfortunately by using that native
DLL the C# code calling into CLR MD is then tied to the same architecture
requirement.

Theoretically you could get around this requirement yourself in a few ways. For
example, you can wrap your calls to the API into a seperate process, then use
interprocess communication to relay the information. I do not plan on adding
anything to the API to do this automatically though.

## I am receiving `UnauthorizedAccessException` when attaching to a Linux process

You need `PTRACE` capabilities (see `CAP_SYS_PTRACE`) and various associated
security checks passing in order to attach to a Linux process. One option to
pass the checks is running as root. If however you are just trying to avoid the
problems mentioned above regarding inspecting your own process then you may want
to start the inspecting process from the process to be inspected. In this case
you can use the ability to explicitely grant another process access by doing a
P/Invoke call for `prctl` with the `PR_SET_PTRACER` option.
