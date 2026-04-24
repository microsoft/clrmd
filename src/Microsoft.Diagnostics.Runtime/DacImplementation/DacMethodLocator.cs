// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacMethodLocator : IAbstractMethodLocator
    {
        private readonly SOSDac _sos;
        private readonly IDataReader _dataReader;
        private readonly TargetProperties _target;

        public DacMethodLocator(SOSDac sos, IDataReader dataReader, TargetProperties target)
        {
            _sos = sos;
            _dataReader = dataReader;
            _target = target;
        }

        public ulong GetMethodHandleContainingType(ulong methodDesc)
        {
            if (!_sos.GetMethodDescData(ClrDataAddress.FromTargetAddress(methodDesc, _target), ClrDataAddress.Null, out MethodDescData mdData))
                return 0;

            return mdData.MethodTable.ToAddress(_target);
        }

        public ulong GetMethodHandleByInstructionPointer(ulong ip)
        {
            ulong md = _sos.GetMethodDescPtrFromIP(ClrDataAddress.FromTargetAddress(ip, _target)).ToAddress(_target);
            if (md == 0)
            {
                if (_sos.GetCodeHeaderData(ClrDataAddress.FromTargetAddress(ip, _target), out CodeHeaderData codeHeaderData))
                    md = codeHeaderData.MethodDesc.ToAddress(_target);
            }

            return md;
        }

        public bool GetMethodInfo(ulong methodDesc, out MethodInfo methodInfo)
        {
            if (!_sos.GetMethodDescData(ClrDataAddress.FromTargetAddress(methodDesc, _target), ClrDataAddress.Null, out MethodDescData mdd))
            {
                methodInfo = default;
                return false;
            }

            if (mdd.HasNativeCode != 0 && _sos.GetCodeHeaderData(mdd.NativeCodeAddr, out CodeHeaderData chd))
            {
                ulong nativeCodeAddr = mdd.NativeCodeAddr.ToAddress(_target);
                methodInfo = new()
                {
                    CompilationType = (MethodCompilationType)chd.JITType,
                    HotCold = new(nativeCodeAddr, chd.HotRegionSize, chd.ColdRegionStart.ToAddress(_target), chd.ColdRegionSize),
                    MethodDesc = chd.MethodDesc.ToAddress(_target),
                    Token = (int)mdd.MDToken
                };
            }
            else
            {
                // Fall back to slot-based lookup (issue #935). Reference-type generic
                // instantiations share code via canonical method descs and may not have
                // NativeCodeAddr set, but the method table slot points to the shared code.
                ClrDataAddress slot = _sos.GetMethodTableSlot(mdd.MethodTable, mdd.SlotNumber);
                if (!slot.IsNull && _sos.GetCodeHeaderData(slot, out CodeHeaderData slotChd))
                {
                    ulong slotAddr = slot.ToAddress(_target);
                    methodInfo = new()
                    {
                        CompilationType = (MethodCompilationType)slotChd.JITType,
                        HotCold = new(slotAddr, slotChd.HotRegionSize, slotChd.ColdRegionStart.ToAddress(_target), slotChd.ColdRegionSize),
                        MethodDesc = methodDesc,
                        Token = (int)mdd.MDToken
                    };
                }
                else
                {
                    // Method has no native code by either path (not yet JIT compiled)
                    methodInfo = new()
                    {
                        CompilationType = MethodCompilationType.None,
                        HotCold = default,
                        MethodDesc = methodDesc,
                        Token = (int)mdd.MDToken
                    };
                }
            }

            return true;
        }
    }
}
