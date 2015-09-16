// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopMethod : ClrMethod
    {
        public override string ToString()
        {
            return string.Format("<ClrMethod signature='{0}' />", _sig);
        }

        internal static DesktopMethod Create(DesktopRuntimeBase runtime, IMetadata metadata, IMethodDescData mdData)
        {
            if (mdData == null)
                return null;

            MethodAttributes attrs = (MethodAttributes)0;
            if (metadata != null)
            {
                int pClass, methodLength;
                uint blobLen, codeRva, implFlags;
                IntPtr blob;
                if (metadata.GetMethodProps(mdData.MDToken, out pClass, null, 0, out methodLength, out attrs, out blob, out blobLen, out codeRva, out implFlags) < 0)
                    attrs = (MethodAttributes)0;
            }

            return new DesktopMethod(runtime, mdData.MethodDesc, mdData, attrs);
        }

        internal static ClrMethod Create(DesktopRuntimeBase runtime, IMethodDescData mdData)
        {
            if (mdData == null)
                return null;

            DesktopModule module = runtime.GetModule(mdData.Module);
            return Create(runtime, module != null ? module.GetMetadataImport() : null, mdData);
        }

        public DesktopMethod(DesktopRuntimeBase runtime, ulong md, IMethodDescData mdData, MethodAttributes attrs)
        {
            _runtime = runtime;
            _sig = runtime.GetNameForMD(md);
            _ip = mdData.NativeCodeAddr;
            _jit = mdData.JITType;
            _attrs = attrs;
            _token = mdData.MDToken;
            var heap = (DesktopGCHeap)runtime.GetHeap();
            _type = heap.GetGCHeapType(mdData.MethodTable, 0);
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

        public override Address NativeCode
        {
            get { return _ip; }
        }

        public override MethodCompilationType CompilationType
        {
            get { return _jit; }
        }

        public override string GetFullSignature()
        {
            return _sig;
        }

        public override SourceLocation GetSourceLocationForOffset(Address nativeOffset)
        {
            ClrType type = Type;
            if (type == null)
                return null;

            DesktopModule module = (DesktopModule)type.Module;
            if (module == null)
                return null;

            if (!module.IsPdbLoaded)
            {
                string val = module.TryDownloadPdb();
                if (val == null)
                    return null;

                module.LoadPdb(val);
                if (!module.IsPdbLoaded)
                    return null;
            }

            ILToNativeMap[] map = ILOffsetMap;
            if (map == null)
                return null;

            int ilOffset = 0;
            if (map.Length > 1)
                ilOffset = map[1].ILOffset;

            for (int i = 0; i < map.Length; ++i)
            {
                //bug bug: we dont use nativeOffset
                if (map[i].StartAddress <= _ip && _ip <= map[i].EndAddress)
                {
                    ilOffset = map[i].ILOffset;
                    break;
                }
            }

            return module.GetSourceInformation(MetadataToken, ilOffset);
        }

        public override bool IsStatic
        {
            get { return (_attrs & MethodAttributes.Static) == MethodAttributes.Static; }
        }

        public override bool IsFinal
        {
            get { return (_attrs & MethodAttributes.Final) == MethodAttributes.Final; }
        }

        public override bool IsPInvoke
        {
            get { return (_attrs & MethodAttributes.PinvokeImpl) == MethodAttributes.PinvokeImpl; }
        }

        public override bool IsVirtual
        {
            get { return (_attrs & MethodAttributes.Virtual) == MethodAttributes.Virtual; }
        }

        public override bool IsAbstract
        {
            get { return (_attrs & MethodAttributes.Abstract) == MethodAttributes.Abstract; }
        }


        public override bool IsPublic
        {
            get { return (_attrs & MethodAttributes.MemberAccessMask) == MethodAttributes.Public; }
        }

        public override bool IsPrivate
        {
            get { return (_attrs & MethodAttributes.MemberAccessMask) == MethodAttributes.Private; }
        }

        public override bool IsInternal
        {
            get
            {
                MethodAttributes access = (_attrs & MethodAttributes.MemberAccessMask);
                return access == MethodAttributes.Assembly || access == MethodAttributes.FamANDAssem;
            }
        }

        public override bool IsProtected
        {
            get
            {
                MethodAttributes access = (_attrs & MethodAttributes.MemberAccessMask);
                return access == MethodAttributes.Family || access == MethodAttributes.FamANDAssem || access == MethodAttributes.FamORAssem;
            }
        }

        public override bool IsSpecialName
        {
            get
            {
                return (_attrs & MethodAttributes.SpecialName) == MethodAttributes.SpecialName;
            }
        }

        public override bool IsRTSpecialName
        {
            get
            {
                return (_attrs & MethodAttributes.RTSpecialName) == MethodAttributes.RTSpecialName;
            }
        }


        public override ILToNativeMap[] ILOffsetMap
        {
            get
            {
                if (_ilMap == null)
                    _ilMap = _runtime.GetILMap(_ip);

                return _ilMap;
            }
        }

        public override uint MetadataToken
        {
            get { return _token; }
        }

        public override ClrType Type
        {
            get { return _type; }
        }

        private uint _token;
        private ILToNativeMap[] _ilMap;
        private string _sig;
        private ulong _ip;
        private MethodCompilationType _jit;
        private MethodAttributes _attrs;
        private DesktopRuntimeBase _runtime;
        private ClrType _type;
    }
}
