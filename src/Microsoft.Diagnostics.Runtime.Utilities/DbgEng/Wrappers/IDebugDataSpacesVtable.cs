// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable CS0169 // field is never used
#pragma warning disable CS0649 // field is never assigned
namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "This is required for vtable layout")]
    [SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "VTable Layout")]
    internal readonly unsafe struct IDebugDataSpacesVtable
    {
        private readonly nint QueryInterface;
        private readonly nint AddRef;
        private readonly nint Release;

        /* IDebugDataSpaces */
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, byte*, int, out int, int> ReadVirtual;
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, byte*, int, out int, int> WriteVirtual;
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