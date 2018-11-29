// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

            _md = data.MethodDesc;
            _ip = data.NativeCodeAddr;
            _module = data.Module;
            _token = data.MDToken;
            _mt = data.MethodTable;

            if (sos.GetCodeHeaderData(data.NativeCodeAddr, out CodeHeaderData header))
            {
                if (header.JITType == 1)
                    _jitType = MethodCompilationType.Jit;
                else if (header.JITType == 2)
                    _jitType = MethodCompilationType.Ngen;
                else
                    _jitType = MethodCompilationType.None;

                _gcInfo = header.GCInfo;
                _coldStart = header.ColdRegionStart;
                _coldSize = header.ColdRegionSize;
                _hotSize = header.HotRegionSize;
            }
            else
            {
                _jitType = MethodCompilationType.None;
            }

            return true;
        }

        private MethodCompilationType _jitType;
        private ulong _gcInfo, _md, _module, _ip, _coldStart;
        private uint _token, _coldSize, _hotSize;
        private ulong _mt;
        
        public ulong GCInfo
        {
            get
            {
                return _gcInfo;
            }
        }

        public ulong MethodDesc
        {
            get { return _md; }
        }

        public ulong Module
        {
            get { return _module; }
        }

        public uint MDToken
        {
            get { return _token; }
        }

        public ulong NativeCodeAddr
        {
            get { return _ip; }
        }

        public MethodCompilationType JITType
        {
            get { return _jitType; }
        }


        public ulong MethodTable
        {
            get { return _mt; }
        }

        public ulong ColdStart
        {
            get { return _coldStart; }
        }

        public uint ColdSize
        {
            get { return _coldSize; }
        }

        public uint HotSize
        {
            get { return _hotSize; }
        }
    }
}
