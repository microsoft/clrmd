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
        private readonly TargetProperties _target;

        public DacComHelpers(SOSDac sos, TargetProperties target)
        {
            _sos = sos;
            _target = target;
        }

        public IEnumerable<ClrRcwCleanupData> EnumerateRcwCleanupData()
        {
            return _sos.EnumerateRCWCleanup(ClrDataAddress.Null).Select(r => new ClrRcwCleanupData(r.Rcw.ToAddress(_target), r.Context.ToAddress(_target), r.Thread.ToAddress(_target), r.IsFreeThreaded));
        }

        public bool GetCcwInfo(ulong obj, out CcwInfo info)
        {
            info = default;
            if (_sos.GetObjectData(ClrDataAddress.FromTargetAddress(obj, _target), out ObjectData objData) &&
                !objData.CCW.IsNull &&
                _sos.GetCCWData(objData.CCW, out CcwData ccwData))
            {
                ulong ccwAddr = objData.CCW.ToAddress(_target);
                COMInterfacePointerData[]? pointers = _sos.GetCCWInterfaces(objData.CCW, ccwData.InterfaceCount);
                info = new()
                {
                    Address = ccwAddr,
                    IUnknown = ccwData.OuterIUnknown.ToAddress(_target),
                    Object = ccwData.ManagedObject.ToAddress(_target),
                    Handle = ccwData.Handle.ToAddress(_target),
                    RefCount = ccwData.RefCount,
                    JupiterRefCount = ccwData.JupiterRefCount,
                    Interfaces = pointers?.Select(r => new ComInterfaceEntry() { InterfacePointer = r.InterfacePointer.ToAddress(_target), MethodTable = r.MethodTable.ToAddress(_target) }).ToArray() ?? Array.Empty<ComInterfaceEntry>()
                };
            }

            return false;
        }

        public bool GetRcwInfo(ulong obj, out RcwInfo info)
        {
            info = default;
            if (_sos.GetObjectData(ClrDataAddress.FromTargetAddress(obj, _target), out ObjectData objData) &&
                !objData.RCW.IsNull &&
                _sos.GetRCWData(objData.RCW, out RcwData rcwData))
            {
                ulong rcwAddr = objData.RCW.ToAddress(_target);
                COMInterfacePointerData[]? pointers = _sos.GetRCWInterfaces(objData.RCW, rcwData.InterfaceCount);
                info = new()
                {
                    Address = rcwAddr,
                    IUnknown = rcwData.IUnknownPointer.ToAddress(_target),
                    Object = rcwData.ManagedObject.ToAddress(_target),
                    RefCount = rcwData.RefCount,
                    CreatorThread = rcwData.CreatorThread.ToAddress(_target),
                    IsDisconnected = rcwData.IsDisconnected != 0,
                    VTablePointer = rcwData.VTablePointer.ToAddress(_target),
                    Interfaces = pointers?.Select(r => new ComInterfaceEntry() { InterfacePointer = r.InterfacePointer.ToAddress(_target), MethodTable = r.MethodTable.ToAddress(_target) }).ToArray() ?? Array.Empty<ComInterfaceEntry>()
                };
            }

            return false;
        }
    }
}