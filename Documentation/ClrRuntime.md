# ClrRuntime, the core of ClrMD

## Introduction

In the last tutorial we covered how to load a crash dump. In this tutorial we
will look at the `ClrRuntime` class, the entrypoint and core of this API.

The `ClrRuntime` object itself represents the data for a single version of CLR
loaded into the process. In the SxS scenario (when both v2 and v4 are loaded
into the same process), you can have up to two of these at any given time.

In this tutorial we will cover how to get data about the targeted runtime such
as enumerating AppDomains, Threads, and the Memory Regions CLR has allocated, as
well as the GC Handle table and finalizer queue.

## Very Basic Operations

`ClrRuntime` implements a handful of grab-bag operations which you can use to
get some bits of information about the runtime. The first (and simplest) is that
`ClrRuntime`.PointerSize returns to you the size of a pointer in the target
process. All functions in ClrMD use a ulong (64bit int) everytime a pointer
would be required, but this field tells you what bitness the target process is.

There are two GC related properties on `ClrRuntime`:

`ClrRuntime.ServerGC` should be clear from the name: It returns `true` if the
target process was running the Server GC. Otherwise it returns `false` if the
process was running a Workstation GC.

`ClrRuntime.HeapCount` returns the number of logical GC heaps in the process.
For a workstation GC, there is only ever 1 logical GC heap. For a server GC,
there may be any number of logical GC heaps in the process (though in reality,
we generally keep the number of heaps at the number of cores on the box...with a
few exceptions). This is important because if your logical GC heaps become
imbalanced (meaning one heap has MUCH more data than another heap) then this
will quickly become a perf problem in your server application. We will cover
more about GC heap imbalance in the tutorial section covering heap walking.

`ClrRuntime` also gives you some very simple functions to read data out of the
target process. Currently it exposes two functions. ReadVirtual allows you to
read a raw buffer out of the target process. ReadPtr is special in that it will
only read a single pointer out of the target process (either 4 or 8 bytes
depending on the architecture).

Lastly, `ClrRuntime` has 3 static functions for telling whether a
`ClrElementType` (an enum we'll cover in a later tutorial about types and
fields) is a primitive, object reference, or value class (struct).

That's it for the most basic functions in `ClrRuntime`. Now off to the more
structured data...

## AppDomains

AppDomains in ClrMD are (for now!) one of the simplest data structures to walk.
This is because as of Beta 0.3, we do not expose a lot of information about
them. Future versions of ClrMD will be able to enumerate modules loaded into the
AppDomain as well as more information about the state of the AppDomain.

### Enumerating AppDomains

To walk AppDomains in the process, simply use the `ClrRuntime.AppDomains`
property:

    ClrRuntime runtime = ...;
    foreach (ClrAppDomain domain in runtime.AppDomains)
    {
        Console.WriteLine("ID:      {0}", domain.Id);
        Console.WriteLine("Name:    {0}", domain.Name);
        Console.WriteLine("Address: {0}", domain.Address);
    }

This is the entirety of what you can do with AppDomains (as of Beta 0.3). The
primary reason for exposing AppDomains at all is to be able to get at the name
of the AppDomain. Other data in ClrMD contains AppDomain data (for example,
memory regions, which we will cover later). The intent is that when we have
AppDomain information, you can display either the AppDomain ID or Name
associated with it.

## Threads

Thankfully, you can do a lot more with threads in ClrMD than you can AppDomains.
Similar to `ClrRuntime.AppDomains`, there is a `ClrRuntime.Threads` property,
which holds the current list of all managed threads in the process.

The `CLRThread` class contains data such as: The address of the thread object,
the AppDomain which the topmost frame is running in, the last thrown exception
on that thread, a managed stack trace of the thread, the stack base and limit of
the thread, and so on.

The most interesting of which is stack walking, so we'll start there.

### Walking the stack

You can walk the managed stack of any thread in the process by simply
enumerating the `ClrThread.StackTrace` property. Here's an example of printing
out the call stack for each thread in the process:

    foreach (ClrThread thread in runtime.Threads)
    {
        if (!thread.IsAlive)
            continue;

        Console.WriteLine("Thread {0:X}:", thread.OSThreadId);

        foreach (ClrStackFrame frame in thread.StackTrace)
            Console.WriteLine("{0,12:X} {1,12:X} {2}", frame.StackPointer, frame.InstructionPointer, frame.ToString());

        Console.WriteLine();
    }

Note that to get the class/function name that we are currently executing,
calling `ClrStackFrame.ToString` will give you output roughly equivalent to
SOS's `!ClrStack`.

### Caveats

There are a few caveats to `ClrThread` objects which you should be aware of:

First, we only create `ClrThread` objects (internally this is the clr!Thread
object in clr.dll) for threads which have run managed code, or might soon run
managed code. This means that there is NOT a 1-1 mapping of all threads in the
process to a `ClrThread` object. If a thread has never run managed code before,
chances are there's not a `ClrThread` for it.

