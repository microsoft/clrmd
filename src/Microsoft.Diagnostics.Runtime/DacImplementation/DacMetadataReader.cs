// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacMetadataReader : IAbstractMetadataReader, IDisposable
    {
        private readonly MetadataImport _import;
        private bool _disposed;

        public DacMetadataReader(MetadataImport import)
        {
            _import = import;
        }

        public void Dispose()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            _disposed = true;
            _import.Dispose();
        }

        public bool GetMethodAttributes(int methodDef, out MethodAttributes attributes) => _import.GetMethodAttributes(methodDef, out attributes);

        public bool GetFieldDefInfo(int token, out FieldDefInfo info)
        {
            if (!_import.GetFieldProps(token, out string? name, out FieldAttributes attributes, out nint fieldSig, out int sigLen, out int type, out nint pValue))
            {
                info = default;
                return false;
            }

            info = new()
            {
                Token = token,
                Name = name,
                Attributes = attributes,
                Signature = fieldSig,
                SignatureSize = sigLen,
                Type = type,
                ValuePointer = pValue
            };

            return true;
        }

        public bool GetTypeDefInfo(int typeDef, out TypeDefInfo info)
        {
            HResult hr = _import.GetTypeDefProperties(typeDef, out string? name, out TypeAttributes attributes, out int parent);
            if (hr.IsOK)
            {
                info = new()
                {
                    Token = typeDef,
                    Name = name,
                    Attributes = attributes,
                    Parent = parent,
                };

                return true;
            }

            info = default;
            return false;
        }

        public string? GetTypeRefName(int typeRef) => _import.GetTypeRefName(typeRef);

        public bool GetNestedClassToken(int typeDef, out int parent) => _import.GetNestedClassProperties(typeDef, out parent);

        public IEnumerable<FieldDefInfo> EnumerateFields(int typeDef)
        {
            foreach (int token in _import.EnumerateFields(typeDef))
            {
                if (GetFieldDefInfo(token, out FieldDefInfo info))
                    yield return info;
            }
        }

        public IEnumerable<GenericParameterInfo> EnumerateGenericParameters(int typeDef)
        {
            foreach (int token in _import.EnumerateGenericParams(typeDef))
            {
                if (_import.GetGenericParamProps(token, out int index, out GenericParameterAttributes attributes, out string? name))
                {
                    yield return new()
                    {
                        Token = token,
                        Index = index,
                        Attributes = attributes,
                        Name = name
                    };
                }
            }
        }

        public IEnumerable<InterfaceInfo> EnumerateInterfaces(int typeDef)
        {
            foreach (int token in _import.EnumerateInterfaceImpls(typeDef))
            {
                if (_import.GetInterfaceImplProps(token, out _, out int mdIFace))
                {
                    yield return new()
                    {
                        Token = token,
                        InterfaceToken = mdIFace
                    };
                }
            }
        }


        public unsafe int GetCustomAttributeData(int token, string name, Span<byte> buffer)
        {
            if (_import.GetCustomAttributeByName(token, name, out nint pData, out uint cbData))
            {
                if (pData != 0 && cbData > 0)
                {
                    int size = cbData > int.MaxValue ? 0 : (int)cbData;
                    int result = Math.Min(size, buffer.Length);

                    ReadOnlySpan<byte> data = new((void*)pData, result);
                    data.CopyTo(buffer);

                    return result;
                }
            }

            return 0;
        }

        public uint GetRva(int metadataToken) => _import.GetRva(metadataToken);
    }
}