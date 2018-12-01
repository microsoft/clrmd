// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
#pragma warning disable 0649
#pragma warning disable 0169

    internal class V45MethodDescDataWrapper : IMethodDescData
    {
        public bool Init(SOSDac sos, ulong md)
        {
            if (!sos.GetMethodDescData(md, 0, out MethodDescData data))
                return false;

            MethodDesc = data.MethodDesc;
            NativeCodeAddr = data.NativeCodeAddr;
            Module = data.Module;
            MDToken = data.MDToken;
            MethodTable = data.MethodTable;

            if (sos.GetCodeHeaderData(data.NativeCodeAddr, out CodeHeaderData header))
            {
                if (header.JITType == 1)
                    JITType = MethodCompilationType.Jit;
                else if (header.JITType == 2)
                    JITType = MethodCompilationType.Ngen;
                else
                    JITType = MethodCompilationType.None;

                GCInfo = header.GCInfo;
                ColdStart = header.ColdRegionStart;
                ColdSize = header.ColdRegionSize;
                HotSize = header.HotRegionSize;
            }
            else
            {
                JITType = MethodCompilationType.None;
            }

            return true;
        }

        public ulong GCInfo { get; private set; }
        public ulong MethodDesc { get; private set; }
        public ulong Module { get; private set; }
        public uint MDToken { get; private set; }
        public ulong NativeCodeAddr { get; private set; }
        public MethodCompilationType JITType { get; private set; }
        public ulong MethodTable { get; private set; }
        public ulong ColdStart { get; private set; }
        public uint ColdSize { get; private set; }
        public uint HotSize { get; private set; }
    }
}