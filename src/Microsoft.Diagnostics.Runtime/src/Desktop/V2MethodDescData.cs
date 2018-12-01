// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V2MethodDescData : IMethodDescData
    {
        private int _bHasNativeCode;
        private int _bIsDynamic;
        private short _wSlotNumber;
        // Useful for breaking when a method is jitted.
        private ulong _addressOfNativeCodeSlot;

        private ulong _EEClassPtr;

        private ulong _preStubAddr;
        private short _JITType;
        private ulong _GCStressCodeCopy;

        // This is only valid if bIsDynamic is true
        private ulong _managedDynamicMethodObject;

        public ulong MethodDesc { get; }
        public ulong Module { get; }
        public uint MDToken { get; }
        ulong IMethodDescData.NativeCodeAddr { get; }

        MethodCompilationType IMethodDescData.JITType
        {
            get
            {
                if (_JITType == 1)
                    return MethodCompilationType.Jit;
                if (_JITType == 2)
                    return MethodCompilationType.Ngen;

                return MethodCompilationType.None;
            }
        }

        public ulong MethodTable { get; }
        public ulong GCInfo { get; }
        public ulong ColdStart => 0;
        public uint ColdSize => 0;
        public uint HotSize => 0;
    }
}