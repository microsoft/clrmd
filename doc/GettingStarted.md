# Getting Started

This tutorial introduces you to the concepts of working with
`Microsoft.Diagnostics.Runtime.dll` (called 'ClrMD' for short), and the
underlying reasons why we do things the way we do. If you are already familiar
with the dac private API, you should skip down below to the code which shows you
how to create a `ClrRuntime` instance from a crash dump and a dac.

## CLR Debugging, a brief introduction

All of .NET debugging support is implemented on top of a dll we call "The Dac" (usually named `mscordacwks.dll`).  This library is the building block for our private debugging APIs which should not be directly used.  The dotnet runtime team has implemented several public APIs using these private ones for anyone to consume.  This includes the ClrMD library, which is a process and runtime inspection API as well as `ICorDebug`, the official debugger API for CLR.  Note that ClrMD is not a debugging api and does not support setting breakpoints, stepping, and so on.

## What do I need to debug a crash dump with ClrMD?

As mentioned before, all .NET debugging is implemented on top of the Dac. To debug a crash dump or live process, all you need to do is have the crash dump and matching `mscordacwks.dll`. Those are the only prerequisites for using this API.

The correct dac for a particular crash dump can be obtained through a simple symbol server request. See the later Getting the Dac from the Symbol Server section below for how to do this.

There is one other caveat for using ClrMD though: ClrMD must load and use the dac to do its work. Since the dac is a native DLL, your program is tied to the architecture of the dac. This means if you are debugging an x86 crash dump, the program calling into ClrMD must be running as an x86 process. Similarly for an amd64 crash dump. This means you will have to relaunch your tool under wow64 (or vice versa) if you detect that the dump you are debugging is not the same architecture as you currently are.

## Loading a crash dump

To get started the first thing you need to do is to create a `DataTarget`. The `DataTarget` class represents a crash dump or live process you want to debug. To create an instance of the `DataTarget` class, call one of the static functions on `DataTarget`.  Here is the code to create a `DataTarget` from a crash dump:

```cs
using (DataTarget dataTarget = DataTarget.LoadDump(@"c:\work\crash.dmp"))
{
}
```

The `DataTarget` class is designed to give you a whole view of the process with some limited data about what modules (both managed and native) are loaded into the process.  It's main use though is to enumerate what instances of the CLR runtime are loaded into the process and using that information to create `ClrRuntime` instances.

To enumerate the versions of CLR loaded into the target process, use `DataTarget.ClrVersions`:

```cs
foreach (ClrInfo version in dataTarget.ClrVersions)
{
    Console.WriteLine("Found CLR Version: " + version.Version);

    // This is the data needed to request the dac from the symbol server:
    DacInfo dacInfo = version.DacInfo;
    Console.WriteLine("Filesize:  {0:X}", dacInfo.IndexFileSize);
    Console.WriteLine("Timestamp: {0:X}", dacInfo.IndexTimeStamp);
    Console.WriteLine("Dac File:  {0}", dacInfo.PlatformSpecificFileName);

    // If we just happen to have the correct dac file installed on the machine,
    // the "LocalMatchingDac" property will return its location on disk:
    string dacLocation = version.DacInfo.LocalDacPath;
    if (!string.IsNullOrEmpty(dacLocation))
        Console.WriteLine("Local dac location: " + dacLocation);

    // Note that you can also simply download the dac from the symbol server
    // which is usually done automatically for you.  This will be covered more
    // later.
}
```

Note that `dataTarget.ClrVersions` is an `ImmutableArray<ClrInfo>`. We can have multiple copies of CLR loaded into the process in the side-by-side scenario such as having a copy of the desktop CLR loaded and .Net Core loaded into the process at the same time.

The next step to getting useful information out of ClrMD is to construct an instance of the `ClrRuntime` class.  This class represents one CLR runtime in the process.  To create one of these classes, use `ClrInfo.CreateRuntime` and you will create the runtime for the selected version:

