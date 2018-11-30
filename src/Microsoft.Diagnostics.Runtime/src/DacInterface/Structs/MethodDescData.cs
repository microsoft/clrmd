// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MethodDescData
    {
        public readonly uint HasNativeCode;
        public readonly uint IsDynamic;
        public readonly short SlotNumber;
        public readonly ulong NativeCodeAddr;

        // Useful for breaking when a method is jitted.
        public readonly ulong AddressOfNativeCodeSlot;

        public readonly ulong MethodDesc;
        public readonly ulong MethodTable;
        public readonly ulong Module;

        public readonly uint MDToken;
        public readonly ulong GCInfo;
        public readonly ulong GCStressCodeCopy;

        // This is only valid if bIsDynamic is true
        public readonly ulong ManagedDynamicMethodObject;

        public readonly ulong RequestedIP;

        // Gives info for the single currently active version of a method
        public readonly RejitData RejitDataCurrent;

        // Gives info corresponding to requestedIP (for !ip2md)
        public readonly RejitData RejitDataRequested;

        // Total number of rejit versions that have been jitted
        public readonly uint JittedRejitVersions;
    }
}