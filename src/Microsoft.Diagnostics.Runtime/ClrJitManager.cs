using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class ClrJitManager
    {
        private readonly IClrNativeHeapHelpers _helpers;

        public ClrRuntime Runtime { get; }
        public ulong Address { get; }
        public CodeHeapKind Kind { get; }

        internal ClrJitManager(ClrRuntime runtime, in JitManagerInfo info, IClrNativeHeapHelpers helpers)
        {
            Runtime = runtime;
            Address = info.Address;
            Kind = info.Kind;
            _helpers = helpers;
        }

        public IEnumerable<ClrNativeHeapInfo> EnumerateNativeHeaps() => _helpers.EnumerateNativeHeaps(this);
    }
}