```cs
ClrInfo runtimeInfo = dataTarget.ClrVersions[0];  // just using the first runtime
ClrRuntime runtime = runtimeInfo.CreateRuntime();
```

Note that we did not specify the location of the dac library when calling here.  ClrMD will default to looking at the `LocalDacPath` and use it if it is non-null.  If the `_NT_SYMBOL_PATH` environment variable is set we will also check the symbol servers listed for a matching dac.  To use the Microsoft symbol server you can set `_NT_SYMBOL_PATH=https://msdl.microsoft.com/download/symbols`.

You can also create a runtime from a dac location on disk if you know exactly where it is:

```cs
ClrInfo runtimeInfo = dataTarget.ClrVersions[0];  // just using the first runtime
ClrRuntime runtime = runtimeInfo.CreateRuntime(@"C:\path\to\mscordacwks.dll");
```

## Getting the Dac from the Symbol Server

When you call CreateRuntime without specifying the location of mscordacwks.dll, ClrMD attempts to locate the dac for you.  It does this through a few mechanisms, first it checks to see if you have the same version of CLR that you are attempting to debug on your local machine.  If so, it loads the dac from there.  (This is usually at c:\windows\Framework[64]\[version]\mscordacwks.dll.)  If you are debugging a crash dump that came from another computer, you will have to find the dac that matches the crash dump you are debugging.

All versions of the dac are required to be on the Microsoft public symbol server, located here:  https://msdl.microsoft.com/download/symbols.  The DataTarget.SymbolLocator property is how ClrMD interacts with symbol servers.  If you have set the _NT_SYMBOL_PATH environment variable, ClrMD will use that string as your symbol path.  If this environment variable is not set, it will default to the Microsoft Symbol Server.

With any luck, you should never have to manually locate the dac or interact with DataTarget.SymbolLocator.  CreateRuntime should be able to successfully locate all released builds of CLR.

However, if you have built .Net Core yourself from source or are using a non-standard build, you will have to keep track of the correct dac yourself (these will not be on the symbol servers).  In that case you will need to pass the path of the dac on disk to ClrInfo.CreateRuntime manually.

## Attaching to a live process

CLRMD can also attach to a live process (not just work from a crashdump). To do this, everything is the same, except you call `DataTarget.AttachToProcess` instead of `DataTarget.LoadCrashDump`. For example:

```cs
DataTarget dataTarget = DataTarget.AttachToProcess(0x123, suspend: true);
```

The first parameter is the process id to attach to.  The second parameter tells ClrMD whether or not to suspend the target process when it attaches to the process.  Note that ClrMD **only** support inspecting a suspended process or a crash dump, we do *not* support inspecting a running process.  The reason we allow you to specify whether to suspend the process or not is so that diagnostic tool developers can do process suspension themselves without ClrMD interfering with that.

If you need to inspect a running process (or your own process) the only supported way to do this is to create a snapshot of the running process using `CreateSnapshotAndAttach` and inspect that snapshot instead.  Note that on Windows we use the PssCreateSnapshot api which will only leave process suspended a relatively short amount of time.  On Linux and OS X we have no such API to use, so we will create a temporary coredump from the live process and this may take significantly longer.

For example, this is how you would create a DataTarget capable of inspecting your own process.  Note that the target process will be left suspended for only the duration of the call to `CreateSnapshotAndAttach`.

```cs
using DataTarget dt = DataTarget.CreateSnapshotAndAttach(Process.GetCurrentProcess().Id);
```

Lastly, you should always be sure to dispose of the `DataTarget` instances you create.  If you do not call `DataTarget.Dispose` we may leave processes in suspended states, or we could leave behind temporary files on disk that we would otherwise have deleted.

# ClrRuntime, the core of ClrMD

## Introduction

The `ClrRuntime` object itself represents the data for a single version of CLR loaded into the process.  This is the "root" object that you will be working with most of the time when using ClrMD.  Please note that `ClrRuntime` is marked as `IDisposable`, so please be sure to call `Dispose` when you are finished using it.

