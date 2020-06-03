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

Note that we did not specific the location of the dac library when calling here.  ClrMD will default to looking at the `LocalDacPath` and use it if it is non-null.  If the `_NT_SYMBOL_PATH` environment variable is set we will also check the symbol servers listed for a matching dac.  To use the Microsoft symbol server you can set `_NT_SYMBOL_PATH=https://msdl.microsoft.com/download/symbols`.

You can also create a runtime from a dac location on disk if you know exactly where it is:

```cs
ClrInfo runtimeInfo = dataTarget.ClrVersions[0];  // just using the first runtime
ClrRuntime runtime = runtimeInfo.CreateRuntime(@"C:\path\to\mscordacwks.dll");
```

## Getting the Dac from the Symbol Server

When you call CreateRuntime without specifying the location of mscordacwks.dll, ClrMD attempts to locate the dac for you.  It does this through a few mechanisms, first it checks to see if you have the same version of CLR that you are attempting to debug on your local machine.  If so, it loads the dac from there.  (This is usually at c:\windows\Framework[64]\[version]\mscordacwks.dll.)  If you are debugging a crash dump that came from another computer, you will have to find the dac that matches the crash dump you are debugging.

All versions of the dac are requried to be on the Microsoft public symbol server, located here:  https://msdl.microsoft.com/download/symbols.  The DataTarget.SymbolLocator property is how ClrMD interacts with symbol servers.  If you have set the _NT_SYMBOL_PATH environment variable, ClrMD will use that string as your symbol path.  If this environment variable is not set, it will default to the Microsoft Symbol Server.

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

Third, there are many cases where we cannot get a complete managed stack trace.  Without getting into too much detail here, the process could have been stopped in a state where our stackwalker gets confused and cannot properly walk the managed stack. However, you are gauaranteed the same results that SOS gets in
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
6. **Variable** - An usused handle type. You should never see one of these.
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
            if (!obj.IsValidObject)
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

Second, you need to check whether objects returned from `EnumerateObjects are valid.  `ClrSegment.NextObject` does not attempt to detect heap corruption, so it is possible we will return an object where `ClrObject.IsValid` is false.

## Walking objects without walking the segments


```c#
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
        if (!obj.IsValidObject)
            continue;

        ulong objSize = obj.Size;
        int generation = runtime.Heap.GetSegmentByAddress(obj).GetGeneration(obj);
        Console.WriteLine($"{obj} {objSize} {generation}");
    }
}
```

The above code's results are equivalent to the one above it but it will be slower due to having to look up the segment the object is in

## A non-linear heap walk

The approach above is a good way to to walk every object on the heap. But what if you want to only walk a subset of objects? For example, let's say you have an object and you want to know all of the objects it points to, and all the objects those point to, and so on. This is what we call the `!objsize` algorithm.

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

In general, there is no wrapper types for an object in CLR MD, but other types in CLR MD take an object address as a parameter to operate on.

## Basic type information

We'll start with basic operations of a type. There are a few fairly self explanatory functions and properties, such as `ClrType.Name` (the name of the type) and `ClrType.GetSize`, which returns the size of an instance of that type. Note that you must pass in an instance of an object to get its size since there are variable-sized types in CLR. Similarly, `ClrType` has `IsArray`, `IsException`, `IsEnum`, and `IsFree` which tells you if the type is an array of some sort, is a subclass of `Exception`, an `Enum`, or free space on the heap, respectively. (We'll cover free objects in more detail below.)

Another very basic thing that you can do with a type is enumerate the interfaces it implements. Here is an example of doing that:

```c#
ClrType type = ...;
Console.WriteLine($"Type {type.Name} implements interfaces:");

foreach (ClrInterface inter in type.EnumerateInterfaces())
    Console.WriteLine("    {0}", inter.Name);
