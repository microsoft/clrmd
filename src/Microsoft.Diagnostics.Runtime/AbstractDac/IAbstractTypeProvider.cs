// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IAbstractTypeProvider
    {
        bool GetTypeInfo(ulong methodTable, out TypeInfo info);
        string? GetTypeName(ulong methodTable);
        string? GetTypeName(MetadataImport metadata, int token);
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
        bool GetFieldMetadataInfo(MetadataImport import, int token, out FieldMetadataInfo info);
        ulong GetStaticFieldAddress(in AppDomainInfo appDomain, in ClrModuleInfo module, in TypeInfo typeInfo, in FieldInfo field);
    }

    internal struct TypeInfo
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
    }

    internal struct ObjectArrayInformation
    {
        public ulong ComponentType { get; set; }

        public ClrElementType ComponentElementType { get; set; }

        /// <summary>
        /// The location of the first element in the array.
        /// </summary>
        public int DataPointer { get; set; }
    }

    internal struct MethodInfo
    {
        public ulong MethodDesc { get; set; }
        public int Token { get; set; }
        public MethodCompilationType CompilationType { get; set; }
        public HotColdRegions HotCold { get; set; }
    }

    internal struct FieldInfo
    {
        public ulong FieldDesc { get; set; }
        public int Token { get; set; }
        public ulong MethodTable { get; set; }
        public ClrElementType ElementType { get; set; }
        public int Offset { get; set; }
        public FieldKind Kind { get; set; }
    }

    internal enum FieldKind
    {
        Unsupported,
        Instance,
        Static,
        ThreadStatic
    }

    internal struct FieldMetadataInfo
    {
        public string? Name { get; set; }
        public FieldAttributes Attributes { get; set; }
        public nint Signature { get; set; }
        public int SignatureSize { get; set; }
    }
}