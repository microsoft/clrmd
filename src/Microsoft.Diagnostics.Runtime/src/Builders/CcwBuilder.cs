// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal class CcwBuilder : ICcwData
    {
        private CcwData _ccwData;
        private readonly SOSDac _sos;
        private readonly IClrTypeFactory _factory;

        public CcwBuilder(SOSDac sos, IClrTypeFactory factory)
        {
            _sos = sos;
            _factory = factory;
        }

        public bool Init(ulong obj)
        {
            if (!_sos.GetObjectData(obj, out ObjectData data))
                return false;

            if (data.CCW == 0)
                return false;

            Address = data.CCW;
            return _sos.GetCCWData(data.CCW, out _ccwData);
        }

        public ulong Address { get; private set; }

        ulong ICcwData.Address => _ccwData.CCWAddress;
        ulong ICcwData.IUnknown => _ccwData.OuterIUnknown;
        ulong ICcwData.Object => _ccwData.ManagedObject;
        ulong ICcwData.Handle => _ccwData.Handle;
        int ICcwData.RefCount => _ccwData.RefCount + _ccwData.JupiterRefCount;
        int ICcwData.JupiterRefCount => _ccwData.JupiterRefCount;

        ImmutableArray<ComInterfaceData> ICcwData.GetInterfaces() => _factory.GetCCWInterfaces(Address, _ccwData.InterfaceCount);
    }
}
