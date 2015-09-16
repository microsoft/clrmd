# Getting Started

This tutorial introduces you to the concepts of working with
`Microsoft.Diagnostics.Runtime.dll` (called 'ClrMD' for short), and the
underlying reasons why we do things the way we do. If you are already familiar
with the dac private API, you should skip down below to the code which shows you
how to create a ClrRuntime instance from a crash dump and a dac.

## CLR Debugging, a brief introduction

All of .Net debugging support is implemented on top of a dll we call "The Dac".
This file (usually named `mscordacwks.dll`) is the building block for both our
public debugging API (ICorDebug) as well as the two private debugging APIs: The
SOS-Dac API and IXCLR.

In a perfect world, everyone would use `ICorDebug`, our public debugging API.
However a vast majority of features needed by tool developers such as yourself
is lacking from `ICorDebug`. This is a problem that we are fixing where we can,
but these improvements go into CLR v.next, not older versions of CLR. In fact,
the `ICorDebug` API only added support for crash dump debugging in CLR v4.
Anyone debugging CLR v2 crash dumps cannot use `ICorDebug` at all!

The other two debugging APIs suffer from a different problem. They can be used
on a crash dump, but these APIs are considered private. Any tool which builds on
top of these APIs cannot be shipped publically. This is due to Microsoft policy
that we only ship tools based on public APIs (unless you are the team which owns
the private API, thus CLR can ship SOS without a problem, since CLR owns both
the private SOS-Dac API and SOS itself).

The second problem with the two private debugging APIs is that they are
incredibly difficult to use correctly. Writing correct code on top of the SOS-
Dac API requires you to basically understand the entirety of CLR's internals.
For example, how a GC Segment is laid out, how object references inside an
object can be found, and so on.

ClrMD is an attempt to bridge the gap between private APIs and tool writers. The
API itself is built on top of the Dac private API, but abstracts away all of the
gory details you are required to know to use them successfully. Better yet,
ClrMD is a publically shipping, documented API. You may build your own programs
on top of it and ship them outside of Microsoft (which is not true if you build
on top of the raw Dac private APIs).

## What do I need to debug a crash dump with ClrMD?

As mentioned before, all .Net debugging is implemented on top of the Dac. To
debug a crash dump or live process, all you need to do is have the crash dump
and matching `mscordacwks.dll`. Those are the only prerequisites for using this
API.

To find the correct dac for a particular crash dump can be obtained through a
simple symbol server request. See the later Getting the Dac from the Symbol
Server section below for how to do this.

There is one other caveat for using ClrMD though: ClrMD must load and use the
dac to do its work. Since the dac is a native DLL, your program is tied to the
architecture of the dac. This means if you are debugging an x86 crash dump, the
program calling into ClrMD must be running as an x86 process. Similarly for an
amd64 crash dump. This means you will have to relaunch your tool under wow64 (or
vice versa) if you detect that the dump you are debugging is not the same
architecture as you currently are.

## Loading a crash dump

To get started the first thing you need to do is to create a `DataTarget`. The
`DataTarget` class represents a crash dump or live process you want to debug
(though as of Beta 0.3, only crash dumps are supported). To create an instance
of the `DataTarget` class, call one of the static functions on `DataTarget`.
Here is the code to create a `DataTarget` from a crash dump:

    using (DataTarget target = DataTarget.LoadCrashDump(@"c:\work\crash.dmp"))
    {
    }

The `DataTarget` class has two primary functions: Getting information about what
runtimes are loaded into the process and creating `ClrRuntime` instances.

