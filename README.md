# Microsoft.Diagnostics.Runtime

`Microsoft.Diagnostics.Runtime.dll` (nicknamed "CLR MD") is a process and crash
dump introspection library. This allows you to write tools and debugger plugins
which can do thing similar to SOS and PSSCOR.

For more details, take a look at the [documentation] and [samples].

[documentation]: ./Documentation/ClrRuntime.md
[samples]: https://github.com/Microsoft/dotnetsamples/tree/master/Microsoft.Diagnostics.Runtime/CLRMD

## FAQ

Please see the [FAQ](./Documentation/FAQ.md) for more information.

## Tutorials

Here you will find a step by step walkthrough on how to use the CLR MD API.
These tutorials are meant to be read and worked through in linear order to teach
you the surface area of the API and what you can do with it.

1. [Getting Started](./Documentation/GettingStarted.md) - A brief introduction
   to the API and how to create a CLRRuntime instance.

2. [The CLRRuntime Object](./Documentation/ClrRuntime.md) - Basic operations
   like enumerating AppDomains, Threads, the Finalizer Queue, etc.

3. [Walking the Heap](./Documentation/WalkingTheHeap.md) - Walking objects on
   the GC heap, working with types in CLR MD.

4. [Types and Fields in CLRMD](./Documentation/TypesAndFields.md) - More
   information about dealing with types and fields in CLRMD.

5. [Machine Code in CLRMD](./Documentation/MachineCode.md) - Getting access to
   the native code produced by the JIT or NGEN