This object allows you to inspect a wide variety of CLR constructs:  The handle table, the GC heap, the AppDomains loaded into the process, the loaded modules, and so on.  We'll be covering some of the caveats and gotchas of using the class in this section.

## AppDomains

### Enumerating AppDomains

To walk AppDomains in the process, simply use the `ClrRuntime.AppDomains`
property:

```cs
ClrRuntime runtime = ...;
foreach (ClrAppDomain domain in runtime.AppDomains)
{
    Console.WriteLine("ID:      {0}", domain.Id);
    Console.WriteLine("Name:    {0}", domain.Name);
    Console.WriteLine("Address: {0}", domain.Address);
}
```

You can see the modules loaded for each `ClrAppDomain` instance via the `Modules` property:

```c#
foreach (ClrModule module in domain.Modules)
{
    Console.WriteLine("Module: {0}", module.Name);
}
```

ClrRuntime also contains a helper method `EnumerateModules` which will walk all domains and enumerate all of their modules.  **Note:** ClrModule represents a single module loaded into an AppDomain.  This means that if there are three AppDomains loaded into the process and each AppDomain loads the "foo.dll" module, then `ClrRuntime.EnumerateModules` will enumerate three different instances of `ClrModule`, each of which representing "foo.dll".


## Threads

Similar to `ClrRuntime.AppDomains`, there is a `ClrRuntime.Threads` property, which holds the current list of all managed threads in the process.

The `CLRThread` class contains data such as: The address of the thread object, the AppDomain which the topmost frame is running in, the last thrown exception on that thread, a managed stack trace of the thread, the stack base and limit of the thread, and so on.

The most interesting of which is stack walking, so we'll start there.

### Walking the stack

You can walk the managed stack of any thread in the process by simply enumerating the `ClrThread.StackTrace` property. Here's an example of printing out the call stack for each thread in the process:

```cs
foreach (ClrThread thread in runtime.Threads)
{
    if (!thread.IsAlive)
        continue;

    Console.WriteLine($"Thread {thread.OSThreadId:x}:");

    foreach (ClrStackFrame frame in thread.EnumerateStackTrace())
        Console.WriteLine($"    {frame.StackPointer:x12} {frame.InstructionPointer:x12} {frame}");

    Console.WriteLine();
}
```

Note that to get the class/function name of a given stack frame, calling `ClrStackFrame.ToString` will give you output roughly equivalent to SOS's `!ClrStack`.

### Caveats

There are a few caveats to `ClrThread` objects which you should be aware of:

First, we only create `ClrThread` objects (internally this is the `clr!Thread` object in `clr.dll`) for threads which have run managed code, or might soon run managed code. This means that there is NOT a 1-1 mapping of all threads in the process to a `ClrThread` object. If a thread has never run managed code before, chances are there's not a `ClrThread` for it.

Second, we keep these `clr!Thread` objects around for a short while after the actual underlying OS thread has died. Since `ClrThread` is basically a wrapper around `clr!Thread`, we expose that out here. The `ClrThread.IsAlive` property tells you whether a thread is alive (meaning the thread is still active and
running in the process) or dead (the underlying OS thread has been terminated and thus this thread is no longer running anymore).

Third, there are many cases where we cannot get a complete managed stack trace.  Without getting into too much detail here, the process could have been stopped in a state where our stackwalker gets confused and cannot properly walk the managed stack. However, you are guaranteed the same results that SOS gets in
this case, so it should not be too bad.

Fourth, you will notice that there is an `AppDomain` property on `ClrThread`.  This is the AppDomain in which the topmost frame of the stack is running in, not what every stack frame is running in. The Dac private API is limited in that it cannot give us per-frame AppDomain information, so ClrMD cannot give you that information.

## Enumerating GC Handles in the Process

