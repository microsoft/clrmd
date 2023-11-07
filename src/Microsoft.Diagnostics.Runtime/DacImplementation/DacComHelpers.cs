// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacComHelpers : IAbstractComHelpers
    {
        private readonly SOSDac _sos;

        public DacComHelpers(SOSDac sos)
        {
            _sos = sos;
        }

        public IEnumerable<ClrRcwCleanupData> EnumerateRcwCleanupData()
        {
            return _sos.EnumerateRCWCleanup(0).Select(r => new ClrRcwCleanupData(r.Rcw, r.Context, r.Thread, r.IsFreeThreaded));
        }

        public bool GetCcwInfo(ulong obj, out CcwInfo info)
        {
            info = default;
            if (_sos.GetObjectData(obj, out ObjectData objData) &&
                objData.CCW != 0 &&
                _sos.GetCCWData(objData.CCW, out CcwData ccwData))
            {
                COMInterfacePointerData[]? pointers = _sos.GetCCWInterfaces(objData.CCW, ccwData.InterfaceCount);
                info = new()
                {
                    Address = objData.CCW,
                    IUnknown = ccwData.OuterIUnknown,
                    Object = ccwData.ManagedObject,
                    Handle = ccwData.Handle,
                    RefCount = ccwData.RefCount,
                    JupiterRefCount = ccwData.JupiterRefCount,
                    Interfaces = pointers?.Select(r => new ComInterfaceEntry() { InterfacePointer = r.InterfacePointer, MethodTable = r.MethodTable }).ToArray() ?? Array.Empty<ComInterfaceEntry>()
                };
            }

            return false;
        }

        public bool GetRcwInfo(ulong obj, out RcwInfo info)
        {
            info = default;
            if (_sos.GetObjectData(obj, out ObjectData objData) &&
                objData.RCW != 0 &&
                _sos.GetRCWData(objData.RCW, out RcwData rcwData))
            {
                COMInterfacePointerData[]? pointers = _sos.GetRCWInterfaces(objData.RCW, rcwData.InterfaceCount);
                info = new()
                {
                    Address = objData.RCW,
                    IUnknown = rcwData.IUnknownPointer,
                    Object = rcwData.ManagedObject,
                    RefCount = rcwData.RefCount,
                    CreatorThread = rcwData.CreatorThread,
                    IsDisconnected = rcwData.IsDisconnected != 0,
                    VTablePointer = rcwData.VTablePointer,
                    Interfaces = pointers?.Select(r => new ComInterfaceEntry() { InterfacePointer = r.InterfacePointer, MethodTable = r.MethodTable }).ToArray() ?? Array.Empty<ComInterfaceEntry>()
                };
            }

            return false;
        }
    }
}