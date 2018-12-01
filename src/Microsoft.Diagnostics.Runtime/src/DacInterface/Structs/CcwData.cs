// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CCWData : ICCWData
    {
        public readonly ulong OuterIUnknown;
        public readonly ulong ManagedObject;
        public readonly ulong Handle;
        public readonly ulong CCWAddress;

        public readonly int RefCount;
        public readonly int InterfaceCount;
        public readonly uint IsNeutered;

        public readonly int JupiterRefCount;
        public readonly uint IsPegged;
        public readonly uint IsGlobalPegged;
        public readonly uint HasStrongRef;
        public readonly uint IsExtendsCOMObject;
        public readonly uint HasWeakReference;
        public readonly uint IsAggregated;

        ulong ICCWData.IUnknown => OuterIUnknown;
        ulong ICCWData.Object => ManagedObject;
        ulong ICCWData.Handle => Handle;
        ulong ICCWData.CCWAddress => CCWAddress;
        int ICCWData.RefCount => RefCount;
        int ICCWData.JupiterRefCount => JupiterRefCount;
        int ICCWData.InterfaceCount => InterfaceCount;
    }
}