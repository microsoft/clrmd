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
        private readonly SOSDac _sos;
        private MethodDescData _mdData;
        private CodeHeaderData _codeHeaderData;

        public MethodBuilder(SOSDac sos, IMethodHelpers helpers)
        {
            _sos = sos;
            Helpers = helpers;
        }

        public bool Init(ulong mt, int i)
        {
            ulong slot = _sos.GetMethodTableSlot(mt, i);

            if (!_sos.GetCodeHeaderData(slot, out _codeHeaderData))
                return false;

            MethodDesc = _codeHeaderData.MethodDesc;
            return _sos.GetMethodDescData(_codeHeaderData.MethodDesc, 0, out _mdData);
        }

        public bool Init(ulong methodDesc)
        {
            MethodDesc = methodDesc;
            if (!_sos.GetMethodDescData(methodDesc, 0, out _mdData))
                return false;

            ulong slot = _sos.GetMethodTableSlot(_mdData.MethodTable, _mdData.SlotNumber);

            return _sos.GetCodeHeaderData(slot, out _codeHeaderData);
        }

        public ObjectPool<MethodBuilder>? Owner { get; set; }
        public IMethodHelpers Helpers { get; }

        public uint Token => _mdData.MDToken;

        public ulong MethodDesc { get; private set; }
        public MethodCompilationType CompilationType => (MethodCompilationType)_codeHeaderData.JITType;

        public ulong HotStart => _mdData.NativeCodeAddr;

        public uint HotSize => _codeHeaderData.HotRegionSize;

        public ulong ColdStart => _codeHeaderData.ColdRegionStart;

        public uint ColdSize => _codeHeaderData.ColdRegionSize;

        public void Dispose()
        {
            var owner = Owner;
            Owner = null;
            owner?.Return(this);
        }
    }
}
