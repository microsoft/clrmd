// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal class CCWBuilder : ICCWData
    {
        private CCWData _ccwData;
        private readonly SOSDac _sos;
        private readonly RuntimeBuilder _builder;

        public CCWBuilder(SOSDac sos, RuntimeBuilder builder)
        {
            _sos = sos;
            _builder = builder;
        }

        public bool Init(ulong obj)
        {
            if (!_sos.GetObjectData(obj, out V45ObjectData data))
                return false;

            if (data.CCW == 0)
                return false;

            Address = data.CCW;
            return _sos.GetCCWData(data.CCW, out _ccwData);
        }

        public ulong Address { get; private set; }

        ulong ICCWData.Address => _ccwData.CCWAddress;
        ulong ICCWData.IUnknown => _ccwData.OuterIUnknown;
        ulong ICCWData.Object => _ccwData.ManagedObject;
        ulong ICCWData.Handle => _ccwData.Handle;
        int ICCWData.RefCount => _ccwData.RefCount + _ccwData.JupiterRefCount;
        int ICCWData.JupiterRefCount => _ccwData.JupiterRefCount;

        ImmutableArray<ComInterfaceData> ICCWData.GetInterfaces()
        {
            COMInterfacePointerData[]? ifs = _sos.GetCCWInterfaces(Address, _ccwData.InterfaceCount);
            if (ifs is null)
                return ImmutableArray<ComInterfaceData>.Empty;

            return _builder.CreateComInterfaces(ifs);
        }
    }
}
