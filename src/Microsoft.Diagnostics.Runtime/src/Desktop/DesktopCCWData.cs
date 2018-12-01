// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopCCWData : CcwData
    {
        public override ulong IUnknown => _ccw.IUnknown;
        public override ulong Object => _ccw.Object;
        public override ulong Handle => _ccw.Handle;
        public override int RefCount => _ccw.RefCount + _ccw.JupiterRefCount;

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

        private readonly ulong _addr;
        private readonly ICCWData _ccw;
        private readonly DesktopGCHeap _heap;
        private List<ComInterfaceData> _interfaces;
    }
}