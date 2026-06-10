// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;
using FieldInfo = Microsoft.Diagnostics.Runtime.AbstractDac.FieldInfo;
using MethodInfo = Microsoft.Diagnostics.Runtime.AbstractDac.MethodInfo;
using TypeInfo = Microsoft.Diagnostics.Runtime.AbstractDac.TypeInfo;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal sealed class DacTypeHelpers : IAbstractTypeHelpers
    {
        private readonly string UnloadedTypeName = "<Unloaded Type>";

        private readonly ClrDataProcess _clrDataProcess;
        private readonly SOSDac _sos;
        private readonly SOSDac6? _sos6;
        private readonly SOSDac8? _sos8;
        private readonly SosDac14? _sos14;
        private readonly IDataReader _dataReader;
        private readonly DacModuleHelpers _moduleHelpers;
        private readonly TargetProperties _target;
        private readonly ClrFlavor _flavor;
        private readonly int _clrVersionMajor;

        // Cache of per-module candidate MethodTables (types whose metadata declares a static
        // FieldDef).  The type-def map and metadata filter are thread-independent, so this is
        // computed once per module and reused for every static and thread-static base lookup.
        // ConcurrentDictionary because EnumerateAdditionalRoots may run on several threads at once:
        // recomputing a module's (deterministic, immutable) candidate array is harmless, but reads
        // must never tear.
        private readonly ConcurrentDictionary<ulong, ulong[]> _staticCandidates = new();

        public DacTypeHelpers(ClrDataProcess clrDataProcess, SOSDac sos, SOSDac6? sos6, SOSDac8? sos8, SosDac14? sos14, IDataReader dataReader, DacModuleHelpers moduleHelpers, TargetProperties target, ClrFlavor flavor, int clrVersionMajor)
        {
            _clrDataProcess = clrDataProcess;
            _sos = sos;
            _sos6 = sos6;
            _sos8 = sos8;
            _sos14 = sos14;
            _dataReader = dataReader;
            _moduleHelpers = moduleHelpers;
            _target = target;
            _flavor = flavor;
            _clrVersionMajor = clrVersionMajor;
        }

        public bool GetTypeInfo(ulong methodTable, out TypeInfo info)
        {
            lock (_sos.SyncRoot)
            {
                if (!_sos.GetMethodTableData(ClrDataAddress.FromTargetAddress(methodTable, _target), out MethodTableData data))
                {
                    info = default;
                    return false;
                }

                info = new()
                {
                    MetadataToken = unchecked((int)data.Token),
                    StaticSize = unchecked((int)data.BaseSize),
                    ComponentSize = unchecked((int)data.ComponentSize),
                    ContainsPointers = data.ContainsPointers != 0,
                    IsShared = data.Shared != 0,
                    MethodCount = data.NumMethods,
                    MethodTable = methodTable,
                    ParentMethodTable = data.ParentMethodTable.ToAddress(_target),
                    ModuleAddress = data.Module.ToAddress(_target),
                    HasFinalizer = ReadHasFinalizer(methodTable),
                };
                return true;
            }
        }

        // MethodTable layout (verified on modern .NET and Desktop Framework, all
        // architectures — x86, x64, ARM, ARM64):
        //   - m_dwFlags is a DWORD at offset 0
        //   - enum_flag_HasFinalizer = 0x00100000 lives in the high half of m_dwFlags
        //   - High flags are read directly with no string/array special-casing
        //     (only the low WORD is overloaded for component size)
        // SOS's MethodTableData does not surface this flag, so we read it from target memory.
        private const uint enum_flag_HasFinalizer = 0x00100000;

        private bool ReadHasFinalizer(ulong methodTable)
        {
            if (methodTable == 0)
                return false;

            if (!_dataReader.Read(methodTable, out uint flags))
                return false;

            return (flags & enum_flag_HasFinalizer) != 0;
        }

        public string? GetTypeName(ulong module, ulong methodTable, int token)
        {
            string? name;
            lock (_sos.SyncRoot)
            {
                name = _sos.GetMethodTableName(ClrDataAddress.FromTargetAddress(methodTable, _target));
            }
            if (string.IsNullOrWhiteSpace(name) || name == UnloadedTypeName)
                return GetTypeByToken(module, token);

            if (name == UnloadedTypeName)
                return GetTypeByToken(module, token);

            name = DacNameParser.Parse(name);
            return name;
        }

        public string? GetTypeByToken(ulong module, int token)
        {
            if (module == 0)
                return null;

            IAbstractMetadataReader? import = _moduleHelpers.GetMetadataReader(module);
            if (import is null)
                return null;

            string? name = GetNameFromToken(import, token);
            if (string.IsNullOrWhiteSpace(name))
                return null;

            name = DacNameParser.Parse(name);
            return name;
        }

        private static string? GetNameFromToken(IAbstractMetadataReader import, int token)
        {
            string? name = null;
            if (import.GetTypeDefInfo(token, out TypeDefInfo info))
                name = info.Name;

            if (name is not null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (import.GetNestedClassToken(token, out int enclosingToken) && enclosingToken != 0 && enclosingToken != token)
                    {
                        string? inner = GetNameFromToken(import, enclosingToken) ?? "<UNKNOWN>";
                        name += $"+{inner}";

                        token = enclosingToken;
                        continue;
                    }

                    break;
                }

                return name;
            }

            return null;
        }

        public ulong GetLoaderAllocatorHandle(ulong mt)
        {
            lock (_sos.SyncRoot)
            {
                if (_sos6 != null && _sos6.GetMethodTableCollectibleData(ClrDataAddress.FromTargetAddress(mt, _target), out MethodTableCollectibleData data) && data.Collectible != 0)
                    return data.LoaderAllocatorObjectHandle.ToAddress(_target);
            }

            return 0;
        }

        public ulong GetAssemblyLoadContextAddress(ulong mt)
        {
            lock (_sos.SyncRoot)
            {
                if (_sos8 != null && _sos8.GetAssemblyLoadContext(ClrDataAddress.FromTargetAddress(mt, _target), out ClrDataAddress assemblyLoadContext))
                    return assemblyLoadContext.ToAddress(_target);
            }

            return 0;
        }

        public bool GetObjectArrayInformation(ulong objRef, out ObjectArrayInformation data)
        {
            lock (_sos.SyncRoot)
            {
                if (!_sos.GetObjectData(ClrDataAddress.FromTargetAddress(objRef, _target), out ObjectData objData))
                {
                    data = default;
                    return false;
                }

                ulong dataPointer = objData.ArrayDataPointer.ToAddress(_target) > objRef ? objData.ArrayDataPointer.ToAddress(_target) - objRef : 0;
                data = new()
                {
                    ComponentType = objData.ElementTypeHandle.ToAddress(_target),
                    ComponentElementType = (ClrElementType)objData.ElementType,
                    DataPointer = dataPointer > int.MaxValue ? int.MaxValue : (int)dataPointer
                };
                return true;
            }
        }

        public IEnumerable<MethodInfo> EnumerateMethodsForType(ulong methodTable)
        {
            // Materialize entirely under the DAC lock: every step touches _sos.
            List<MethodInfo>? results = null;
            lock (_sos.SyncRoot)
            {
                if (!_sos.GetMethodTableData(ClrDataAddress.FromTargetAddress(methodTable, _target), out MethodTableData data))
                    return Array.Empty<MethodInfo>();

                for (uint i = 0; i < data.NumMethods; i++)
                {
                    ClrDataAddress slot = _sos.GetMethodTableSlot(ClrDataAddress.FromTargetAddress(methodTable, _target), i);
                    if (_sos.GetCodeHeaderData(slot, out CodeHeaderData chd))
                    {
                        if (_sos.GetMethodDescData(chd.MethodDesc, ClrDataAddress.Null, out MethodDescData mdd))
                        {
                            ulong md = chd.MethodDesc.ToAddress(_target);
                            uint compilation = chd.JITType;

                            HotColdRegions regions;
                            if (mdd.HasNativeCode != 0 && _sos.GetCodeHeaderData(mdd.NativeCodeAddr, out CodeHeaderData chdBasedOnNative))
                            {
                                regions = new(mdd.NativeCodeAddr.ToAddress(_target), chdBasedOnNative.HotRegionSize, chdBasedOnNative.ColdRegionStart.ToAddress(_target), chdBasedOnNative.ColdRegionSize);
                                md = chdBasedOnNative.MethodDesc.ToAddress(_target);
                                compilation = chdBasedOnNative.JITType;
                            }
                            else
                            {
                                regions = new(mdd.NativeCodeAddr.ToAddress(_target), chd.HotRegionSize, chd.ColdRegionStart.ToAddress(_target), chd.ColdRegionSize);
                            }

                            results ??= new List<MethodInfo>();
                            results.Add(new()
                            {
                                MethodDesc = md,
                                Token = (int)mdd.MDToken,
                                CompilationType = (MethodCompilationType)compilation,
                                HotCold = regions,
                            });
                        }
                    }
                }
            }

            return (IEnumerable<MethodInfo>?)results ?? Array.Empty<MethodInfo>();
        }

        public IEnumerable<FieldInfo> EnumerateFields(TypeInfo type, int baseFieldCount)
        {
            // Materialize entirely under the DAC lock.
            List<FieldInfo>? results = null;
            lock (_sos.SyncRoot)
            {
                if (!_sos.GetFieldInfo(ClrDataAddress.FromTargetAddress(type.MethodTable, _target), out MethodTableFieldInfo fieldInfo) || fieldInfo.FirstFieldAddress.IsNull)
                    return Array.Empty<FieldInfo>();

                ulong nextField = fieldInfo.FirstFieldAddress.ToAddress(_target);
                for (int i = baseFieldCount; i < fieldInfo.NumInstanceFields + fieldInfo.NumStaticFields; i++)
                {
                    if (!_sos.GetFieldData(ClrDataAddress.FromTargetAddress(nextField, _target), out FieldData dacFieldData))
                        break;

                    if (dacFieldData.IsContextLocal == 0)
                    {
                        FieldKind kind;
                        if (dacFieldData.IsThreadLocal != 0)
                            kind = FieldKind.ThreadStatic;
                        else if (dacFieldData.IsStatic != 0)
                            kind = FieldKind.Static;
                        else if (dacFieldData.IsContextLocal != 0)
                            kind = FieldKind.Unsupported;
                        else
                            kind = FieldKind.Instance;

                        results ??= new List<FieldInfo>();
                        results.Add(new()
                        {
                            FieldDesc = nextField,
                            MethodTable = dacFieldData.TypeMethodTable.ToAddress(_target),
                            ElementType = (ClrElementType)dacFieldData.ElementType,
                            Offset = dacFieldData.Offset <= int.MaxValue ? (int)dacFieldData.Offset : int.MaxValue,
                            Token = dacFieldData.FieldToken <= int.MaxValue ? (int)dacFieldData.FieldToken : int.MaxValue,
                            Kind = kind,
                        });
                    }

                    nextField = dacFieldData.NextField.ToAddress(_target);
                }
            }

            return (IEnumerable<FieldInfo>?)results ?? Array.Empty<FieldInfo>();
        }

        public ulong GetStaticFieldAddress(in AppDomainInfo appDomain, in ClrModuleInfo module, in TypeInfo type, in FieldInfo field)
        {
            lock (_sos.SyncRoot)
            {
                if (_sos14 is not null)
                {
                    // The static base for a type is allocated up front, but the .cctor may not
                    // have run yet (e.g. for beforefieldinit types whose statics have not been
                    // touched). Returning a non-zero slot address in that case would let callers
                    // read garbage/zero and believe it was a real field value. Match the legacy
                    // DomainLocalModuleData path, which returns 0 when the class is not initialized.
                    if (_sos14.GetMethodTableInitializationFlags(ClrDataAddress.FromTargetAddress(type.MethodTable, _target)) != MethodTableInitializationFlags.MethodTableInitialized)
                        return 0;

                    (ClrDataAddress nonGcBase, ClrDataAddress gcBase) = _sos14.GetStaticBaseAddress(ClrDataAddress.FromTargetAddress(type.MethodTable, _target));
                    if (field.ElementType.IsPrimitive())
                        return !nonGcBase.IsNull ? nonGcBase.ToAddress(_target) + (uint)field.Offset : 0;

                    return !gcBase.IsNull ? gcBase.ToAddress(_target) + (uint)field.Offset : 0;
                }

                if (appDomain.Address == 0)
                    return 0;

                if (type.IsShared)
                {
                    if (!_sos.GetDomainLocalModuleDataFromAppDomain(ClrDataAddress.FromTargetAddress(appDomain.Address, _target), (int)module.Id, out DomainLocalModuleData dlmd))
                        return 0;

                    if (field.ElementType.IsPrimitive())
                        return dlmd.NonGCStaticDataStart.ToAddress(_target) + (uint)field.Offset;
                    else
                        return dlmd.GCStaticDataStart.ToAddress(_target) + (uint)field.Offset;
                }
                else
                {
                    if (!_sos.GetDomainLocalModuleDataFromModule(ClrDataAddress.FromTargetAddress(module.Address, _target), out DomainLocalModuleData dlmd))
                        return 0;

                    if (!IsInitialized(dlmd, type.MetadataToken))
                        return 0;

                    if (field.ElementType.IsPrimitive())
                        return dlmd.NonGCStaticDataStart.ToAddress(_target) + (uint)field.Offset;
                    else
                        return dlmd.GCStaticDataStart.ToAddress(_target) + (uint)field.Offset;
                }
            }
        }

        public ulong GetThreadStaticFieldAddress(ulong threadAddress, in ClrModuleInfo module, in TypeInfo type, in FieldInfo field)
        {
            if (threadAddress == 0)
                return 0;

            lock (_sos.SyncRoot)
            {
                if (_sos14 is not null)
                {
                    // See GetStaticFieldAddress: the per-type static bases are allocated even
                    // when the .cctor hasn't run. Match the legacy ThreadLocalModuleData path,
                    // which only returns an address once the class is initialized.
                    if (_sos14.GetMethodTableInitializationFlags(ClrDataAddress.FromTargetAddress(type.MethodTable, _target)) != MethodTableInitializationFlags.MethodTableInitialized)
                        return 0;

                    (ClrDataAddress nonGcBase, ClrDataAddress gcBase) = _sos14.GetThreadStaticBaseAddress(ClrDataAddress.FromTargetAddress(type.MethodTable, _target), ClrDataAddress.FromTargetAddress(threadAddress, _target));
                    if (field.ElementType.IsPrimitive())
                        return !nonGcBase.IsNull ? nonGcBase.ToAddress(_target) + (uint)field.Offset : 0;

                    return !gcBase.IsNull ? gcBase.ToAddress(_target) + (uint)field.Offset : 0;
                }

                if (!_sos.GetThreadLocalModuleData(ClrDataAddress.FromTargetAddress(threadAddress, _target), (uint)module.Index, out ThreadLocalModuleData threadData))
                    return 0;

                if (field.ElementType.IsPrimitive())
                    return threadData.NonGCStaticDataStart.ToAddress(_target) + (uint)field.Offset;

                return threadData.GCStaticDataStart.ToAddress(_target) + (uint)field.Offset;
            }
        }

        public IEnumerable<AdditionalRootInfo> EnumerateAdditionalRoots(ulong moduleAddress, ulong[] threadAddresses)
        {
            // On .NET 9 and 10 Core, two kinds of GC root are not surfaced by the normal handle/
            // finalizer/stack walk and must be enumerated here:
            //  * Regular statics: a type's GC statics live in slots inside a shared pinned Object[]
            //    (the runtime's PinnedHeapHandleTable bucket) kept alive by a single strong handle.
            //    On Server GC with DATAS the DAC's handle walker can miss that handle (it walks only
            //    GCHeapCount() per-processor handle-table slots, fewer than the actual slot count once
            //    the dynamic heap count is reduced), so the bucket -- and anything reachable only
            //    through a static -- would otherwise have no enumerable root.
            //  * Non-collectible thread statics: stored in the per-thread ThreadLocalData GC array
            //    (scanned directly by the GC), not a GC handle.
            // .NET 11+ enumerates the handle table and statics correctly, so this is scoped to 9/10.
            // _sos14 is required for the per-type GC base lookups and only exists on those runtimes.
            if (_flavor != ClrFlavor.Core || _clrVersionMajor < 9 || _clrVersionMajor > 10 || _sos14 is null)
                yield break;

            HashSet<ulong> seenThreadStatic = new();
            foreach (ulong methodTable in GetStaticCandidateTypes(moduleAddress))
            {
                // Regular static base for this type (interior to its pinned Object[] bucket).
                ulong gcStaticBase;
                try
                {
                    gcStaticBase = GetGCStaticBase(methodTable);
                }
                catch
                {
                    // A single corrupt MethodTable must not abort enumeration of all roots
                    // (EnumerateRoots is one lazy iterator); skip it like HasStaticFieldDef does.
                    gcStaticBase = 0;
                }

                if (gcStaticBase != 0)
                    yield return new AdditionalRootInfo { Address = gcStaticBase, IsThreadStatic = false };

                // Thread-static base for this type on each live thread.
                foreach (ulong threadAddress in threadAddresses)
                {
                    if (threadAddress == 0)
                        continue;

                    ulong gcThreadBase;
                    try
                    {
                        gcThreadBase = GetGCThreadStaticBase(methodTable, threadAddress);
                    }
                    catch
                    {
                        continue;
                    }

                    if (gcThreadBase != 0 && seenThreadStatic.Add(gcThreadBase))
                        yield return new AdditionalRootInfo { Address = gcThreadBase, IsThreadStatic = true };
                }
            }
        }

        private ulong[] GetStaticCandidateTypes(ulong moduleAddress)
        {
            if (_staticCandidates.TryGetValue(moduleAddress, out ulong[]? cached))
                return cached;

            // Cheap pre-filter: keep only types whose metadata declares a static FieldDef, mirroring
            // ClrModule.EnumerateTypesWithStaticFields. Null/unreadable metadata => no filter (slower
            // but correct). GetMetadataReader returns null when metadata is unavailable.
            IAbstractMetadataReader? metadata = _moduleHelpers.GetMetadataReader(moduleAddress);

            List<ulong> candidates = new();
            foreach ((ulong methodTable, int token) in _moduleHelpers.EnumerateTypeDefMap(moduleAddress))
            {
                if (methodTable == 0)
                    continue;

                if (metadata is not null && !HasStaticFieldDef(metadata, token))
                    continue;

                candidates.Add(methodTable);
            }

            // Concurrent callers may build this simultaneously; every result is identical and
            // immutable, so publishing any winner (last write wins) is correct.
            ulong[] result = candidates.ToArray();
            _staticCandidates[moduleAddress] = result;
            return result;
        }

        private ulong GetGCStaticBase(ulong methodTable)
        {
            // The per-type GC static base is allocated up front but only points at real storage
            // once the type is initialized; an uninitialized type or a type with no GC statics
            // yields a null gcBase (=> 0, filtered by the caller).  The returned pointer is
            // interior to the shared pinned Object[] that backs the module's GC statics.
            if (_sos14!.GetMethodTableInitializationFlags(ClrDataAddress.FromTargetAddress(methodTable, _target)) != MethodTableInitializationFlags.MethodTableInitialized)
                return 0;

            (_, ClrDataAddress gcBase) = _sos14.GetStaticBaseAddress(ClrDataAddress.FromTargetAddress(methodTable, _target));
            return gcBase.IsNull ? 0 : gcBase.ToAddress(_target);
        }

        private ulong GetGCThreadStaticBase(ulong methodTable, ulong threadAddress)
        {
            // The per-type GC thread-static base is allocated up front but only once the type is
            // initialized; an uninitialized type, a type with no GC thread statics, or storage not
            // yet allocated on this thread all yield a null gcBase (=> 0, filtered by the caller).
            if (_sos14!.GetMethodTableInitializationFlags(ClrDataAddress.FromTargetAddress(methodTable, _target)) != MethodTableInitializationFlags.MethodTableInitialized)
                return 0;

            (_, ClrDataAddress gcBase) = _sos14.GetThreadStaticBaseAddress(ClrDataAddress.FromTargetAddress(methodTable, _target), ClrDataAddress.FromTargetAddress(threadAddress, _target));
            return gcBase.IsNull ? 0 : gcBase.ToAddress(_target);
        }

        private static bool HasStaticFieldDef(IAbstractMetadataReader metadata, int typeToken)
        {
            try
            {
                foreach (FieldDefInfo info in metadata.EnumerateFields(typeToken))
                {
                    // Literal (const) fields have no runtime storage; require Static set, Literal clear.
                    const System.Reflection.FieldAttributes StaticNonStorage =
                        System.Reflection.FieldAttributes.Static | System.Reflection.FieldAttributes.Literal;
                    if ((info.Attributes & StaticNonStorage) == System.Reflection.FieldAttributes.Static)
                        return true;
                }
            }
            catch
            {
                return true; // Unreadable metadata: fall back so the type is still considered.
            }

            return false;
        }

        private bool IsInitialized(in DomainLocalModuleData data, int token)
        {
            ulong flagsAddr = data.ClassData.ToAddress(_target) + (uint)(token & ~0x02000000u) - 1;
            if (!_dataReader.Read(flagsAddr, out byte flags))
                return false;

            return (flags & 1) != 0;
        }

        // Method helpers

        public string? GetMethodSignature(ulong methodDesc)
        {
            lock (_sos.SyncRoot)
                return _sos.GetMethodDescName(ClrDataAddress.FromTargetAddress(methodDesc, _target));
        }

        public ulong GetILForModule(ulong address, uint rva)
        {
            lock (_sos.SyncRoot)
                return _sos.GetILForModule(ClrDataAddress.FromTargetAddress(address, _target), rva).ToAddress(_target);
        }

        public ImmutableArray<ILToNativeMap> GetILMap(ulong ip, in HotColdRegions hotCold)
        {
            ImmutableArray<ILToNativeMap>.Builder result = ImmutableArray.CreateBuilder<ILToNativeMap>();

            lock (_sos.SyncRoot)
            {
                foreach (ClrDataMethod method in _clrDataProcess.EnumerateMethodInstancesByAddress(ClrDataAddress.FromTargetAddress(ip, _target)))
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
