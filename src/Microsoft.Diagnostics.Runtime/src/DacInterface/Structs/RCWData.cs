// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RCWData
    {
        public readonly ulong IdentityPointer;
        public readonly ulong IUnknownPointer;
        public readonly ulong ManagedObject;
        public readonly ulong JupiterObject;
        public readonly ulong VTablePointer;
        public readonly ulong CreatorThread;
        public readonly ulong CTXCookie;

        public readonly int RefCount;
        public readonly int InterfaceCount;

        public readonly uint IsJupiterObject;
        public readonly uint SupportsIInspectable;
        public readonly uint IsAggregated;
        public readonly uint IsContained;
        public readonly uint IsFreeThreaded;
        public readonly uint IsDisconnected;
    }
}