To enumerate the versions of CLR loaded into the target process, use
`DataTarget.ClrVersions`:

    foreach (ClrInfo version in target.ClrVersions)
    {
        Console.WriteLine("Found CLR Version:" + version.Version.ToString());

        // This is the data needed to request the dac from the symbol server:
        ModuleInfo dacInfo = version.DacInfo;
        Console.WriteLine("Filesize:  {0:X}", dacInfo.FileSize);
        Console.WriteLine("Timestamp: {0:X}", dacInfo.TimeStamp);
        Console.WriteLine("Dac File:  {0}", dacInfo.FileName);

        // If we just happen to have the correct dac file installed on the machine,
        // the "TryGetDacLocation" function will return its location on disk:
        string dacLocation = version.TryGetDacLocation();
        if (!string.IsNullOrEmpty(dacLocation))
            Console.WriteLine("Local dac location: " + dacLocation);

        // You may also download the dac from the symbol server, which is covered
        // in a later section of this tutorial.
    }

Note that `target.ClrVersions` is an `IList<ClrInfo>`. We can have two copies of
CLR loaded into the process in the side-by-side scenario (that is, both v2 and
v4 loaded into the process at the same time).

As you can see, ClrInfo has information which will help you track down the
correct dac to load. Once you have found the correct location of the dac (or
downloaded it from the symbol server), you then create an instance of ClrRuntime
with the full path to the dac:

    ClrRuntime runtime = target.CreateRuntime(@"C:\work\mscordacwks.dll");

We will cover what to actually do with a `ClrRuntime` object in the next few
tutorials.

## Attaching to a live process

CLRMD can also attach to a live process (not just work from a crashdump). To do
this, everything is the same, except you call `DataTarget.AttachToProcess`
instead of "LoadCrashDump". For example:

    DataTarget dt = DataTarget.AttachToProcess(0x123, AttachFlags.Noninvasive, 5000);

The parameters to the function are: the pid to attach to, the type of debugger
attach to use, and a timeout to use for the attach.

There are three different `AttachFlags` which can be used when attaching to a
live process: Invasive, Noninvasive, and Passive. An Invasive attach is a normal
debugger attach. Only one debugger can attach to a process at a time. This means
that if you are already attached with Visual Studio or an instance of Windbg, an
invasive attach through CLRMD will fail. A non-invasive attach gets around this
problem. Any number of non-invasive debuggers may be attached to a process. Both
an invasive and non-invasive attach will pause the debugee (so this means VS's
debugger will not function again until you detatch). The primary difference
between the two is that you cannot control the target process or receive debug
notifications (such as exceptions) when using a non-invasive attach.

To be clear though, the difference between an invasive and non-invasive attach
doesn't matter to CLRMD. It only matters if you need to control the process
through the IDebug interfaces. If you do not care about getting debugger events
or breaking/continuing the process, you should choose a non-invaisve attach.

One last note on invasive and non-invasive, is that managed debuggers (such as
ICorDebug, and Visual Studio) cannot function when when something pauses the
process. So if you attach to a process with a Noninvasive or Invasive attach,
Visual Studio's debugger will hang until you detach.

A "Passive" attach does not involve the debugger apis at all, and it does not
pause the process. This means things like Visual Studio's debugger will continue
to function. However, if the process is running, you will get highly
inconsistent information for things like heap data, as the target process is
continuing to run as you attempt to read data from it.

In general, you should use a Passive attach if you are using CLRMD in
conjunction with another debugger (like Visual Studio), and only when that
debugger has the target process paused. You should use an invasive attach if you
need to control the target process. You should use a non-invasive attach for all
other uses of the API.

## Detatching from a process or dump

DataTarget implements the `IDisposable` interface. Every instance of
`DataTarget` should be wrapped with a using statement (otherwise you should find
a place to call `Dispose` when you are done using the `DataTarget` in your
application).

This is important for two reasons. First, any crash dump you load will be locked
until you dispose of `DataTarget`. Second, and more importantly is the live
process case. For a live process, ClrMD acts as a real debugger, which has a lot
of implications in terms of program termination. Primarily, if you kill the
debugger process without detaching from the target process, Windows will kill
the target process. Calling `DataTarget`'s `Dispose` method will detatch from
any live process.

DataTarget itself has a finalzier (which calls `Dispose`), and this will be run
if the process is terminated normally. However I highly recommend that your
program eagerly call `Dispose` as soon as you are done using ClrMD on the
process.