```


todo:  I am here editing this document.



## Getting field values

ClrMD supports obtaining instance and static fields from types.  Note we currently do not support thread static values or context statics because the dac does not give us the proper functions to be able to do this.

At the simplest level you can request a field by name. For example, all `Exception` objects contains an `_HResult` field. Here is how you would get the `_HResult` value for that object:

```c#
if (type.IsException)
{
    GCHeapInstanceField _hresult = type.GetFieldByName("_HResult");
    Debug.Assert(_hresult.ElementType == ClrElementType.ELEMENT_TYPE_I4);

    int value = (int)_hresult.GetFieldValue(obj);
    Console.WriteLine("Exception 0x{0:X} hresult: 0x{1:X}", obj, value);
}
```

Note that `GCHeapInstanceField.GetFieldValue` returns an "object" which I've
blindly cast to an int here. That's because the underlying type of `_HResult` is
an int. Were the field a float, you would need to cast that to a float as well.
Similarly, all `System.String` objects come out as a regular "string" filled
with its contents.

You may only call `GetFieldValue` if the
`GCHeapInstanceField.HasSimpleValue` returns true. In theory, you should always
check that property before calling `GetFieldValue`. However, in practice, only
fields which are value classes (C#'s "struct") cannot retrieve a value through
`GetFieldValue`. Calling `GetFieldValue` on anything where `HasSimpleValue` is
`false`, returns `null`.

You can tell what type `GetFieldValue` will
return (if you do not know it ahead of time) by looking at the value of
`GCHeapInstanceField.ElementType`. This property is equivalent to the
`CorElementType` of this field, as defined by the CLI spec. This tells you what
exact type will be returned in the "object" return value of `GetFieldValue`.

Here is an example of formatting the return value of `GetFieldValue` as
hexadecimal string for any pointer-types, and otherwise as a string
representation for the value for that type.

```c#
static string GetOutput(ulong obj, GCHeapInstanceField field)
{
    // If we don't have a simple value, return the address of the field in hex.
    if (!field.HasSimpleValue)
        return field.GetFieldAddress(obj).ToString("X");

    object value = field.GetFieldValue(obj);
    if (value == null)
        return "{error}";  // Memory corruption in the target process.

    // Decide how to format the string based on the underlying type of the field.
    switch (field.ElementType)
    {
        case ClrElementType.ELEMENT_TYPE_STRING:
            // In this case, value is the actual string itself.
            return (string)value;

        case ClrElementType.ELEMENT_TYPE_ARRAY:
        case ClrElementType.ELEMENT_TYPE_SZARRAY:
        case ClrElementType.ELEMENT_TYPE_OBJECT:
        case ClrElementType.ELEMENT_TYPE_CLASS:
        case ClrElementType.ELEMENT_TYPE_FNPTR:
        case ClrElementType.ELEMENT_TYPE_I:
        case ClrElementType.ELEMENT_TYPE_U:
            // These types are pointers.  Print as hex.
            return string.Format("{0:X}", value);

        default:
            // Everything else will look fine by simply calling ToString.
            return value.ToString();
    }
}
```

## Working with embedded structs

As you've already seen, value classes (C#'s `struct`s) require special handling.
CLR recursively embeds structs in types, this means that if you had a class
defined as:

```c#
struct Three
{
    int b;
}
struct Two
{
    int a;
    Three three;
    int c;
}
class One
{
    int i;
    Two two;
    int j;
}
```

What does the layout of an object of type `One` look like? It looks something
like this (on x86, and this layout may be completely different, numbers in hex):

```
+00 int i;
+04 int a;    <- start of struct "two"
+08 int c;
+0c int b;    <- start of struct "three";
+10 int j;
```

As you can see, CLR has recursively embedded structs `Two` and `Three` into
class `One`. This can have surprising consequences if you are attempting to
deeply inspect an object. For example, let's take the naive approach to walking
the `One` class:

```c#
public static void WriteFields(ulong obj, ClrType type)
{
    foreach (var field in type.Fields)
    {
        string output;
        if (field.HasSimpleValue)
            output = field.GetFieldValue(obj).ToString();
        else
            output = field.GetFieldAddress(obj).ToString("X");

        Console.WriteLine("  +{0,2:X2} {1} {2} = {3}", field.Offset, field.Type.Name, field.Name, output);
    }
}
```

This works just fine, but you will not see fields a, b, or c! This prints out
something like:

```
+00 System.Int32 i = 0
+04 Two two = 27F2CB4
+10 System.Int32 j = 0
```

As you can see, the `two` field has not been expanded. If that's what you are
looking for, great! You are done.

However, let's say you want to retrieve fields which are a part of an inlined
struct, embedded in an object. To do this, you will need to use the
`GetFieldValue` overload which accepts the `inner` parameter, signifying that
you are reading from an embedded struct. Here is a simple example of doing so,
where obj is of type `One`:

```c#
var twoField = type.GetFieldByName("two");
var twoA = twoField.Type.GetFieldByName("a");
var twoC = twoField.Type.GetFieldByName("c");

