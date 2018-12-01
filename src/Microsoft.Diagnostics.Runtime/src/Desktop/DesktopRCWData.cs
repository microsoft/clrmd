// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopRCWData : RcwData
    {
        //public ulong IdentityPointer { get; }
        public override ulong IUnknown => _rcw.UnknownPointer;
        public override ulong VTablePointer => _rcw.VTablePtr;
        public override int RefCount => _rcw.RefCount;
        public override ulong Object => _rcw.ManagedObject;
        public override bool Disconnected => _rcw.IsDisconnected;
        public override ulong WinRTObject => _rcw.JupiterObject;
        public override uint CreatorThread
        {
            get
            {
                if (_osThreadID == uint.MaxValue)
                {
                    IThreadData data = _heap.DesktopRuntime.GetThread(_rcw.CreatorThread);
                    if (data == null || data.OSThreadID == uint.MaxValue)
                        _osThreadID = 0;
                    else
                        _osThreadID = data.OSThreadID;
                }

                return _osThreadID;
            }
        }

        public override IList<ComInterfaceData> Interfaces
        {
            get
            {
                if (_interfaces != null)
                    return _interfaces;

                _heap.LoadAllTypes();

                _interfaces = new List<ComInterfaceData>();

                COMInterfacePointerData[] interfaces = _heap.DesktopRuntime.GetRCWInterfaces(_addr, _rcw.InterfaceCount);
                for (int i = 0; i < interfaces.Length; ++i)
                {
                    ClrType type = null;
                    if (interfaces[i].MethodTable != 0)
                        type = _heap.GetTypeByMethodTable(interfaces[i].MethodTable, 0);

                    _interfaces.Add(new DesktopInterfaceData(type, interfaces[i].InterfacePointer));
                }

                return _interfaces;
            }
        }

        internal DesktopRCWData(DesktopGCHeap heap, ulong rcw, IRCWData data)
        {
            _addr = rcw;
            _rcw = data;
            _heap = heap;
            _osThreadID = uint.MaxValue;
        }

        private readonly IRCWData _rcw;
        private readonly DesktopGCHeap _heap;
        private uint _osThreadID;
        private List<ComInterfaceData> _interfaces;
        private readonly ulong _addr;
    }
}