Second, we keep these clr!Thread objects around for a short while after the
actual underlying OS thread has died. Since `ClrThread` is basically a wrapper
around clr!Thread, we expose that out here. The `ClrThread.IsAlive` property
tells you whether a thread is alive (meaning the thread is still active and
running in the process) or dead (the underlying OS thread has been terminated
and thus this thread is no longer running anymore).

Third, there are many cases where we cannot get a complete managed stack trace.
Without getting into too much detail here, the process could have been stopped
in a state where our stackwalker gets confused and cannot properly walk the
managed stack. However, you are gauaranteed the same results that SOS gets in
this case, so it should not be too bad.

Fourth, you will notice that there is an `AppDomain` property on `ClrThread`.
This is the AppDomain in which the topmost frame of the stack is running in, not
what every stack frame is running in. The Dac private API is limited in that it
cannot give us per-frame AppDomain information, so ClrMD cannot give you that
information.

## Enumerating CLR Memory Regions

CLR can allocate a lot of memory over the lifetime of a process. When debugging
memory issues, a common question is "where did all of my memory go?" To help
answer that question, ClrMD provides the `ClrRuntime.EnumerateMemoryRegions`
function. (This data is similar to SOS's `!EEHeap` command, but contains more
data.)

This function enumerates `CLRMemoryRegion` objects which describe a region of
memory that CLR knows about. For each memory region enumerated you can get the
starting address of the region, the length of the region, and what type of
memory resides there (for example, GC Heap segment, JIT code segment, etc).
Additionally, ClrMD also provides some extra information depending on the type
of memory region. For example, some memory regions have an AppDomain associated
with it, some memory regions have a module (dll/exe) associated with it, and GC
segments have a heap number as well as a flag indicating whether the segment is
Ephemeral, Regular, or a LargeObject segment.

Not all memory regions have all pieces of data. For example, GC Segments do not
have an AppDomain associated with them (as GC Segments are not per-AppDomain,
memory in the GC is mixed together, not segregated by AppDomain).

The intent of providing this data is to allow you to do a few things:

1. If you are displaying a list of all memory in the process, this allows you to
   annotate certain ones as CLR owned (and what exactly they are).

2. If you are attempting to find out what is eating all the memory in your
   process, you can use aggregate statistics of this data to see what types of
   CLR heaps are the largest in the process.

3. In general, only the GC Segments (and Reserve GC Segments) should really be
   eating much memory in your process. The total memory for each other type of
   heap in your process should be less than 50 megs (slightly more for very
   large dumps). If something is eating more than 100 megs of memory (and it's
   not a GC Segment) then that's a red flag for investigation.

Here is an example of using linq to build a useful table of what is eating the
most memory in the process:

    foreach (var region in (from r in runtime.EnumerateMemoryRegions()
                            where r.Type != ClrMemoryRegionType.ReservedGCSegment
                            group r by r.Type into g
                            let total = g.Sum(p => (uint)p.Size)
                            orderby total descending
                            select new
                            {
                                TotalSize = total,
                                Count = g.Count(),
                                Type = g.Key
                            }))
    {
        Console.WriteLine("{0,6:n0} {1,12:n0} {2}", region.Count, region.TotalSize, region.Type.ToString());
    }

Note that I have eliminated `ReservedGCSegments` from this table. That's because
`ReserveGCSegments` are just that...reserved memory which isn't counting toward
your workingset.

## A quick word about Enumerators vs. Properties in ClrMD

As you have probably noticed, `ClrRuntime.Threads` and `ClrRuntime.AppDomains`
are properties, yet `ClrRuntime.EnumerateMemoryRegions` is a function (which
returns an enumerator instead of a collection/list/set). Why is that?

ClrMD follows the convention that anything it can quickly calculate (and then
likely cache), it exposes through properties. Anything which is expected to take
a long time to calculate or enumerate is exposed through a function, like
EnumerateMemoryRegions. For example, on significantly large dumps,
EnumerateMemoryRegions can take up to a few seconds to complete (compared to
only a few hundred milliseconds for getting the threads/AppDomains in a
process).

The long story short is, if you are getting data out of a property, it's safe to
continually refer back to that property. ClrMD will do the right thing here and
cache the data so your perf stays good. However, anytime you are calling into a
function which gives you a list/enumeration back to you, you should be careful
to only enumerate that data once and cache what you need out of it or you may
pay a hefty perf penalty.

## Enumerating the FinalizerQueue

The finalizer queue in CLR is a list of objects which have been collected and
will soon have their finalizer run. Enumerating all objects on the finalizer
queue is as simple as calling

        foreach (ulong obj in runtime.FinalizerQueue)
        {
        }

Of course, at this point I have not shown you what you can do with object
addresses in ClrMD. That will come in the next tutorial
[Enumerating the Heap](WalkingTheHeap.md).

## Enumerating GC Handles in the Process

The last thing we will cover in this tutorial is how to walk the GC Handle
table. A GC Handle consists of three basic building blocks: An object, a handle
which can be used to refer to this object (without knowing its exact address),
and a handle type which specifies how the runtime treats the handle.

