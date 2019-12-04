// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    class FieldBuilder : IFieldData, IDisposable
    {
        private readonly SOSDac _sos;
        private FieldData _fieldData;

        public IFieldHelpers Helpers { get; }

        public ClrElementType ElementType => (ClrElementType)_fieldData.ElementType;

        public uint Token => _fieldData.FieldToken;

        public int Offset => (int)_fieldData.Offset;

        public ulong TypeMethodTable => _fieldData.TypeMethodTable;

        public ObjectPool<FieldBuilder> Owner { get; set; }

        public ulong NextField => _fieldData.NextField;

        public bool IsContextLocal => _fieldData.IsContextLocal != 0;
        public bool IsStatic => _fieldData.IsStatic != 0;
        public bool IsThreadLocal => _fieldData.IsThreadLocal != 0;

        public FieldBuilder(SOSDac sos, IFieldHelpers helpers)
        {
            _sos = sos;
            Helpers = helpers;
        }

        internal bool Init(ulong fieldDesc) => _sos.GetFieldData(fieldDesc, out _fieldData);

        public void Dispose()
        {
            var owner = Owner;
            Owner = null;
            owner?.Return(this);
        }
    }
}
