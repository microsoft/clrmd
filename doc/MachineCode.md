# Machine Code in CLRMD

Some folks asked whether it's possible to get the machine code produced by the
JIT or NGEN. It's possible, but a bit involved:

* Get the `ClrType` object for the type the method is on
* Get the `ClrMethod` object for the method
* Get the offset of the native code
* Compute the end address by mapping the IL instruction to addresses
* Disassemble the native code contained in that range (not provided by CLRMD)

```CSharp
using(DataTarget dt = DataTarget.LoadCrashDump(@"crash.dmp"))
{
    // Boilerplate.
    ClrRuntime runtime = dt.CreateRuntime(dt.ClrVersions.Single().TryDownloadDac());
    ClrHeap heap = runtime.GetHeap();

    // Note heap.GetTypeByName doesn't always get you the type, even if it exists, due to
    // limitations in the dac private apis that ClrMD is written on.  If you have the ClrType
    // already via other means (heap walking, stack walking, etc), then that's better than
    // using GetTypeByName:
    ClrType systemObject = heap.GetTypeByName("System.Object");

    // Get the method you are looking for.
    ClrMethod getHashCode = systemObject.Methods.Where(method => method.Name == "GetHashCode").Single();

    // Find out whether the method was JIT'ed or NGEN'ed (if you care):
    MethodCompilationType compileType = getHashCode.CompilationType;

    // This is the first instruction of the JIT'ed (or NGEN'ed) machine code.
    ulong startAddress = getHashCode.NativeCode;

    // Unfortunately there's not a great way to get the size of the code, or the end address.
    // This is partly due to the fact that we don't *have* to put all the JIT'ed code into one
    // contiguous chunk, though I think an implementation detail is that we actually do.
    // You are supposed to do code flow analysis like "uf" in windbg to find the size, but
    // in practice you can use the IL to native mapping:
    ulong endAddress = getHashCode.ILOffsetMap.Select(entry => entry.EndAddress).Max();

    // So the assembly code for System.Object.GetHashCode is in the range [startAddress, endAddress] inclusive.
}
```

