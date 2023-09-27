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

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class ClrTypeHelpers : IClrTypeHelpers, IClrFieldHelpers
    {
        private readonly string UnloadedTypeName = "<Unloaded Type>";

        private readonly SOSDac _sos;
        private readonly SOSDac6? _sos6;
        private readonly SOSDac8? _sos8;
        private readonly IClrTypeFactory _typeFactory;
        private readonly IClrMethodHelpers _methodHelpers;

        public ClrHeap Heap { get; }


        public IDataReader DataReader { get; }

        public ClrTypeHelpers(ClrDataProcess clrDataProcess, SOSDac sos, SOSDac6? sos6, SOSDac8? sos8, IClrTypeFactory typeFactory, ClrHeap heap)
        {
            _sos = sos;
            _sos6 = sos6;
            _sos8 = sos8;
            _typeFactory = typeFactory;
            Heap = heap;
            DataReader = heap.Runtime.DataTarget.DataReader;
            _methodHelpers = new ClrMethodHelpers(clrDataProcess, sos, DataReader);
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

        public ImmutableArray<ClrMethod> GetMethodsForType(ClrType type)
        {
            ulong mt = type.MethodTable;
            if (!_sos.GetMethodTableData(mt, out MethodTableData data) || data.NumMethods == 0)
                return ImmutableArray<ClrMethod>.Empty;

            ImmutableArray<ClrMethod>.Builder builder = ImmutableArray.CreateBuilder<ClrMethod>(data.NumMethods);
            for (uint i = 0; i < data.NumMethods; i++)
            {
                ulong slot = _sos.GetMethodTableSlot(mt, i);
                if (_sos.GetCodeHeaderData(slot, out CodeHeaderData chd) && _sos.GetMethodDescData(chd.MethodDesc, 0, out MethodDescData mdd))
                {
                    HotColdRegions regions = new(mdd.NativeCodeAddr, chd.HotRegionSize, chd.ColdRegionStart, chd.ColdRegionSize);
                    builder.Add(new(_methodHelpers, type, chd.MethodDesc, (int)mdd.MDToken, (MethodCompilationType)chd.JITType, regions));
                }
            }

            return builder.MoveOrCopyToImmutable();
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
            if (!_sos.GetFieldInfo(type.MethodTable, out DacInterface.FieldInfo fieldInfo) || fieldInfo.FirstFieldAddress == 0)
                yield break;

            ulong nextField = fieldInfo.FirstFieldAddress;
            for (int i = baseFieldCount; i < fieldInfo.NumInstanceFields + fieldInfo.NumStaticFields; i++)
            {
                if (!_sos.GetFieldData(nextField, out FieldData fieldData))
                    break;

                if (fieldData.IsContextLocal == 0 && fieldData.IsThreadLocal == 0)
                {
                    ClrType? fieldType = _typeFactory.GetOrCreateType(fieldData.TypeMethodTable, 0);
                    if (fieldData.IsStatic != 0)
                        yield return new ClrStaticField(type, fieldType, this, fieldData);
                    else
                        yield return new ClrInstanceField(type, fieldType, this, fieldData);
                }

                nextField = fieldData.NextField;
            }
        }


        public bool ReadProperties(ClrType parentType, int fieldToken, out string? name, out FieldAttributes attributes, ref ClrType? type)
        {
            MetadataImport? import = parentType.Module.MetadataImport;
            if (import is null || !import.GetFieldProps(fieldToken, out name, out attributes, out IntPtr fieldSig, out int sigLen, out _, out _))
            {
                name = null;
                attributes = default;
                return false;
            }

            if (type is null)
            {
                SigParser sigParser = new(fieldSig, sigLen);
                if (sigParser.GetCallingConvInfo(out int sigType) && sigType == SigParser.IMAGE_CEE_CS_CALLCONV_FIELD)
                {
                    sigParser.SkipCustomModifiers();
                    type = _typeFactory.GetOrCreateTypeFromSignature(parentType.Module, sigParser, parentType.EnumerateGenericParameters(), Array.Empty<ClrGenericParameter>());
                }
            }

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
    }
}