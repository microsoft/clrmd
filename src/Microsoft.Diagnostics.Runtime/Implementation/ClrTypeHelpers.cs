// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;
using FieldInfo = Microsoft.Diagnostics.Runtime.AbstractDac.FieldInfo;
using MethodInfo = Microsoft.Diagnostics.Runtime.AbstractDac.MethodInfo;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class ClrTypeHelpers : IClrTypeHelpers
    {
        private readonly string UnloadedTypeName = "<Unloaded Type>";

        private readonly ClrDataProcess _clrDataProcess;
        private readonly SOSDac _sos;
        private readonly SOSDac6? _sos6;
        private readonly SOSDac8? _sos8;
        private readonly IClrTypeFactory _typeFactory;

        public ClrHeap Heap { get; }


        public IDataReader DataReader { get; }

        public ClrTypeHelpers(ClrDataProcess clrDataProcess, SOSDac sos, SOSDac6? sos6, SOSDac8? sos8, IClrTypeFactory typeFactory, ClrHeap heap)
        {
            _clrDataProcess = clrDataProcess;
            _sos = sos;
            _sos6 = sos6;
            _sos8 = sos8;
            _typeFactory = typeFactory;
            Heap = heap;
            DataReader = heap.Runtime.DataTarget.DataReader;
        }

        public string? GetTypeName(ulong methodTable)
        {
            string? name = _sos.GetMethodTableName(methodTable);
            if (string.IsNullOrWhiteSpace(name) || name == UnloadedTypeName)
                return null;

            if (name == UnloadedTypeName)
                return null;

            name = DacNameParser.Parse(name);
            return name;
        }

        public string? GetTypeName(MetadataImport import, int token)
        {
            string? name = GetNameFromToken(import, token);
            if (string.IsNullOrWhiteSpace(name))
                return null;

            name = DacNameParser.Parse(name);
            return name;
        }

        private static string? GetNameFromToken(MetadataImport? import, int token)
        {
            if (import is not null)
            {
                HResult hr = import.GetTypeDefProperties(token, out string? name, out _, out _);
                if (hr && name is not null)
                {
                    hr = import.GetNestedClassProperties(token, out int enclosingToken);
                    if (hr && enclosingToken != 0 && enclosingToken != token)
                    {
                        string? inner = GetNameFromToken(import, enclosingToken) ?? "<UNKNOWN>";
                        name += $"+{inner}";
                    }

                    return name;
                }
            }

            return null;
        }

        public ulong GetLoaderAllocatorHandle(ulong mt)
        {
            if (_sos6 != null && _sos6.GetMethodTableCollectibleData(mt, out MethodTableCollectibleData data) && data.Collectible != 0)
                return data.LoaderAllocatorObjectHandle;

            return 0;
        }

        public ulong GetAssemblyLoadContextAddress(ulong mt)
        {
            if (_sos8 != null && _sos8.GetAssemblyLoadContext(mt, out ClrDataAddress assemblyLoadContext))
                return assemblyLoadContext;

            return 0;
        }

        public bool GetObjectArrayInformation(ulong objRef, out ObjectArrayInformation data)
        {
            data = default;
            if (_sos.GetObjectData(objRef, out ObjectData objData))
            {
                data.ComponentType = (ClrElementType)objData.ElementType;
                ulong dataPointer = objData.ArrayDataPointer > objRef ? objData.ArrayDataPointer - objRef : 0;
                data.DataPointer = dataPointer > int.MaxValue ? int.MaxValue : (int)dataPointer;
                return true;
            }

            return false;
        }

        public IEnumerable<MethodInfo> EnumerateMethodsForType(ulong methodTable)
        {
            if (!_sos.GetMethodTableData(methodTable, out MethodTableData data) || data.NumMethods == 0)
                yield break;

            for (uint i = 0; i < data.NumMethods; i++)
            {
                ulong slot = _sos.GetMethodTableSlot(methodTable, i);
                if (_sos.GetCodeHeaderData(slot, out CodeHeaderData chd) && _sos.GetMethodDescData(chd.MethodDesc, 0, out MethodDescData mdd))
                {
                    HotColdRegions regions = new(mdd.NativeCodeAddr, chd.HotRegionSize, chd.ColdRegionStart, chd.ColdRegionSize);
                    yield return new()
                    {
                        MethodDesc = chd.MethodDesc,
                        Token = (int)mdd.MDToken,
                        CompilationType = (MethodCompilationType)chd.JITType,
                        HotCold = regions,
                    };
                }
            }
        }

        public IEnumerable<ClrField> EnumerateFields(ClrType type)
        {
            int baseFieldCount = 0;
            IEnumerable<ClrField> result = Enumerable.Empty<ClrField>();
            if (type.BaseType is not null)
            {
                result = result.Concat(type.BaseType.Fields);
                baseFieldCount = type.BaseType.Fields.Length;
            }

            return result.Concat(EnumerateFieldsWorker(type, baseFieldCount));
        }

        private IEnumerable<ClrField> EnumerateFieldsWorker(ClrType type, int baseFieldCount)
        {
            if (!_sos.GetFieldInfo(type.MethodTable, out MethodTableFieldInfo fieldInfo) || fieldInfo.FirstFieldAddress == 0)
                yield break;

            ulong nextField = fieldInfo.FirstFieldAddress;
            for (int i = baseFieldCount; i < fieldInfo.NumInstanceFields + fieldInfo.NumStaticFields; i++)
            {
                if (!_sos.GetFieldData(nextField, out FieldData dacFieldData))
                    break;

                FieldInfo fi = new()
                {
                    FieldDesc = nextField,
                    ElementType = (ClrElementType)dacFieldData.ElementType,
                    Offset = dacFieldData.Offset <= int.MaxValue ? (int)dacFieldData.Offset : int.MaxValue,
                    Token = dacFieldData.FieldToken <= int.MaxValue ? (int)dacFieldData.FieldToken : int.MaxValue
                };

                if (dacFieldData.IsContextLocal == 0 && dacFieldData.IsThreadLocal == 0)
                {
                    ClrType? fieldType = _typeFactory.GetOrCreateType(dacFieldData.TypeMethodTable, 0);
                    if (dacFieldData.IsStatic != 0)
                        yield return new ClrStaticField(type, fieldType, this, fi);
                    else
                        yield return new ClrInstanceField(type, fieldType, this, fi);
                }

                nextField = dacFieldData.NextField;
            }
        }

        public bool GetFieldMetadataInfo(MetadataImport import, int token, out FieldMetadataInfo info)
        {
            if (!import.GetFieldProps(token, out string? name, out FieldAttributes attributes, out nint fieldSig, out int sigLen, out _, out _))
            {
                info = default;
                return false;
            }

            info = new()
            {
                Name = name,
                Attributes = attributes,
                Signature = fieldSig,
                SignatureSize = sigLen,
            };

            return true;
        }

        public ulong GetStaticFieldAddress(ClrStaticField field, ulong appDomain)
        {
            if (appDomain == 0)
                return 0;

            ClrType type = field.ContainingType;
            ClrModule? module = type.Module;
            if (module is null)
                return 0;

            bool shared = type.IsShared;

            // TODO: Perf and testing
            if (shared)
            {
                if (!_sos.GetModuleData(module.Address, out ModuleData data))
                    return 0;

                if (!_sos.GetDomainLocalModuleDataFromAppDomain(appDomain, (int)data.ModuleID, out DomainLocalModuleData dlmd))
                    return 0;

                if (!shared && !IsInitialized(dlmd, type.MetadataToken))
                    return 0;

                if (field.ElementType.IsPrimitive())
                    return dlmd.NonGCStaticDataStart + (uint)field.Offset;
                else
                    return dlmd.GCStaticDataStart + (uint)field.Offset;
            }
            else
            {
                if (!_sos.GetDomainLocalModuleDataFromModule(module.Address, out DomainLocalModuleData dlmd))
                    return 0;

                if (field.ElementType.IsPrimitive())
                    return dlmd.NonGCStaticDataStart + (uint)field.Offset;
                else
                    return dlmd.GCStaticDataStart + (uint)field.Offset;
            }
        }

        private bool IsInitialized(in DomainLocalModuleData data, int token)
        {
            ulong flagsAddr = data.ClassData + (uint)(token & ~0x02000000u) - 1;
            if (!DataReader.Read(flagsAddr, out byte flags))
                return false;

            return (flags & 1) != 0;
        }

        // Method helpers

        public string? GetMethodSignature(ulong methodDesc) => _sos.GetMethodDescName(methodDesc);

        public ulong GetILForModule(ulong address, uint rva) => _sos.GetILForModule(address, rva);

        public ImmutableArray<ILToNativeMap> GetILMap(ulong ip, in HotColdRegions hotCold)
        {
            ImmutableArray<ILToNativeMap>.Builder result = ImmutableArray.CreateBuilder<ILToNativeMap>();

            foreach (ClrDataMethod method in _clrDataProcess.EnumerateMethodInstancesByAddress(ip))
            {
                ILToNativeMap[]? map = method.GetILToNativeMap();
                if (map != null)
                {
                    for (int i = 0; i < map.Length; i++)
                    {
                        if (map[i].StartAddress > map[i].EndAddress)
                        {
                            if (i + 1 == map.Length)
                                map[i].EndAddress = FindEnd(hotCold, map[i].StartAddress);
                            else
                                map[i].EndAddress = map[i + 1].StartAddress - 1;
                        }
                    }

                    result.AddRange(map);
                }

                method.Dispose();
            }

            return result.MoveOrCopyToImmutable();
        }

        private static ulong FindEnd(in HotColdRegions reg, ulong address)
        {
            ulong hotEnd = reg.HotStart + reg.HotSize;
            if (reg.HotStart <= address && address < hotEnd)
                return hotEnd;

            ulong coldEnd = reg.ColdStart + reg.ColdSize;
            if (reg.ColdStart <= address && address < coldEnd)
                return coldEnd;

            // Shouldn't reach here, but give a sensible answer if we do.
            return address + 0x20;
        }
    }
}