// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IAbstractMetadataReader
    {
        bool GetFieldDefInfo(int token, out FieldDefInfo info);
        bool GetTypeDefInfo(int typeDef, out TypeDefInfo info);
        string? GetTypeRefName(int typeRef);
        bool GetNestedClassToken(int typeDef, out int parent);

        bool GetMethodAttributes(int methodDef, out MethodAttributes attributes);
        int GetCustomAttributeData(int token, string name, Span<byte> buffer);
        uint GetRva(int metadataToken);

        // Only used for ClrType.EnumerateGenericParameters
        IEnumerable<GenericParameterInfo> EnumerateGenericParameters(int typeDef);

        // Only used for ClrType.EnumerateInterfaces
        IEnumerable<InterfaceInfo> EnumerateInterfaces(int typeDef);

        // Only used for ClrEnum
        IEnumerable<FieldDefInfo> EnumerateFields(int typeDef);
    }

    internal struct FieldDefInfo
    {
        public int Token { get; set; }
        public string? Name { get; set; }
        public FieldAttributes Attributes { get; set; }
        public nint Signature { get; set; }
        public int SignatureSize { get; set; }
        public int Type { get; set; }
        public nint ValuePointer { get; set; }
    }

    internal struct TypeDefInfo
    {
        public int Token { get; set; }
        public string? Name { get; set; }
        public TypeAttributes Attributes { get; set; }
        public int Parent { get; set; }
    }

    internal struct GenericParameterInfo
    {
        public int Token { get; set; }
        public int Index { get; set; }
        public GenericParameterAttributes Attributes { get; set; }
        public string? Name { get; set; }
    }

    internal struct InterfaceInfo
    {
        public int Token { get; set; }
        public int InterfaceToken { get; set; }
    }
}