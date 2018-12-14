// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V35MethodDescData : IMethodDescData
    {
        public readonly int HasNativeCode;
        public readonly int IsDynamic;
        public readonly short SlotNumber;
        public readonly ulong NativeCodeAddr;
        // Useful for breaking when a method is jitted.
        public readonly ulong AddressOfNativeCodeSlot;

        public readonly ulong MethodDescPtr;
        public readonly ulong MethodTablePtr;
        public readonly ulong EEClassPtr;
        public readonly ulong ModulePtr;

        public readonly uint MDToken;
        public readonly ulong GCInfo;
        public readonly short JITType;
        public readonly ulong GCStressCodeCopy;

        // This is only valid if bIsDynamic is true
        public readonly ulong ManagedDynamicMethodObject;

        ulong IMethodDescData.MethodTable => MethodTablePtr;
        ulong IMethodDescData.MethodDesc => MethodDescPtr;
        ulong IMethodDescData.Module => ModulePtr;
        uint IMethodDescData.MDToken => MDToken;
        ulong IMethodDescData.GCInfo => GCInfo;
        ulong IMethodDescData.NativeCodeAddr => NativeCodeAddr;
        ulong IMethodDescData.ColdStart => 0;
        uint IMethodDescData.ColdSize => 0;
        uint IMethodDescData.HotSize => 0;

        MethodCompilationType IMethodDescData.JITType
        {
            get
            {
                if (JITType == 1)
                    return MethodCompilationType.Jit;
                if (JITType == 2)
                    return MethodCompilationType.Ngen;

                return MethodCompilationType.None;
            }
        }
    }
}