Here is an example of walking the GC Handle table and printing out a table of
handles and their types:

    foreach (GCHeapHandle handle in runtime.EnumerateHandles())
    {
        string objectType = heap.GetObjectType(handle.Object).Name;
        Console.WriteLine("{0,12:X} {1,12:X} {2,12} {3}", handle.Address, handle.Object, handle.Type.ToString(), objectType);
    }

Note this example doesn't do anything with types of objects on the heap. Please
see the `GCHandles` code snippet for a different way of breaking down handles.

### Types of GC Handles

Each handle type is either weak (meaning it does not keep the object alive from
the GC's perspective) or strong (meaning as long as the handle exists we will
not collect the object). RefCount handles are the exception: They are
conditionally strong or week depending on whether the RefCount is 0 or greater
than 0. You can tell if a particular handle is strong with the
`GCHeapHandle.Strong` property.

There are 9 handle types, each with a special meaning:
1. **WeakShort** - A weak handle which is meant to be short lived in the
   process.
2. **WeakLong** - A weak handle which is meant to be long lived in the process.
3. **Strong** - A strong handle.
4. **Pinned** - A strong handle which prevents the GC from relocating the
   object.
5. **AsyncPinned** - A strong handle used by async operations. This is also a
   pinning handle, meaning the GC is not allowed to relocate the object this
   points to.
6. **Variable** - An usused handle type. You should never see one of these.
7. **RefCount** - A reference counted strong handle (used for COM interop).
   When the RefCount hits 0 the handle becomes a weak handle.
8. **Dependent** - A weak handle. Please see below.
9. **SizedRef** - A strong handle used by ASP.Net.

The last three, RefCount handles, Dependent handles, and SizedRef handles each
have caveats that you should be aware of.

### RefCount Handles

RefCount handles are used for COM interop, and obviously one of the most
important things to know about a RefCount handle is the actual refcount itself.
For v4 and v4.5 CLR, we have that data. However, in v2 CLR we did not expose the
reference count out through the Dac. This means that if you are debugging a v2
process we do not have a reference count we can give you. The
GCHeapHandle.RefCount field will always return 1. This means ClrMD always treats
RefCount handles as Strong handles in CLR v2. This is mostly true, RefCount
handles get cleaned up when their count hits 0. So the only time you would get a
false-positive in v2 is if the crash dump was taken after the real ref count hit
0 and before the next GC cleaned it up.

### SizedRef Handles

SizedRef handles are used exclusively by Asp.Net to hold onto caches of objects.
As the GC marks objects it keeps track of what objects the handle keeps alive.
When the GC is complete it updates a field on the SizedRef handle which is an
estimate of how much memory that object holds onto. This is similar to a
inaccurate `!objsize`.

Unfortunately the Dac does not expose any way to get the size which the SizedRef
handle holds onto, so neither does ClrMD.

### Dependent Handles

Dependent handles are a very complicated topic. They were added in v4 as a way
to add an edge to the GC graph. A dependent handle consists of the handle
address, the object that the handle refers to (I'll call this the "source"
object), and a "target" object. A dependent handle keeps the "target" object
alive if the "source" object is alive. However, the handle itself is a weak
handle, meaning it does not root the "source" object.

Users can create dependent handles through the
System.Runtime.CompilerServices.ConditionalWeakTable class.

The major caveat here is that dependent handle support was not added to the dac
until CLR v4.5. This means that on CLR v4 runtimes, you will know if there are
dependent handles in the process, and what the "source" object is, but you
cannot get the "target" object unless you are running on CLR v4.5.

All of this is important because if you are attempting to write the equivalent
code to `!objsize` or `!gcroot`, you must take dependent handles into account. A
dependent handle adds real edges to the graph and need to be treated as any
other object reference to get accurate results.

### A word about v2 and v4 CLR handle walking

One last note about the Handles you get in ClrMD: The GC handle table data you
get will be incomplete on v2 and v4. In v2 there was a very nasty bug in the Dac
which causes us to miss a lot of handles on the handle table. We fixed this bug
before v4 CLR shipped, but found another (much less severe) bug which affects
handle table walking causing us to miss some handles in a corner case. In v4.5
we finally cleaned up walking the HandleTable once and for all, replacing the
old algorithm with the correct way of enumerating handles: Asking CLR to do it
for us instead of mimicing what CLR does.

The long story short is, if you are on v2, you very likely to be not getting all
of the handles. On v4, it's possible that you are getting most (but not all) of
the handles. On v4.5, you get 100% of the handles on the handle table. Note that
this is a fundamental bug in the dac which we cannot work around (SOS and PSSCOR
is also affected by this issue).

## Conclusion

In this tutorial we covered the `ClrRuntime` object and the data it provides.
Please keep in mind the difference between properties in ClrMD (fast, you can
keep referring back to the property) and functions which produce a list or
enumeration (slow, you should cache the result and not call again).

Next tutorial: [Walking the Heap](WalkingTheHeap.md)