// Get the address of the "two" field:
ulong twoAddr = twoField.GetFieldAddress(obj);

// Now get the value of a and c, note that we must pass true for the "inner" parameter.
int a = (int)twoA.GetFieldValue(twoAddr, true);
int c = (int)twoC.GetFieldValue(twoAddr, true);
```

As you can see, this gets quite complex, and requires you to understand the
layout of the object you are walking. You know that a field is embedded if
`field.ElementType == ClrElementType.ELEMENTTYPEVALUETYPE`. Here is another
example, one which recursively walks an object and prints out the values and
locations of all fields:

```c#
public static void WriteFields(ulong obj, ClrType type)
{
    WriteFieldsWorker(obj, type, null, 0, false);
}

static void WriteFieldsWorker(ulong obj, ClrType type, string baseName, int offset, bool inner)
{
    // Keep track of nested fields.
    if (baseName == null)
        baseName = "";
    else
        baseName += ".";

    foreach (var field in type.Fields)
    {
        ulong addr = field.GetFieldAddress(obj, inner);

        string output;
        if (field.HasSimpleValue)
            output = field.GetFieldValue(obj, inner).ToString();
        else
            output = addr.ToString("X");

        Console.WriteLine("  +{0,2:X2} {1} {2}{3} = {4}", field.Offset + offset, field.Type.Name, baseName, field.Name, output);

        // Recurse for structs.
        if (field.ElementType == ClrElementType.ELEMENT_TYPE_VALUETYPE)
            WriteFieldsWorker(addr, field.Type, baseName + field.Name, offset + field.Offset, true);
    }
}
```

This would print out something like:

```
+00 System.Int32 i = 0
+04 Two two = 27F2CB4
+0c Three three = 27F2CBc
+04 System.Int32 two.a = 0
+0c System.Int32 two.c = 0
+08 System.Int32 two.b = 0
+10 System.Int32 two.three.j = 0
```

## One last note about fields

As you can see, working with fields is unfortunately very complex in CLRMD. We
are forced into this complex design to accomodate the full expressiveness of
CLR's type system.

## Special Subtypes

There are several special "subtypes" that you should be aware of. For example,
Arrays, Enums, Exceptions, and Free objects. We will go into depth on each of
these.

### Arrays

Arrays in CLRMD are unfortunately another complex topic, again due to embedded
structs. Before we look at the complex case, let's look at an array of ints, as
this easy to work with. Note, I use a simple linq query to filter down to just
`int[]` objects (which is quite inefficient) for simplicity of the example:

```c#
var intArrays = from o in heap.EnumerateObjects()
                let t = heap.GetObjectType(o)
                where t.IsArray && t.Name == "System.Int32[]"
                select new { Address = o, Type = t };

