using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is required for vtable layout")]
    internal unsafe readonly struct IDebugDataSpacesVtable
    {
        /* IDebugDataSpaces */
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, byte*, int, int*, int> ReadVirtual;
        private readonly nint WriteVirtual;
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, ulong, byte*, int, int, ulong*, int> SearchVirtual;
        private readonly nint ReadVirtualUncached;
        private readonly nint WriteVirtualUncached;
        private readonly nint ReadPointersVirtual;
        private readonly nint WritePointersVirtual;
        private readonly nint ReadPhysical;
        private readonly nint WritePhysical;
        private readonly nint ReadControl;
        private readonly nint WriteControl;
        private readonly nint ReadIo;
        private readonly nint WriteIo;
        private readonly nint ReadMsr;
        private readonly nint WriteMsr;
        private readonly nint ReadBusData;
        private readonly nint WriteBusData;
        private readonly nint CheckLowMemory;
        private readonly nint ReadDebuggerData;
        private readonly nint ReadProcessorSystemData;

        /* IDebugDataSpaces2 */
        private readonly nint VirtualToPhysical;
        private readonly nint GetVirtualTranslationPhysicalOffsets;
        private readonly nint ReadHandleData;
        private readonly nint FillVirtual;
        private readonly nint FillPhysical;
        private readonly nint QueryVirtual;

    }
}