## Getting the Dac from the Symbol Server

ClrMD has a method for automatically downloading the dac from the public (or
internal) symbol servers. With no parameters, the following code will attempt to
download the correct dac from the public symbol server:

    // We only care about one version of CLR for this example
    ClrInfo clrVersion = dataTarget.ClrVersions[0];
    string dacLocation = clrVersion.TryDownloadDac();

This will download the Dac, store it in a cache in the user's AppData temp
folder, returning the path on disk to the location of the Dac. You can this pass
'dacLoation' to dataTarget.CreateRuntime. Note: This can fail if the dac isn't
on the symbol server (or due to network problems), at which point this function
will return null.

You may want a bit more control over this though (such as not just downloading
from the public symbol server, but also using microsoft's internal symbol
servers. You may also specify where to cache the files. This is controled by the
symbolPath parameter, and defaultLocalCache parameters. Here is an example, but
please see the intellisense documentation on TryDownloadDac's overrides for
further information these parameters and behavior:

    string dacLocation = clrVersion.TryDownloadDac("SRV*http://symweb.corp.microsoft.com", @"c:\symcache");

One last note, if your tool is shipped publically, you want to use the default
public symbol server `SRV*http://msdl.microsoft.com/download/symbols`. If you
are attempting to write a tool that takes advantage of the internal Microsoft
symbol server, I would suggest using this code:

    string dacLocation = clrVersion.TryDownloadDac(GetSymbolPath());

Where `GetSymbolPath` is defined as:

    static string GetSymbolPath()
    {
        string path = null;
        if (ComputerNameExists("symweb.corp.microsoft.com"))
            path = "SRV*http://symweb.corp.microsoft.com";   // Internal Microsoft location.
        else
            path = "SRV*http://msdl.microsoft.com/download/symbols";

        if (ComputerNameExists("ddrps.corp.microsoft.com"))
            path = path + ";" + @"SRV*\\ddrps.corp.microsoft.com\symbols";
        if (ComputerNameExists("clrmain"))
            path = path + ";" + @"SRV*\\clrmain\symbols";

        return path;
    }

    public static bool ComputerNameExists(string computerName, int timeoutMSec = 700)
    {
        try
        {
            System.Net.IPHostEntry ipEntry = null;
            var result = System.Net.Dns.BeginGetHostEntry(computerName, null, null);
            if (result.AsyncWaitHandle.WaitOne(timeoutMSec))
                ipEntry = System.Net.Dns.EndGetHostEntry(result);
            if (ipEntry != null)
                return true;
        }
        catch (Exception) { }
        return false;
    }

This should find most symbols for internal builds when inside corpnet.

## Putting it all together

We have covered the building blocks of creating a `DataTarget` and a
`ClrRuntime` instance. Here is everything all together:

    static ClrRuntime CreateRuntime(DataTarget dataTarget, string dacLocation = null)
    {

        // Only care about one CLR in this example.
        ClrInfo version = dataTarget.ClrVersions[0];
        if (string.IsNullOrEmpty(dacLocation))
        {
            // TryDownloadDac will also check to see if you have the matching
            // mscordacwks installed locally, so you don't need to also check
            // TryGetDacLocation as well.
            dacLocation = version.TryDownloadDac(GetSymbolPath());
        }

        if (string.IsNullOrEmpty(dacLocation) || !File.Exists(dacLocation))
            throw new FileNotFoundException(string.Format("Could not find .Net Diagnostics Dll {0}", version.DacInfo.FileName));

        return dataTarget.CreateRuntime(dacLocation);
    }

Calling this code is as simple as:

    DataTarget target = DataTarget.LoadCrashDump(@"c:\test\crash.dmp");
    ClrRuntime runtime = CreateRuntime(target);
    // or: ClrRuntime runtime = CreateRuntime(target, @"c:\dac_cache\mscordacwks.dll");

The next tutorial will cover some basic uses of the `ClrRuntime` class.

Next Tutorial: [The ClrRuntime Object](ClrRuntime.md)