foreach (var item in intArrays)
{
    ClrType type = item.Type;
    ulong obj = item.Address;

    int len = type.GetArrayLength(obj);

    Console.WriteLine("Object: {0:X}", obj);
    Console.WriteLine("Length: {0}", len);
    Console.WriteLine("Elements:");

    for (int i = 0; i < len; i++)
        Console.WriteLine("{0,3} - {1}", i, type.GetArrayElementValue(obj, i));
}
```

At a basic level, walking the elements of an array is quite simple.
`ClrType.IsArray` will tell you the type is an array. Use the
`GetArrayLength` function to get the length of the array, and either
`GetArrayElementValue` or `GetArrayElementAddress` to get the address or value
of the field, respectively. Similar to fields, `ClrType` also provides the
`ArrayComponentType` property, telling you the component type of the array. You
can use: `type.ArrayComponentType.HasSimpleValue` or
`type.ArrayComponentType.ElementType == ClrElementType.ELEMENTTYPEVALUETYPE` to
tell if you are dealing with an array of value types.

When working with an array of value types, they work very similarly to value
type fields. They are embedded in the array itself. So, let's look at an example
of walking all "arrays of structs" in a process and printing out the fields they
contain:

```c#
foreach (ulong obj in heap.EnumerateObjects())
{
    var type = heap.GetObjectType(obj);

    // Only consider types which are arrays that do not have simple values (I.E., are structs).
    if (!type.IsArray || type.ArrayComponentType.HasSimpleValue)
        continue;

    int len = type.GetArrayLength(obj);

    Console.WriteLine("Object: {0:X}", obj);
    Console.WriteLine("Type:   {0}", type.Name);
    Console.WriteLine("Length: {0}", len);
    for (int i = 0; i < len; i++)
    {
        ulong addr = type.GetArrayElementAddress(obj, i);
        foreach (var field in type.ArrayComponentType.Fields)
        {
            string output;
            if (field.HasSimpleValue)
                output = field.GetFieldValue(addr, true).ToString();        // <- true here, as this is an embedded struct
            else
                output = field.GetFieldAddress(addr, true).ToString("X");   // <- true here as well

            Console.WriteLine("{0}  +{1,2:X2} {2} {3} = {4}", i, field.Offset, field.Type.Name, field.Name, output);
        }
    }
}
```

This would print out:

```
Object: 27FAC28
Type:   System.Collections.Generic.Dictionary.Entry<System.String,System.Resources.ResourceLocator>[]
Length: 3
0  +00 System.__Canon key = 41896456
0  +08 System.__Canon value = 18446744071403970992
0  +10 System.Int32 hashCode = 41921440
0  +14 System.Int32 next = 0
1  +00 System.__Canon key = 41923968
1  +08 System.__Canon value = 122910689
1  +10 System.Int32 hashCode = 41924480
1  +14 System.Int32 next = 0
2  +00 System.__Canon key = 41924008
2  +08 System.__Canon value = 18446744070652986553
2  +10 System.Int32 hashCode = 41924696
2  +14 System.Int32 next = 0
```

Of course, these embedded structs can also have embedded structs within them!
You would need to recursively walk these structs (as we did earlier) to fully
walk the contents of these arrays.

### Enums

You can check if a type is an Enum by checking the `ClrType.IsEnum` property.
If a type is an Enum, you can get the list of enumeration values it contains
using `ClrType.GetEnumNames`. For example, let's say an enum was defined as:

```c#
enum Numbers
{
    Two = 2,
    Three = 3
    One = 1,
}
```

`GetEnumNames` would return `"One"`, `"Two"`, and `"Three"`. You can then feed these
values into `TryGetEnumValue`, which will give you the value of each defined
name. For example, `type.TryGetEnumValue("Three", out value)` would set
`value = 3`.

There is one major caveat to using enums in CLRMD. Enums in CLR can be of many
different types. Currently CLRMD only supports getting the enum value for "int"
(which is the most common type anyway). You can check what underlying type an
enum actually is using `GetEnumElementType`. The following code demonstrates
some of these concepts:

```c#
// This walks the heap looking for enums, but keep in mind this works for field
// values which are enums too.
foreach (ulong obj in heap.EnumerateObjects())
{
    var type = heap.GetObjectType(obj);
    if (!type.IsEnum)
        continue;

    // Enums do not have to be ints!  We will only handle the int case here.
    if (type.GetEnumElementType() != ClrElementType.ELEMENT_TYPE_I4)
        continue;

    int objValue = (int)type.GetValue(obj);

    bool found = false;
    foreach (var name in type.GetEnumNames())
    {
        int value;
        if (type.TryGetEnumValue(name, out value) && objValue == value)
        {
            Console.WriteLine("{0} - {1}", value, name);
            found = true;
            break;
        }
    }

    if (!found)
        Console.WriteLine("{0} - {1}", objValue, "Unknown");
}
```

### Exceptions

Exceptions in CLRMD are objects which derrive from `System.Exception`. CLR
itself does not actually make that distinction, almost any object can be thrown
as an exception (this is mostly to support C++/CLI which allows throwing non-
`System.Exception` objects). `ClrType.IsException` only returns `true` if an
object derives from `System.Exception`.

The `ClrType` class does not offer any properties or methods specific to
exceptions (other than `IsException`). Instead, it provides a wrapper class for
exception objects which gives you a wide variety of helpful properties for
exceptions. This includes the HRESULT of the exception, the exception message,
the type of the exception, an optional callstack and so on. Here is a very
simple example of using the `GCHeapException` object:

```c#
foreach (ulong obj in heap.EnumerateObjects())
{
    var type = heap.GetObjectType(obj);
    if (!type.IsException)
        continue;

    GCHeapException ex = heap.GetExceptionObject(obj);
    Console.WriteLine(ex.Type.Name);
    Console.WriteLine(ex.Message);
    Console.WriteLine(ex.HResult.ToString("X"));

    foreach (var frame in ex.StackTrace)
        Console.WriteLine(frame.ToString());
}
```

Please note that we do not always have a stack trace available for every
exception. For example, if an exception object is constructed, but never thrown,
it will not have an exception stack trace. In these cases,
`GCHeapException.StackTrace` will be a list of length 0 (so the above code will
simply not print a stack trace in that case).

### "Free" objects

When enumerating the heap, you will notice a lot of "Free objects". These are
denoted by the `ClrType.IsFree` property.

Free objects are not real objects in the strictest sense. They are actually
markers placed by the GC to denote free space on the heap. Free objects have no
fields (though they do have a size). In general, if you are trying to find heap
fragmentation, you will need to take a look at how many Free objects there are,
how big they are, and what lies between them. Otherwise, you should ignore them.

## Getting Methods

Enumerating methods on a type is performed via the `ClrType.Methods` property
which returns a list of `ClrMethod` objects for the type.

## Enumerating all types

Previously, we have talked about types by starting from an object instance and
getting the type from it. However, there are also circumstances where you want
to simply enumerate all types in the runtime. For example, you may want to get
the values of all static variables in the process. To enumerate all types that
CLR currently knows about, you can call the `GCHeap.EnumerateTypes` function.
Enumerating all types in the runtime has a LOT of caveats, however. Before we
get to those, let's look at a quick example of printing out every static
variable in the runtime:

```c#
foreach (var type in heap.EnumerateTypes())
{
    foreach (CLRAppDomain appDomain in runtime.AppDomains)
    {
        foreach (GCHeapStaticField field in type.StaticFields)
        {
            if (field.HasSimpleValue)
                Console.WriteLine("{0}.{1} ({2}) = {3}", type.Name, field.Name, appDomain.ID, field.GetFieldValue(appDomain));
        }
    }
}
```

Similarly, this is how you would walk all ThreadStatic variables in your process:

```c#
foreach (var type in heap.EnumerateTypes())
{
    foreach (CLRAppDomain appDomain in runtime.AppDomains)
    {
        foreach (CLRThread thread in runtime.Threads)
        {
            foreach (GCHeapThreadStaticField field in type.ThreadStaticFields)
            {
                if (field.HasSimpleValue)
                    Console.WriteLine("{0}.{1} ({2}, {3:X}) = {4}", type.Name, field.Name, appDomain.ID, thread.OSThreadId, field.GetFieldValue(appDomain, thread));
            }
        }
    }
}
```

There are several caveats that you need to know about when using
`GCHeap.EnumerateTypes`. First, this function can be quite slow the first time
you call it. CLRMD is implemented on top of the old dac debugging interfaces,
and this is simply one place where we did not enumerate types in a smart or
performant way. You should expect a 1-3 second delay for a sizable program when
calling this function for the first time. The result is cached though, so
subsequent calls will be MUCH faster.

Second, this function only enumerates types which CLR currently knows about.
Let's say you have a class called "Foo" in your program which has no static
variables, and has never been constructed. While the class exists in the
metadata of your program, CLR has never needed it, so it never constructed it.
That means that it will NOT be one of the types enumerated to you. This again is
a limitation of the underlying API this library was implemented on top of. If
you want all of the types in your metadata, you will need to use the real
Metadata APIs, which CLRMD does not provide.

Third, some generics will be enumerated and some will not. Some generic
instantiations will be enumerated and some will not. Sometimes the runtime will
enumerate the "open form" of generics through this function. For example, we may
enumerate `Action<T>` (the open generic form), `Action<int>`, and
`Action<object>`, but not enumerate `Action<float>`. You will have to account
for this and work around it if you attempt to enumerate types in the runtime.

The thing to keep in mind here is that it was impossible for me to implement
EnumerateTypes in a sensible, consistent way due to the underlying API CLRMD is
built on. I would not have included it at all in the API surface area, except
that you need a way to do things like: Enumerate all statics in the process,
enumerate all types (that we can!) which implement `IDisposable`, and so on.
Please use caution when using this for anything "outside the box" and realize
the limitations of it.

