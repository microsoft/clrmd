// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopCCWData : CcwData
    {
        public override ulong IUnknown { get { return _ccw.IUnknown; } }
        public override ulong Object { get { return _ccw.Object; } }
        public override ulong Handle { get { return _ccw.Handle; } }
        public override int RefCount { get { return _ccw.RefCount + _ccw.JupiterRefCount; } }

        public override IList<ComInterfaceData> Interfaces
        {
            get
            {
                if (_interfaces != null)
                    return _interfaces;

                _heap.LoadAllTypes();

                _interfaces = new List<ComInterfaceData>();

                COMInterfacePointerData[] interfaces = _heap.DesktopRuntime.GetCCWInterfaces(_addr, _ccw.InterfaceCount);
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

        internal DesktopCCWData(DesktopGCHeap heap, ulong ccw, ICCWData data)
        {
            _addr = ccw;
            _ccw = data;
            _heap = heap;
        }

        private ulong _addr;
        private ICCWData _ccw;
        private DesktopGCHeap _heap;
        private List<ComInterfaceData> _interfaces;
    }
}
