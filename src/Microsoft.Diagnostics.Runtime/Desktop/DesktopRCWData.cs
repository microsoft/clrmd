// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopRCWData : RcwData
    {
        //public ulong IdentityPointer { get; }
        public override ulong IUnknown { get { return _rcw.UnknownPointer; } }
        public override ulong VTablePointer { get { return _rcw.VTablePtr; } }
        public override int RefCount { get { return _rcw.RefCount; } }
        public override ulong Object { get { return _rcw.ManagedObject; } }
        public override bool Disconnected { get { return _rcw.IsDisconnected; } }
        public override ulong WinRTObject
        {
            get { return _rcw.JupiterObject; }
        }
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

        private IRCWData _rcw;
        private DesktopGCHeap _heap;
        private uint _osThreadID;
        private List<ComInterfaceData> _interfaces;
        private ulong _addr;
    }
}
