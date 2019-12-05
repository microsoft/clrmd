// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Builders
{
    internal class RCWBuilder : IRCWData
    {
        private RCWData _rcwData;
        private readonly SOSDac _sos;
        private readonly RuntimeBuilder _builder;

        public RCWBuilder(SOSDac sos, RuntimeBuilder builder)
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

        ulong IRCWData.IUnknown => _rcwData.IUnknownPointer;
        ulong IRCWData.VTablePointer => _rcwData.VTablePointer;
        int IRCWData.RefCount => _rcwData.RefCount;
        ulong IRCWData.ManagedObject => _rcwData.ManagedObject;
        bool IRCWData.Disconnected => _rcwData.IsDisconnected != 0;
        ulong IRCWData.CreatorThread => _rcwData.CreatorThread;

        IReadOnlyList<ComInterfaceData> IRCWData.GetInterfaces()
        {
            COMInterfacePointerData[] ifs = _sos.GetRCWInterfaces(Address, _rcwData.InterfaceCount);
            return _builder.CreateComInterfaces(ifs);
        }
    }
}
