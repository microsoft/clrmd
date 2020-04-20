// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal class MethodBuilder : IMethodData, IDisposable
    {
        private MethodDescData _mdData;
        private CodeHeaderData _codeHeaderData;
        private IMethodHelpers? _helpers;

        public bool Init(SOSDac sos, ulong mt, uint i, IMethodHelpers helpers)
        {
            ulong slot = sos.GetMethodTableSlot(mt, i);

            if (!sos.GetCodeHeaderData(slot, out _codeHeaderData))
                return false;

            _helpers = helpers;
            MethodDesc = _codeHeaderData.MethodDesc;
            return sos.GetMethodDescData(_codeHeaderData.MethodDesc, 0, out _mdData);
        }

        public bool Init(SOSDac sos, ulong methodDesc, IMethodHelpers helpers)
        {
            MethodDesc = methodDesc;
            if (!sos.GetMethodDescData(methodDesc, 0, out _mdData))
                return false;

            _helpers = helpers;
            ulong slot = sos.GetMethodTableSlot(_mdData.MethodTable, _mdData.SlotNumber);
            return sos.GetCodeHeaderData(slot, out _codeHeaderData);
        }

        public ObjectPool<MethodBuilder>? Owner { get; set; }
        public IMethodHelpers Helpers => _helpers!;

        public int Token => (int)_mdData.MDToken;

        public ulong MethodDesc { get; private set; }
        public MethodCompilationType CompilationType => (MethodCompilationType)_codeHeaderData.JITType;

        public ulong HotStart => _mdData.NativeCodeAddr;

        public uint HotSize => _codeHeaderData.HotRegionSize;

        public ulong ColdStart => _codeHeaderData.ColdRegionStart;

        public uint ColdSize => _codeHeaderData.ColdRegionSize;

        public void Dispose()
        {
            _helpers = null;
            var owner = Owner;
            Owner = null;
            owner?.Return(this);
        }
    }
}
