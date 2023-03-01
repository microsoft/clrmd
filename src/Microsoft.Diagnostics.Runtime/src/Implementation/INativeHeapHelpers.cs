using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface INativeHeapHelpers
    {
        IEnumerable<ClrNativeHeapInfo> EnumerateNativeHeaps(ClrAppDomain domain);
    }
}
