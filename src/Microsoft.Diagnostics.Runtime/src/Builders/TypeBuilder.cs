// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    class TypeBuilder : ITypeData, IDisposable
    {
        private MethodTableData _mtData;
        private readonly SOSDac _sos;

        public TypeBuilder(SOSDac sos, ITypeHelpers helpers)
        {
            _sos = sos;
            Helpers = helpers;
        }

        public bool Init(ulong methodTable)
        {
            MethodTable = methodTable;
            if (!_sos.GetMethodTableData(MethodTable, out _mtData))
                return false;

            return true;
        }

        public ObjectPool<TypeBuilder> Owner { get; set; }

        public ITypeHelpers Helpers { get; }
        public bool IsShared => _mtData.Shared != 0;
        public uint Token => _mtData.Token;
        public ulong MethodTable { get; private set; }
        public ulong ComponentMethodTable => 0;
        public int BaseSize => (int)_mtData.BaseSize;
        public int ComponentSize => (int)_mtData.ComponentSize;
        public int MethodCount => _mtData.NumMethods;
        public bool ContainsPointers => _mtData.ContainsPointers != 0;
        public ulong ParentMethodTable => _mtData.ParentMethodTable;
        public ulong Module => _mtData.Module;

        public void Dispose()
        {
            var owner = Owner;
            Owner = null;
            owner.Return(this);
        }
    }
}
