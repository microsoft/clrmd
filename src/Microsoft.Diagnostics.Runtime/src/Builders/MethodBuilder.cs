// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal class MethodBuilder : IMethodData
    {
        private MethodDescData _mdData;
        private CodeHeaderData _codeHeaderData;

        public bool Init(SOSDac sos, ulong mt, uint i)
        {
            ulong slot = sos.GetMethodTableSlot(mt, i);

            if (!sos.GetCodeHeaderData(slot, out _codeHeaderData))
                return false;

            MethodDesc = _codeHeaderData.MethodDesc;
            return sos.GetMethodDescData(_codeHeaderData.MethodDesc, 0, out _mdData);
        }

        public bool Init(SOSDac sos, ulong methodDesc)
        {
            MethodDesc = methodDesc;
            if (!sos.GetMethodDescData(methodDesc, 0, out _mdData))
                return false;

            ulong slot = sos.GetMethodTableSlot(_mdData.MethodTable, _mdData.SlotNumber);
            return sos.GetCodeHeaderData(slot, out _codeHeaderData);
        }

        public int Token => (int)_mdData.MDToken;

        public ulong MethodDesc { get; private set; }
        public MethodCompilationType CompilationType => (MethodCompilationType)_codeHeaderData.JITType;

        public ulong HotStart => _mdData.NativeCodeAddr;

        public uint HotSize => _codeHeaderData.HotRegionSize;

        public ulong ColdStart => _codeHeaderData.ColdRegionStart;

        public uint ColdSize => _codeHeaderData.ColdRegionSize;
    }
}
