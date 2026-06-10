// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// Provides information about types, fields, and methods.
    ///
    /// This interface is required, but some methods can return default values.
    ///
    /// This interface is not "stable" and may change even in minor or patch
    /// versions of ClrMD.
    /// </summary>
    public interface IAbstractTypeHelpers
    {
        bool GetTypeInfo(ulong methodTable, out TypeInfo info);
        string? GetTypeName(ulong module, ulong methodTable, int token);
        ulong GetLoaderAllocatorHandle(ulong mt);
        ulong GetAssemblyLoadContextAddress(ulong mt);

        bool GetObjectArrayInformation(ulong objRef, out ObjectArrayInformation data);
        IEnumerable<MethodInfo> EnumerateMethodsForType(ulong methodTable);
        IEnumerable<FieldInfo> EnumerateFields(TypeInfo type, int baseFieldCount);

        // Method helpers
        string? GetMethodSignature(ulong methodDesc);
        ImmutableArray<ILToNativeMap> GetILMap(ulong ip, in HotColdRegions hotCold);
        ulong GetILForModule(ulong address, uint rva);

        // Field helpers
        ulong GetStaticFieldAddress(in AppDomainInfo appDomain, in ClrModuleInfo module, in TypeInfo typeInfo, in FieldInfo field);
        ulong GetThreadStaticFieldAddress(ulong threadAddress, in ClrModuleInfo module, in TypeInfo typeInfo, in FieldInfo field);

        // Enumerates "additional" GC roots that the normal handle/finalizer/stack walk does not
        // surface: on .NET 9 and 10 Core this is the GC bases of regular statics (interior to the
        // module's shared pinned Object[] storage) and non-collectible thread statics (the per-
        // thread GC storage object) for every constructed type defined by the module.  threadAddresses
        // are the live threads to probe for thread statics.  Returns an empty enumeration on non-Core
        // runtimes and on CLR versions outside [9, 10] (pre-9 had no such split; .NET 11+ enumerates
        // the handle table and statics correctly, so no special handling is needed there).
        IEnumerable<AdditionalRootInfo> EnumerateAdditionalRoots(ulong moduleAddress, ulong[] threadAddresses);
    }

    /// <summary>
    /// A GC root base discovered by <see cref="IAbstractTypeHelpers.EnumerateAdditionalRoots"/>.
    /// </summary>
    public struct AdditionalRootInfo
    {
        /// <summary>
        /// The GC base address of the static storage.  For a regular static this is an interior
        /// pointer into the pinned Object[] that backs the module's GC statics; for a thread static
        /// it is the per-thread GC thread-static storage object itself.
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// True if this is a thread-static base; false if it is a regular static base.
        /// </summary>
        public bool IsThreadStatic { get; set; }
    }

    public struct TypeInfo
    {
        public ulong MethodTable { get; set; }
        public ulong ModuleAddress { get; set; }
        public ulong ParentMethodTable { get; set; }
        public int MetadataToken { get; set; }
        public int StaticSize { get; set; }
        public int ComponentSize { get; set; }
        public bool IsShared { get; set; }
        public int MethodCount { get; set; }
        public bool ContainsPointers { get; set; }
        public bool HasFinalizer { get; set; }
    }

    public struct ObjectArrayInformation
    {
        public ulong ComponentType { get; set; }

        public ClrElementType ComponentElementType { get; set; }

        /// <summary>
        /// The location of the first element in the array.
        /// </summary>
        public int DataPointer { get; set; }
    }

    public struct MethodInfo
    {
        public ulong MethodDesc { get; set; }
        public int Token { get; set; }
        public MethodCompilationType CompilationType { get; set; }
        public HotColdRegions HotCold { get; set; }
    }

    public struct FieldInfo
    {
        public ulong FieldDesc { get; set; }
        public int Token { get; set; }
        public ulong MethodTable { get; set; }
        public ClrElementType ElementType { get; set; }
        public int Offset { get; set; }
        public FieldKind Kind { get; set; }
    }

    public enum FieldKind
    {
        Unsupported,
        Instance,
        Static,
        ThreadStatic
    }
}