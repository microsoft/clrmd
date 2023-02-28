// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal class RcwHelpers : IRcwHelpers
    {
        private RcwData _rcwData;
        private readonly SOSDac _sos;
        private readonly IClrTypeFactory _factory;

        public RcwHelpers(SOSDac sos, IClrTypeFactory factory)
        {
            _sos = sos;
            _factory = factory;
        }

        public bool Init(ulong obj)
        {
            if (!_sos.GetObjectData(obj, out ObjectData data))
                return false;

            if (data.RCW == 0)
                return false;

            Address = data.RCW;
            return _sos.GetRCWData(data.RCW, out _rcwData);
        }

        public ulong Address { get; private set; }

        ulong IRcwHelpers.IUnknown => _rcwData.IUnknownPointer;
        ulong IRcwHelpers.VTablePointer => _rcwData.VTablePointer;
        int IRcwHelpers.RefCount => _rcwData.RefCount;
        ulong IRcwHelpers.ManagedObject => _rcwData.ManagedObject;
        bool IRcwHelpers.Disconnected => _rcwData.IsDisconnected != 0;
        ulong IRcwHelpers.CreatorThread => _rcwData.CreatorThread;

        ImmutableArray<ComInterfaceData> IRcwHelpers.GetInterfaces()
        {
            return _factory.GetRCWInterfaces(Address, _rcwData.InterfaceCount);
        }

    }
}
