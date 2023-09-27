// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IClrTypeHelpers
    {
        string? GetTypeName(ulong methodTable);
        string? GetTypeName(MetadataImport metadata, int token);
        ulong GetLoaderAllocatorHandle(ulong mt);
        ulong GetAssemblyLoadContextAddress(ulong mt);

        bool GetObjectArrayInformation(ulong objRef, out ObjectArrayInformation data);
        IEnumerable<MethodInfo> EnumerateMethodsForType(ulong methodTable);
        IEnumerable<ClrField> EnumerateFields(ClrType type);

        // Method helpers
        string? GetMethodSignature(ulong methodDesc);
        ImmutableArray<ILToNativeMap> GetILMap(ulong ip, in HotColdRegions hotCold);
        ulong GetILForModule(ulong address, uint rva);
    }

    internal struct ObjectArrayInformation
    {
        public ClrElementType ComponentType { get; set; }

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
}