The last thing we will cover in this tutorial is how to walk the GC Handle table. A GC Handle consists of three basic building blocks: An object, a handle which can be used to refer to this object (without knowing its exact address), and a handle type which specifies how the runtime treats the handle.

Here is an example of walking the GC Handle table and printing out a table of handles and their types:

```cs
foreach (ClrHandle handle in runtime.EnumerateHandles())
{
    string objectType = heap.GetObjectType(handle.Object).Name;
    Console.WriteLine($"{handle.Address:x12} {handle.Object} {handle.HandleKind}");
}
```


### Types of GC Handles

Each handle type is either weak (meaning it does not keep the object alive from the GC's perspective) or strong (meaning as long as the handle exists we will not collect the object). RefCount handles are the exception: They are conditionally strong or weak depending on whether the RefCount is 0, or greater
than 0. You can tell if a particular handle is strong with the `GCHeapHandle.Strong` property.

There are nine handle types, each with a special meaning:
1. **WeakShort** - A weak handle which is meant to be short lived in the
   process.
2. **WeakLong** - A weak handle which is meant to be long lived in the process.
3. **Strong** - A strong handle.
4. **Pinned** - A strong handle which prevents the GC from relocating the
   object.
5. **AsyncPinned** - A strong handle used by async operations. This is also a
   pinning handle, meaning the GC is not allowed to relocate the object this
   points to.
6. **Variable** - An unused handle type. You should never see one of these.
7. **RefCount** - A reference counted strong handle (used for COM interop).
   When the RefCount hits 0 the handle becomes a weak handle.
8. **Dependent** - A weak handle. Please see below.
9. **SizedRef** - A strong handle used by ASP.NET.

The last three, RefCount handles, Dependent handles, and SizedRef handles each have caveats that you should be aware of.

### RefCount Handles

RefCount handles are used for COM interop, and obviously one of the most important things to know about a RefCount handle is the actual refcount itself.  For v4 and v4.5 CLR, we have that data. However, in v2 CLR we did not expose the reference count out through the Dac. This means that if you are debugging a v2 process we do not have a reference count we can give you. The `GCHeapHandle.RefCount` property will always return 1. This means ClrMD always treats RefCount handles as Strong handles in CLR v2. This is mostly true, RefCount handles get cleaned up when their count hits 0. So the only time you would get a false-positive in v2 is if the crash dump was taken after the real ref count hit 0 and before the next GC cleaned it up.

### SizedRef Handles

SizedRef handles are used exclusively by ASP.NET to hold onto caches of objects.  As the GC marks objects it keeps track of what objects the handle keeps alive. When the GC is complete it updates a field on the SizedRef handle which is an estimate of how much memory that object holds onto. This is similar to a
inaccurate `!objsize`.

Unfortunately the Dac does not expose any way to get the size which the SizedRef handle holds onto, so neither does ClrMD.

### Dependent Handles

Dependent handles are a very complicated topic. They were added in v4 as a way to add an edge to the GC graph. A dependent handle consists of the handle address, the object that the handle refers to (I'll call this the "source" object), and a "target" object. A dependent handle keeps the "target" object alive if the "source" object is alive. However, the handle itself is a weak handle, meaning it does not root the "source" object.  Users can create dependent handles through the `System.Runtime.CompilerServices.ConditionalWeakTable` class.

The major caveat here is that dependent handle support was not added to the dac until CLR v4.5. This means that on CLR v4 runtimes, you will know if there are dependent handles in the process, and what the "source" object is, but you cannot get the "target" object unless you are running on CLR v4.5.

All of this is important because if you are attempting to write the equivalent code to `!objsize` or `!gcroot`, you must take dependent handles into account. A dependent handle adds real edges to the graph and needs to be treated as any other object reference to get accurate results.

# The GC Heap

## Introduction to the GC Heap

CLR's GC is a generational, mark and sweep GC. There are 3 generations in the process, gen0, gen1, and gen2. Objects are allocated in gen0, and whenever an object survives a GC they are promoted to the next generation (thus a gen0 object becomes a gen1 object and a gen1 object becomes a gen2 object). The only exception to this rule are large objects. Large objects are allocated directly into gen2 (on a special segment) and are never relocated. An object is considered a "large object" if it takes up more than 85,000 bytes.

There are also two types of GC's: the workstation GC and the server GC. There are two primary differences between a workstation GC and a server GC from a diagnostics standpoint: First, the workstation GC runs on only one thread whereas the server GC runs on multiple threads. Second (and related to the first point), a workstation GC has only one logical heap whereas the server GC has as many logical heaps as there are cores on the machine.

When you make an allocation with the server GC running, the newly allocated object goes into the logical heap associated with the core that thread happened to be running on. In general, the distinction between the two types of GCs does not matter for diagnostics. The only real case where you need to care about the logical heaps in the process is when they become unbalanced... when one heap has had much more allocated on it than other heaps. This can lead to a performance issue where GCs pause the process for longer than it should. (An example of displaying heap balance is shown later.)

Each logical heap in the process has a number of heap Segments associated with them. A segment is a region of memory which contains managed objects. There are three kinds of GC segments:

Ephemeral segments, gen2 segments, and large object segments. There is exactly 1 ephemeral segment per logical heap, and the ephemeral segment contains gen0, gen1, and gen2 objects. When we run out of space on the ephemeral segment, we allocate a gen2 segment and move some (or all) gen2 objects from the ephemeral segment to the new segment. If there's already a gen2 segment for that logical heap, we will continue to move gen2 objects out of the ephemeral segment onto the gen2 segment until we run out of room. There can be any number of gen2 segments per logical heap.

Finally, there are large object segments. When a large object (85,000 bytes or more) are allocated, we allocate this object directly into gen2. We do not, however, use the gen2 segments to do this. Instead we allocate these objects in "large object segments" directly. Large object segments have two main properties: all objects in them are considered gen2 and no object in them may be relocated (we treat objects in these segments as pinned).

## Objects on the Heap

Objects on the target process's GC heap are represented by the `ClrObject` struct.  `ClrObject` instances give you wide range of operations to find out information about the object:

1.  `Address` tells you the address of the object in the target process's address space.
2.  `IsValid` tells you if the object is valid.  This will return false if the address in the target address space did not point to a valid object.
3.  `IsNull` tells you if the object address was null (also `Address == 0` in this case).  Note that null objects are considered valid (`IsNull == true` => `IsValid == true`).
4.  `Type` tells you the type of the object, but note that this field may be null if `IsNull` is true or `IsValid` is false.
5.  `Read*Field` methods allow you to read the instance fields of the class.
6.  `IsArray` and `AsArray` gives you the ability to read from array indicies.
7.  `IsException` and `AsException` let you retrieve specific information about exception objects.
8.  And more.

If you have the address of an object in the target address space, you can create a `ClrObject` for that address by calling `ClrHeap.GetObject(address)`.

## Getting the Heap and Walking Segments

The `ClrRuntime` object has a property called `Heap`, which returns a `ClrHeap` object. The `ClrHeap` object, among other things, allows you to walk each segment in the process. Here's a simple example of walking each segment and printing out data for each segment:

```c#
ClrRuntime runtime = ...;
foreach (ClrSegment segment in runtime.Heap.Segments)
{
    string type;
    if (segment.IsEphemeralSegment)
        type = "Ephemeral";
    else if (segment.IsLargeObjectSegment)
        type = "Large";
    else
        type = "Gen2";

    Console.WriteLine($"{type} object range: {segment.ObjectRange} committed memory range: {segment.CommittedMemory} reserved memory range: {segment.ReservedMemory}");
}
```

As you can see, each `ClrSegment` object gives you `ObjectRange` which is the range of memory that contains objects as well the committed and reserved memory ranges for every segment.

Note that the `ClrSegment.LogicalHeap` is actually the logical GC Heap to which the segment belongs. Here is a simple linq query which will print out a table showing logical heap balance:

```c#
foreach (var item in from seg in heap.Segments
                        group seg by seg.LogicalHeap into g
                        orderby g.Key
                        select new
                        {
                            Heap = g.Key,
                            Size = g.Sum(p => (uint)p.Length)
                        })
{
    Console.WriteLine($"Heap {item.Heap} {item.Size:n0} bytes");
}
```

## Walking Managed Objects in the Process

You can walk all objects on a segment by calling `ClrSegment.EnumerateObjects`, but you don't have to enumerate every segment just to enumerate objects, we also implement `ClrHeap.EnumerateObjects` which enumerates every object on every segment.  Here is an example of walking each object on each segment in the process and printing the address, size, generation, and type of the object:

```c#
ClrRuntime runtime = ...;

if (!runtime.Heap.CanWalkHeap)
{
    Console.WriteLine("Cannot walk the heap!");
}
else
{
    foreach (ClrSegment seg in runtime.Heap.Segments)
    {
        foreach (ClrObject obj in seg.EnumerateObjects())
        {
            // If heap corruption, continue past this object.
            if (!obj.IsValid)
                continue;

            ulong objSize = obj.Size;
            int generation = seg.GetGeneration(obj);
            Console.WriteLine($"{obj} {objSize} {generation}");
        }
    }
}
```

There are two parts in this example you should pay attention to. First is checking the `ClrHeap.CanWalkHeap` property. This property specifies whether the process is in a state where you can reliably walk the heap. If the crashdump was taken during the middle of a GC, the GC could have been relocating objects. At which point a linear walk of the GC heap is not possible. If this is the
case, `CanWalkHeap` will return `false`.

Second, you need to check whether objects returned from `EnumerateObjects` are valid. `ClrSegment.NextObject` does not attempt to detect heap corruption, so it is possible we will return an object where `ClrObject.IsValid` is false.

## Walking objects without walking the segments


```csharp
CLRRuntime runtime = ...;

if (!runtime.Heap.CanWalkHeap)
{
    Console.WriteLine("Cannot walk the heap!");
}
else
{
    foreach (ClrObject obj in runtime.Heap.EnumerateObjects())
    {
        // If heap corruption, continue past this object.
        if (!obj.IsValid)
            continue;

        ulong objSize = obj.Size;
        int generation = runtime.Heap.GetSegmentByAddress(obj).GetGeneration(obj);
        Console.WriteLine($"{obj} {objSize} {generation}");
    }
}
```

The above code's results are equivalent to the one above it but it will be slower due to having to look up the segment the object is in

## A non-linear heap walk

The approach above is a good way to walk every object on the heap. But what if you want to only walk a subset of objects? For example, let's say you have an object and you want to know all of the objects it points to, and all the objects those point to, and so on. This is what we call the `!objsize` algorithm.

If you are not familiar with `!objsize` in SOS, this command takes an object as a parameter and counts the number of objects it keeps alive as well as reports the total size of all objects the given object keeps alive.  Given an object, you can enumerate all objects it points to using `ClrType.Enumerate` object references. Here is an example of implementing this:

```c#
private static (int count, ulong size) ObjSize(ClrObject input)
{
    HashSet<ulong> seen = new HashSet<ulong>() { input };
    Stack<ClrObject> todo = new Stack<ClrObject>(100);
    todo.Push(input);

    int count = 0;
    ulong totalSize = 0;

    while (todo.Count > 0)
    {
        ClrObject curr = todo.Pop();

        count++;
        totalSize += curr.Size;

        foreach (var obj in curr.EnumerateReferences(carefully: false, considerDependantHandles: false))
            if (seen.Add(obj))
                todo.Push(obj);
    }

    return (count, totalSize);
}
```

Note that `EnumerateReferences` takes two arguments:  `carefully` determines whether we ensure the ClrObjects returned are valid or not if there is heap corruption.  Setting this to true causes us to do a lot more checks which will significantly slow down the algorithm.  `considerDependantHandles` tells ClrMD whether or not to look at the dependant handle table and enumerate references kept alive by `ConditionalWeakTable`.

### Why do we need EnumerateRefsOfObject?

You might be wondering why we need `ClrType.EnumerateRefsOfObject` at all. You can attempt to walk each field in the object and get its value to try to implement this algorithm, but `EnumerateRefsOfObject` is much, much faster. It uses the same algorithm the GC does to get object references out of the object, which is far more efficient than walking fields to look for objects.


# Types and Fields in CLRMD

## Introduction

An object's type in ClrMD is represented by `ClrType`. This class has two sets of operations. The first is to provide data about an instance of that type. For example, `ClrType` has functions for getting the length of an array, getting the size of an object, and getting the field values for an object. The second set is operations which tell you about the type itself such as what fields it contains, what interfaces it implements, what methods the type has, and so on.

For most uses of ClrMD you shouldn't need to use `ClrType` directly very often except to get the values of static variables since `ClrObject` has most of the operations you should use for this purpose.

## Basic type information

We'll start with basic operations of a type. There are a few fairly self explanatory functions and properties, such as `ClrType.Name` (the name of the type) and `ClrType.GetSize`, which returns the size of an instance of that type. Note that you must pass in an instance of an object to get its size since there are variable-sized types in CLR. Similarly, `ClrType` has `IsArray`, `IsException`, `IsEnum`, and `IsFree` which tells you if the type is an array of some sort, is a subclass of `Exception`, an `Enum`, or free space on the heap, respectively. (We'll cover free objects in more detail below.)

Another very basic thing that you can do with a type is enumerate the interfaces it implements. Here is an example of doing that:

```c#
ClrType type = ...;
Console.WriteLine($"Type {type.Name} implements interfaces:");

foreach (ClrInterface inter in type.EnumerateInterfaces())
    Console.WriteLine("    {0}", inter.Name);
```

## Getting field values

ClrMD supports obtaining instance and static fields from types.  Note we currently do not support thread static values or context statics because the dac does not give us the proper functions to be able to do this.

To get instance field values, you should use `ClrObject.Read*Field` functions to read values.  For example let's say we define this class and struct:


```csharp
struct Two
{
    public float f;
    public object o2;
}
class One
{
    public int i;
    public object o;
    public string str;
    public Two two;
}
```

Now let's say that `obj` below points to an instance of `One`.  Here's how we would obtain each of the fields in `One` and `Two`:

```csharp
    ClrObject one = ...;
        
    int i = one.ReadField<int>("i");
    ClrObject o = one.ReadObjectField("o");
    string str = one.ReadStringField("str");
    ClrValueType two = one.ReadValueTypeField("two");

    float f = two.ReadField<float>("f");
    ClrObject o2 = two.ReadObjectField("o2");
```

As you can see, the design of these methods requires you to know what the type of the field is you are requesting at compile time.  If you do not know this information, then you can use `ClrField.ElementType` to find this value.  For example, this statement is true:  `one.Type.Fields.Single(f => f.Name == "i").ElementType == ClrElementType.Int32`.

### "Free" objects

When enumerating the heap, you will notice a lot of "Free objects". These are denoted by the `ClrObject.IsFree` property (also `ClrObject.IsFree == ClrObject.Type.IsFree`).

Free objects are not real objects in the strictest sense. They are actually markers placed by the GC to denote free space on the heap. Free objects have no fields (though they do have a size). In general, if you are trying to find heap fragmentation, you will need to take a look at how many Free objects there are, how big they are, and what lies between them. Otherwise, you should ignore them.

## Getting Methods

Enumerating methods on a type is performed via the `ClrType.Methods` property which returns a list of `ClrMethod` objects for the type.
