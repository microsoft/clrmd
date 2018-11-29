using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_IMPORT_DESCRIPTOR
    {
        [FieldOffset(0)]
        public UInt32 Characteristics; // 0 for terminating null import descriptor
        [FieldOffset(0)]
        public UInt32 OriginalFirstThunk; // RVA to original unbound IAT (PIMAGE_THUNK_DATA)
        [FieldOffset(4)]
        public UInt32 TimeDateStamp; // 0 if not bound,
        // -1 if bound, and real date\time stamp
        //     in IMAGE_DIRECTORY_ENTRY_BOUND_IMPORT (new BIND)
        // O.W. date/time stamp of DLL bound to (Old BIND)

        [FieldOffset(8)]
        public UInt32 ForwarderChain; // -1 if no forwarders
        [FieldOffset(12)]
        public UInt32 Name;
        [FieldOffset(16)]
        public UInt32 FirstThunk; // RVA to IAT (if bound this IAT has actual addresses)
    }
}