// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrValue : IEquatable<IClrValue>
    {
        ulong Address { get; }
        bool ContainsPointers { get; }
        bool HasComCallableWrapper { get; }
        bool HasRuntimeCallableWrapper { get; }
        bool IsArray { get; }
        bool IsBoxedValue { get; }
        bool IsComClassFactory { get; }
        bool IsDelegate { get; }
        bool IsException { get; }
        bool IsFree { get; }
        bool IsNull { get; }
        bool IsRuntimeType { get; }
        bool IsValid { get; }
        ulong Size { get; }
        SyncBlock? SyncBlock { get; }
        IClrType? Type { get; }

        ClrArray AsArray();
        ClrDelegate AsDelegate();
        IClrException? AsException();
        IClrType? AsRuntimeType();
        string? AsString(int maxLength = 4096);

        IEnumerable<ulong> EnumerateReferenceAddresses(bool carefully = false, bool considerDependantHandles = true);
        IEnumerable<IClrValue> EnumerateReferences(bool carefully = false, bool considerDependantHandles = true);
        IEnumerable<ClrReference> EnumerateReferencesWithFields(bool carefully = false, bool considerDependantHandles = true);
        IComCallableWrapper? GetComCallableWrapper();
        IRuntimeCallableWrapper? GetRuntimeCallableWrapper();
        T ReadBoxedValue<T>() where T : unmanaged;
        T ReadField<T>(string fieldName) where T : unmanaged;
        IClrValue ReadObjectField(string fieldName);
        string? ReadStringField(string fieldName, int maxLength = 4096);
        IClrValue ReadValueTypeField(string fieldName);
        bool TryReadField<T>(string fieldName, out T result) where T : unmanaged;

        bool TryReadObjectField(string fieldName, [NotNullWhen(true)] out IClrValue? result);
        bool TryReadValueTypeField(string fieldName, [NotNullWhen(true)] out IClrValue? result);
    }
}