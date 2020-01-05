// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal class RcwBuilder : IRcwData
    {
        private RcwData _rcwData;
        private readonly SOSDac _sos;
        private readonly RuntimeBuilder _builder;

        public RcwBuilder(SOSDac sos, RuntimeBuilder builder)
        {
            _sos = sos;
            _builder = builder;
        }

        public bool Init(ulong obj)
        {
            if (!_sos.GetObjectData(obj, out V45ObjectData data))
                return false;

            if (data.RCW == 0)
                return false;

            Address = data.RCW;
            return _sos.GetRCWData(data.RCW, out _rcwData);
        }

        public ulong Address { get; private set; }

        ulong IRcwData.IUnknown => _rcwData.IUnknownPointer;
        ulong IRcwData.VTablePointer => _rcwData.VTablePointer;
        int IRcwData.RefCount => _rcwData.RefCount;
        ulong IRcwData.ManagedObject => _rcwData.ManagedObject;
        bool IRcwData.Disconnected => _rcwData.IsDisconnected != 0;
        ulong IRcwData.CreatorThread => _rcwData.CreatorThread;

        ImmutableArray<ComInterfaceData> IRcwData.GetInterfaces()
        {
            COMInterfacePointerData[]? ifs = _sos.GetRCWInterfaces(Address, _rcwData.InterfaceCount);
            if (ifs is null)
                return ImmutableArray<ComInterfaceData>.Empty;

            return _builder.CreateComInterfaces(ifs);
        }
    }
}
