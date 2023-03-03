using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    // Helpers to evaluate lazy fields.
    public interface IJitManagerHelpers
    {
        IEnumerable<ClrNativeHeapInfo> EnumerateNativeHeaps(ClrJitManager clrJitManager);
    }
}