using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    public class ClrJitManager
    {
        private readonly IJitManagerHelpers _helpers;

        public ClrRuntime Runtime { get; }
        public ulong Address { get; }
        public CodeHeapKind Kind { get; }

        public ClrJitManager(ClrRuntime runtime, in JitManagerInfo info, IJitManagerHelpers helpers)
        {
            Runtime = runtime;
            Address = info.Address;
            Kind = info.Kind;
            _helpers = helpers;
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateNativeHeaps() => _helpers.EnumerateNativeHeaps(this);
    }
}
