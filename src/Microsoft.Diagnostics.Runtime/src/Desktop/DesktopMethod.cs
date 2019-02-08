// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopMethod : ClrMethod
    {
        public override string ToString()
        {
            return _sig;
        }

        internal static DesktopMethod Create(DesktopRuntimeBase runtime, MetaDataImport metadata, IMethodDescData mdData)
        {
            if (mdData == null)
                return null;

            MethodAttributes attrs = new MethodAttributes();
            if (metadata != null)
                attrs = metadata.GetMethodAttributes((int)mdData.MDToken);

            return new DesktopMethod(runtime, mdData.MethodDesc, mdData, attrs);
        }

        internal void AddMethodHandle(ulong methodDesc)
        {
            if (_methodHandles == null)
                _methodHandles = new List<ulong>(1);

            _methodHandles.Add(methodDesc);
        }

        public override ulong MethodDesc
        {
            get
            {
                if (_methodHandles != null && _methodHandles[0] != 0)
                    return _methodHandles[0];

                return EnumerateMethodDescs().FirstOrDefault();
            }
        }

        public override IEnumerable<ulong> EnumerateMethodDescs()
        {
            if (_methodHandles == null)
                _type?.InitMethodHandles();

            if (_methodHandles == null)
                _methodHandles = new List<ulong>();

            return _methodHandles;
        }

        internal static ClrMethod Create(DesktopRuntimeBase runtime, IMethodDescData mdData)
        {
            if (mdData == null)
                return null;

            DesktopModule module = runtime.GetModule(mdData.Module);
            return Create(runtime, module?.GetMetadataImport(), mdData);
        }

        public DesktopMethod(DesktopRuntimeBase runtime, ulong md, IMethodDescData mdData, MethodAttributes attrs)
        {
            _runtime = runtime;
            _sig = runtime.GetNameForMD(md);
            _ip = mdData.NativeCodeAddr;
            CompilationType = mdData.JITType;
            _attrs = attrs;
            _token = mdData.MDToken;
            GCInfo = mdData.GCInfo;
            ClrHeap heap = runtime.Heap;
            _type = (DesktopHeapType)heap.GetTypeByMethodTable(mdData.MethodTable, 0);
            HotColdInfo = new HotColdRegions {HotStart = _ip, HotSize = mdData.HotSize, ColdStart = mdData.ColdStart, ColdSize = mdData.ColdSize};
        }

        public override string Name
        {
            get
            {
                if (_sig == null)
                    return null;

                int last = _sig.LastIndexOf('(');
                if (last > 0)
                {
                    int first = _sig.LastIndexOf('.', last - 1);

                    if (first != -1 && _sig[first - 1] == '.')
                        first--;

                    return _sig.Substring(first + 1, last - first - 1);
                }

                return "{error}";
            }
        }

        public override ulong NativeCode => _ip;

        public override MethodCompilationType CompilationType { get; }

        public override string GetFullSignature()
        {
            return _sig;
        }

        public override int GetILOffset(ulong addr)
        {
            ILToNativeMap[] map = ILOffsetMap;
            int ilOffset = 0;
            if (map.Length > 1)
                ilOffset = map[1].ILOffset;

            for (int i = 0; i < map.Length; ++i)
                if (map[i].StartAddress <= addr && addr <= map[i].EndAddress)
                    return map[i].ILOffset;

            return ilOffset;
        }

        public override bool IsStatic => (_attrs & MethodAttributes.Static) == MethodAttributes.Static;

        public override bool IsFinal => (_attrs & MethodAttributes.Final) == MethodAttributes.Final;

        public override bool IsPInvoke => (_attrs & MethodAttributes.PinvokeImpl) == MethodAttributes.PinvokeImpl;

        public override bool IsVirtual => (_attrs & MethodAttributes.Virtual) == MethodAttributes.Virtual;

        public override bool IsAbstract => (_attrs & MethodAttributes.Abstract) == MethodAttributes.Abstract;

        public override bool IsPublic => (_attrs & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;

        public override bool IsPrivate => (_attrs & MethodAttributes.MemberAccessMask) == MethodAttributes.Private;

        public override bool IsInternal
        {
            get
            {
                MethodAttributes access = _attrs & MethodAttributes.MemberAccessMask;
                return access == MethodAttributes.Assembly || access == MethodAttributes.FamANDAssem;
            }
        }

        public override bool IsProtected
        {
            get
            {
                MethodAttributes access = _attrs & MethodAttributes.MemberAccessMask;
                return access == MethodAttributes.Family || access == MethodAttributes.FamANDAssem || access == MethodAttributes.FamORAssem;
            }
        }

        public override bool IsSpecialName => (_attrs & MethodAttributes.SpecialName) == MethodAttributes.SpecialName;

        public override bool IsRTSpecialName => (_attrs & MethodAttributes.RTSpecialName) == MethodAttributes.RTSpecialName;

        public override HotColdRegions HotColdInfo { get; }

        public override ILToNativeMap[] ILOffsetMap
        {
            get
            {
                if (_ilMap == null)
                    _ilMap = _runtime.GetILMap(_ip);

                return _ilMap;
            }
        }

        public override uint MetadataToken => _token;

        public override ClrType Type => _type;

        public override ulong GCInfo { get; }

        public override ILInfo IL
        {
            get
            {
                if (_il == null)
                    InitILInfo();

                return _il;
            }
        }

        private void InitILInfo()
        {
            ClrModule module = Type?.Module;
            object mdImport = module?.MetadataImport;
            uint rva = 0;
            if (mdImport is IMetadataImport metadataImport)
            {
                if (metadataImport.GetRVA(_token, out rva, out uint flags) != 0)
                {
                    // GetRVA fail
                    return;
                }
            }
            else if (mdImport is MetaDataImport dacMetaDataImport)
            {
                rva = dacMetaDataImport.GetRva((int) _token);
            }
            ulong il = _runtime.GetILForModule(module, rva);
            if (il != 0)
            {
                _il = new ILInfo();

                if (_runtime.ReadByte(il, out byte b))
                {
                    bool isTinyHeader = (b & (IMAGE_COR_ILMETHOD.FormatMask >> 1)) == IMAGE_COR_ILMETHOD.TinyFormat;
                    if (isTinyHeader)
                    {
                        _il.Address = il + 1;
                        _il.Length = b >> (int) (IMAGE_COR_ILMETHOD.FormatShift - 1);
                        _il.LocalVarSignatureToken = IMAGE_COR_ILMETHOD.mdSignatureNil;
                    }
                    else if (_runtime.ReadDword(il, out uint tmp))
                    {
                        _il.Flags = tmp;
                        _runtime.ReadDword(il + 4, out tmp);
                        _il.Length = (int) tmp;
                        _runtime.ReadDword(il + 8, out tmp);
                        _il.LocalVarSignatureToken = tmp;
                        _il.Address = il + 12;
                    }
                }
            }
        }

        private readonly uint _token;
        private ILToNativeMap[] _ilMap;
        private readonly string _sig;
        private readonly ulong _ip;
        private readonly MethodAttributes _attrs;
        private readonly DesktopRuntimeBase _runtime;
        private readonly DesktopHeapType _type;
        private List<ulong> _methodHandles;
        private ILInfo _il;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IMAGE_COR_ILMETHOD
    {
        public uint FlagsSizeStack;
        public uint CodeSize;
        public uint LocalVarSignatureToken;

        public const uint FormatShift = 3;
        public const uint FormatMask = (uint)(1 << (int)FormatShift) - 1;
        public const uint TinyFormat = 0x2;
        public const uint mdSignatureNil = 0x11000000;
    }
}