// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdMethod : ClrMethod
    {
        private readonly IMethodHelpers _helpers;
        private string? _signature;
        private readonly HotColdRegions _hotCold;
        private readonly MethodAttributes _attrs;

        public override ulong MethodDesc { get; }
        public override int MetadataToken { get; }
        public override ClrType Type { get; }
        public override string? Signature
        {
            get
            {
                if (_signature != null)
                    return _signature;

                // returns whether we should cache the signature or not.
                if (_helpers.GetSignature(MethodDesc, out string? signature))
                    _signature = signature;

                return signature;
            }
        }

        public override ImmutableArray<ILToNativeMap> ILOffsetMap => _helpers?.GetILMap(NativeCode, _hotCold) ?? default;

        public override HotColdRegions HotColdInfo => _hotCold;

        public override ulong NativeCode => HotColdInfo.HotStart != 0 ? HotColdInfo.HotStart : HotColdInfo.ColdStart;

        public ClrmdMethod(ClrType type, IMethodData data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));
            _helpers = data.Helpers;

            Type = type;
            MethodDesc = data.MethodDesc;
            CompilationType = data.CompilationType;
            MetadataToken = data.Token;
            _hotCold = new HotColdRegions(data.HotStart, data.HotSize, data.ColdStart, data.ColdSize);
            _attrs = type?.Module?.MetadataImport?.GetMethodAttributes(MetadataToken) ?? default;
        }

        public override string? Name
        {
            get
            {
                string? signature = Signature;
                if (signature is null)
                    return null;

                int last = signature.LastIndexOf('(');
                if (last > 0)
                {
                    int first = signature.LastIndexOf('.', last - 1);

                    if (first != -1 && signature[first - 1] == '.')
                        first--;

                    return signature.Substring(first + 1, last - first - 1);
                }

                return "{error}";
            }
        }

        public override MethodCompilationType CompilationType { get; }

        public override int GetILOffset(ulong addr)
        {
            ImmutableArray<ILToNativeMap> map = ILOffsetMap;
            if (map.IsDefault)
                return 0;

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

        public override ILInfo? IL => GetILInfo();

        private ILInfo? GetILInfo()
        {
            IDataReader? dataReader = _helpers.DataReader;
            if (dataReader is null)
                return null;

            ClrModule? module = Type?.Module;
            if (module is null)
                return null;

            MetadataImport? mdImport = module.MetadataImport;
            if (mdImport is null)
                return null;

            uint rva = mdImport.GetRva(MetadataToken);

            ulong il = _helpers.GetILForModule(module.Address, rva);
            if (il != 0)
            {
                if (dataReader.Read(il, out byte b))
                {
                    bool isTinyHeader = (b & (IMAGE_COR_ILMETHOD.FormatMask >> 1)) == IMAGE_COR_ILMETHOD.TinyFormat;
                    if (isTinyHeader)
                    {
                        ulong address = il + 1;
                        int len = b >> (int)(IMAGE_COR_ILMETHOD.FormatShift - 1);
                        uint localToken = IMAGE_COR_ILMETHOD.mdSignatureNil;

                        return new ILInfo(address, len, 0, localToken);
                    }
                    else if (dataReader.Read(il, out uint flags))
                    {
                        int len = dataReader.Read<int>(il + 4);
                        uint localToken = dataReader.Read<uint>(il + 8);
                        ulong address = il + 12;

                        return new ILInfo(address, len, flags, localToken);
                    }
                }
            }

            return null;
        }

        public override string? ToString() => Signature;
    }
}
