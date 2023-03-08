using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrJitManager
    {
        ulong Address { get; }
        CodeHeapKind Kind { get; }
        IClrRuntime Runtime { get; }

        IEnumerable<ClrNativeHeapInfo> EnumerateNativeHeaps();
    }
}