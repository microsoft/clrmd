// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacMethodLocator : IAbstractMethodLocator
    {
        private readonly SOSDac _sos;

        public DacMethodLocator(SOSDac sos)
        {
            _sos = sos;
        }

        public ulong GetMethodHandleContainingType(ulong methodDesc)
        {
            if (!_sos.GetMethodDescData(methodDesc, 0, out MethodDescData mdData))
                return 0;

            return mdData.MethodTable;
        }

        public ulong GetMethodHandleByInstructionPointer(ulong ip)
        {
            ulong md = _sos.GetMethodDescPtrFromIP(ip);
            if (md == 0)
            {
                if (_sos.GetCodeHeaderData(ip, out CodeHeaderData codeHeaderData))
                    md = codeHeaderData.MethodDesc;
            }

            return md;
        }

        public bool GetMethodInfo(ulong methodDesc, out MethodInfo methodInfo)
        {
            if (!_sos.GetMethodDescData(methodDesc, 0, out MethodDescData mdd))
            {
                methodInfo = default;
                return false;
            }

            if (mdd.HasNativeCode != 0 && _sos.GetCodeHeaderData(mdd.NativeCodeAddr, out CodeHeaderData chd))
            {
                methodInfo = new()
                {
                    CompilationType = (MethodCompilationType)chd.JITType,
                    HotCold = new(mdd.NativeCodeAddr, chd.HotRegionSize, chd.ColdRegionStart, chd.ColdRegionSize),
                    MethodDesc = chd.MethodDesc,
                    Token = (int)mdd.MDToken
                };
            }
            else
            {
                // Method has not been JIT compiled (e.g. unboxing stubs, un-jitted methods)
                methodInfo = new()
                {
                    CompilationType = MethodCompilationType.None,
                    HotCold = default,
                    MethodDesc = methodDesc,
                    Token = (int)mdd.MDToken
                };
            }

            return true;
        }
    }
}