// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public sealed unsafe class ClrDataMethod : CallableCOMWrapper
    {
        private static readonly Guid IID_IXCLRDataMethodInstance = new Guid("ECD73800-22CA-4b0d-AB55-E9BA7E6318A5");

        private GetILAddressMapDelegate? _getILAddressMap;

        public ClrDataMethod(DacLibrary library, IntPtr pUnk)
            : base(library?.OwningLibrary, IID_IXCLRDataMethodInstance, pUnk)
        {
        }

        private ref readonly IXCLRDataMethodInstanceVTable VTable => ref Unsafe.AsRef<IXCLRDataMethodInstanceVTable>(_vtable);

        public ILToNativeMap[]? GetILToNativeMap()
        {
            InitDelegate(ref _getILAddressMap, VTable.GetILAddressMap);

            HResult hr = _getILAddressMap(Self, 0, out uint needed, null);
            if (!hr)
                return null;

            ILToNativeMap[] map = new ILToNativeMap[needed];
            hr = _getILAddressMap(Self, needed, out needed, map);

            return hr ? map : null;
        }

        private delegate HResult GetILAddressMapDelegate(IntPtr self, uint mapLen, out uint mapNeeded, [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ILToNativeMap[]? map);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IXCLRDataMethodInstanceVTable
    {
        private readonly IntPtr GetTypeInstance;
        private readonly IntPtr GetDefinition;
        private readonly IntPtr GetTokenAndScope;
        private readonly IntPtr GetName;
        private readonly IntPtr GetFlags;
        private readonly IntPtr IsSameObject;
        private readonly IntPtr GetEnCVersion;
        private readonly IntPtr GetNumTypeArguments;
        private readonly IntPtr GetTypeArgumentByIndex;
        private readonly IntPtr GetILOffsetsByAddress; // (ulong address, uint offsetsLen, out uint offsetsNeeded, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] uint[] ilOffsets);
        private readonly IntPtr GetAddressRangesByILOffset; // (uint ilOffset, uint rangesLen, out uint rangesNeeded, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] uint[] addressRanges);
        public readonly IntPtr GetILAddressMap;
        private readonly IntPtr StartEnumExtents;
        private readonly IntPtr EnumExtent;
        private readonly IntPtr EndEnumExtents;
        private readonly IntPtr Request;
        private readonly IntPtr GetRepresentativeEntryAddress;
    }
}