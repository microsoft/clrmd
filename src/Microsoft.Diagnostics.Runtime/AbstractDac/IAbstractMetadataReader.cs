// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    public interface IAbstractMetadataReader
    {
        // Used to get field names, and occasionally to try to create a ClrType for
        // a field if the dac doesn't give us that.
        bool GetFieldDefInfo(int token, out FieldDefInfo info);

        // Used for type names, ClrType.Attribues, and ClrType.EnumerateInterfaces
        bool GetTypeDefInfo(int typeDef, out TypeDefInfo info);

        // Used to resolve type names for unloaded modules
        string? GetTypeRefName(int typeRef);

        // Used to resolve type names for unloaded modules
        bool GetNestedClassToken(int typeDef, out int parent);

        // Used for ClrMethod.Attributes
        bool GetMethodAttributes(int methodDef, out MethodAttributes attributes);

        // Used to get DebuggableAttribute data, to see if this is a debug module or not
        int GetCustomAttributeData(int token, string name, Span<byte> buffer);

        // Used for ClrMethod.GetILInfo
        uint GetRva(int metadataToken);

        // Used for ClrType.EnumerateGenericParameters
        IEnumerable<GenericParameterInfo> EnumerateGenericParameters(int typeDef);

        // Used for ClrType.EnumerateInterfaces
        IEnumerable<InterfaceInfo> EnumerateInterfaces(int typeDef);

        // Used for ClrEnum
        IEnumerable<FieldDefInfo> EnumerateFields(int typeDef);
    }

    public struct FieldDefInfo
    {
        public int Token { get; set; }
        public string? Name { get; set; }
        public FieldAttributes Attributes { get; set; }
        public nint Signature { get; set; }
        public int SignatureSize { get; set; }
        public int Type { get; set; }
        public nint ValuePointer { get; set; }
    }

    public struct TypeDefInfo
    {
        public int Token { get; set; }
        public string? Name { get; set; }
        public TypeAttributes Attributes { get; set; }
        public int Parent { get; set; }
    }

    public struct GenericParameterInfo
    {
        public int Token { get; set; }
        public int Index { get; set; }
        public GenericParameterAttributes Attributes { get; set; }
        public string? Name { get; set; }
    }

    public struct InterfaceInfo
    {
        public int Token { get; set; }
        public int InterfaceToken { get; set; }
    }
}