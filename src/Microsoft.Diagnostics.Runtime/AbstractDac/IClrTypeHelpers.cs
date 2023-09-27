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

        string? ReadString(ulong addr, int maxLength);
        ComCallableWrapper? CreateCCWForObject(ulong obj);
        RuntimeCallableWrapper? CreateRCWForObject(ulong obj);
        ImmutableArray<ComInterfaceData> GetRCWInterfaces(ulong address, int interfaceCount);

        bool GetObjectArrayInformation(ulong objRef, out ObjectArrayInformation data);
        ImmutableArray<ClrMethod> GetMethodsForType(ClrType type);
        IEnumerable<ClrField> EnumerateFields(ClrType type);
    }

    internal struct ObjectArrayInformation
    {
        public ClrElementType ComponentType { get; set; }

        /// <summary>
        /// The location of the first element in the array.
        /// </summary>
        public int DataPointer { get; set; }
    }
}