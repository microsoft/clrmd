using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    public interface IClrJitManager
    {
        ulong Address { get; }
        CodeHeapKind Kind { get; }
        ClrRuntime Runtime { get; }

        IEnumerable<ClrNativeHeapInfo> EnumerateNativeHeaps();
    }
}