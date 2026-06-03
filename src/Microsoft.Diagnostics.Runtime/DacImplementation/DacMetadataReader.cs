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
        private readonly object _syncRoot;
        private bool _disposed;

        public DacMetadataReader(MetadataImport import, object syncRoot)
        {
            _import = import;
            _syncRoot = syncRoot;
        }

        public void Dispose()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            _disposed = true;
            _import.Dispose();
        }

        public bool GetMethodAttributes(int methodDef, out MethodAttributes attributes)
        {
            lock (_syncRoot)
                return _import.GetMethodAttributes(methodDef, out attributes);
        }

        public bool GetFieldDefInfo(int token, out FieldDefInfo info)
        {
            lock (_syncRoot)
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
        }

        public bool GetTypeDefInfo(int typeDef, out TypeDefInfo info)
        {
            lock (_syncRoot)
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
        }

        public string? GetTypeRefName(int typeRef)
        {
            lock (_syncRoot)
                return _import.GetTypeRefName(typeRef);
        }

        public bool GetNestedClassToken(int typeDef, out int parent)
        {
            lock (_syncRoot)
                return _import.GetNestedClassProperties(typeDef, out parent);
        }

        public IEnumerable<FieldDefInfo> EnumerateFields(int typeDef)
        {
            List<FieldDefInfo>? results = null;
            lock (_syncRoot)
            {
                foreach (int token in _import.EnumerateFields(typeDef))
                {
                    if (!_import.GetFieldProps(token, out string? name, out FieldAttributes attributes, out nint fieldSig, out int sigLen, out int type, out nint pValue))
                        continue;

                    results ??= new List<FieldDefInfo>();
                    results.Add(new FieldDefInfo
                    {
                        Token = token,
                        Name = name,
                        Attributes = attributes,
                        Signature = fieldSig,
                        SignatureSize = sigLen,
                        Type = type,
                        ValuePointer = pValue
                    });
                }
            }

            return (IEnumerable<FieldDefInfo>?)results ?? Array.Empty<FieldDefInfo>();
        }

        public IEnumerable<GenericParameterInfo> EnumerateGenericParameters(int typeDef)
        {
            List<GenericParameterInfo>? results = null;
            lock (_syncRoot)
            {
                foreach (int token in _import.EnumerateGenericParams(typeDef))
                {
                    if (_import.GetGenericParamProps(token, out int index, out GenericParameterAttributes attributes, out string? name))
                    {
                        results ??= new List<GenericParameterInfo>();
                        results.Add(new GenericParameterInfo
                        {
                            Token = token,
                            Index = index,
                            Attributes = attributes,
                            Name = name
                        });
                    }
                }
            }

            return (IEnumerable<GenericParameterInfo>?)results ?? Array.Empty<GenericParameterInfo>();
        }

        public IEnumerable<InterfaceInfo> EnumerateInterfaces(int typeDef)
        {
            List<InterfaceInfo>? results = null;
            lock (_syncRoot)
            {
                foreach (int token in _import.EnumerateInterfaceImpls(typeDef))
                {
                    if (_import.GetInterfaceImplProps(token, out _, out int mdIFace))
                    {
                        results ??= new List<InterfaceInfo>();
                        results.Add(new InterfaceInfo
                        {
                            Token = token,
                            InterfaceToken = mdIFace
                        });
                    }
                }
            }

            return (IEnumerable<InterfaceInfo>?)results ?? Array.Empty<InterfaceInfo>();
        }


        public unsafe int GetCustomAttributeData(int token, string name, Span<byte> buffer)
        {
            lock (_syncRoot)
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
        }

        public uint GetRva(int metadataToken)
        {
            lock (_syncRoot)
                return _import.GetRva(metadataToken);
        }
    }
}