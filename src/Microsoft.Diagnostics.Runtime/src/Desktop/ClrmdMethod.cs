// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class ClrmdMethod : ClrMethod
    {
        private readonly IMethodHelpers _helpers;
        private string _signature;

        private readonly HotColdRegions _hotCold;
        private IReadOnlyList<ILToNativeMap> _ilMap;
        private readonly MethodAttributes _attrs;
        private ILInfo _il;

        public override ulong MethodDesc { get; }
        public override uint MetadataToken { get; }
        public override ClrType Type { get; }
        public override ulong GCInfo { get; }

        public override string Signature => _signature ?? (_signature = _helpers?.GetSignature(MethodDesc));
        public override IReadOnlyList<ILToNativeMap> ILOffsetMap => _ilMap ?? (_ilMap = _helpers?.GetILMap(NativeCode, in _hotCold));

        public override HotColdRegions HotColdInfo => _hotCold;

        public override ulong NativeCode => HotColdInfo.HotStart != 0 ? HotColdInfo.HotStart : HotColdInfo.ColdStart;

        public ClrmdMethod(ClrType type, IMethodData data)
        {
            _helpers = data.Helpers;
            
            CompilationType = data.CompilationType;
            MetadataToken = data.Token;
            GCInfo = data.GCInfo;
            _hotCold = new HotColdRegions(data.HotStart, data.HotSize, data.ColdStart, data.ColdSize);
            _attrs = type?.Module?.MetadataImport?.GetMethodAttributes(MetadataToken) ?? default;
        }


        public override string Name
        {
            get
            {
                if (Signature == null)
                    return null;

                int last = Signature.LastIndexOf('(');
                if (last > 0)
                {
                    int first = Signature.LastIndexOf('.', last - 1);

                    if (first != -1 && Signature[first - 1] == '.')
                        first--;

                    return Signature.Substring(first + 1, last - first - 1);
                }

                return "{error}";
            }
        }

        public override MethodCompilationType CompilationType { get; }

        public override int GetILOffset(ulong addr)
        {
            IReadOnlyList<ILToNativeMap> map = ILOffsetMap;
            int ilOffset = 0;
            if (map.Count > 1)
                ilOffset = map[1].ILOffset;

            for (int i = 0; i < map.Count; ++i)
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


        public override ILInfo IL => _il ?? (_il = GetILInfo());

        private ILInfo GetILInfo()
        {
            IDataReader dataReader = _helpers.DataReader;
            if (dataReader == null)
                return null;

            ClrModule module = Type?.Module;
            if (module == null)
                return null;

            MetaDataImport mdImport = module.MetadataImport;
            if (mdImport == null)
                return null;

            uint rva = mdImport.GetRva((int)MetadataToken);

            ulong il = _helpers.GetILForModule(module.Address, rva);
            if (il != 0)
            {
                ILInfo result = new ILInfo();

                if (dataReader.Read(il, out byte b))
                {
                    bool isTinyHeader = (b & (IMAGE_COR_ILMETHOD.FormatMask >> 1)) == IMAGE_COR_ILMETHOD.TinyFormat;
                    if (isTinyHeader)
                    {
                        result.Address = il + 1;
                        result.Length = b >> (int)(IMAGE_COR_ILMETHOD.FormatShift - 1);
                        result.LocalVarSignatureToken = IMAGE_COR_ILMETHOD.mdSignatureNil;
                    }
                    else if (dataReader.Read(il, out uint tmp))
                    {
                        result.Flags = tmp;
                        result.Length = dataReader.ReadUnsafe<int>(il + 4);

                        result.LocalVarSignatureToken = dataReader.ReadUnsafe<uint>(il + 8);

                        result.Address = il + 12;
                    }
                }

                return result;
            }

            return null;
        }

        public override string ToString() => Signature;
    }
}