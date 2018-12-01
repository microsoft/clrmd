// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RCWData : IRCWData
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

        ulong IRCWData.IdentityPointer => IdentityPointer;
        ulong IRCWData.UnknownPointer => IUnknownPointer;
        ulong IRCWData.ManagedObject => ManagedObject;
        ulong IRCWData.JupiterObject => JupiterObject;
        ulong IRCWData.VTablePtr => VTablePointer;
        ulong IRCWData.CreatorThread => CreatorThread;
        int IRCWData.RefCount => RefCount;
        int IRCWData.InterfaceCount => InterfaceCount;
        bool IRCWData.IsJupiterObject => IsJupiterObject != 0;
        bool IRCWData.IsDisconnected => IsDisconnected != 0;
    }
}