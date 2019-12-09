// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class ClrEnum
    {
        public ClrType Type { get; }
        public ClrElementType ElementType { get; }

        private readonly (string, object?)[] _values;

        public ClrEnum(ClrType type)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));

            if (!type.IsEnum)
                throw new InvalidOperationException($"{type.Name ?? nameof(ClrType)} is not an enum.  You must call {nameof(ClrType)}.{nameof(ClrType.IsEnum)} before using {nameof(ClrEnum)}.");

            MetaDataImport? import = type.Module?.MetadataImport;
            if (import != null)
            {
                _values = EnumerateValues(import, out ClrElementType elementType).ToArray();
                ElementType = elementType;
            }
            else
            {
                _values = Array.Empty<(string, object?)>();
            }
        }

        public T GetEnumValue<T>(string name) where T : unmanaged
        {
            object? value = _values.Single(v => v.Item1 == name).Item2;
            if (value == null)
                throw new NullReferenceException($"Enum {Type.Name} had null '{name}' value.");

            return (T)value;
        }

        public IEnumerable<string> GetEnumNames() => _values.Select(v => v.Item1);
        public IEnumerable<(string, object?)> EnumerateValues() => _values;

        private (string, object?)[] EnumerateValues(MetaDataImport import, out ClrElementType elementType)
        {
            List<(string, object?)> values = new List<(string, object?)>();
            elementType = ClrElementType.Unknown;

            foreach (uint token in import.EnumerateFields((int)Type.MetadataToken))
            {
                if (import.GetFieldProps(token, out string? name, out FieldAttributes attr, out IntPtr ppvSigBlob, out int pcbSigBlob, out int pdwCPlusTypeFlag, out IntPtr ppValue))
                {
                    if (name == null)
                        continue;

                    if ((int)attr == 0x606 && name == "value__")
                    {
                        SigParser parser = new SigParser(ppvSigBlob, pcbSigBlob);
                        if (parser.GetCallingConvInfo(out _) && parser.GetElemType(out int elemType))
                            elementType = (ClrElementType)elemType;
                    }

                    // public, static, literal, has default
                    if ((int)attr == 0x8056)
                    {
                        SigParser parser = new SigParser(ppvSigBlob, pcbSigBlob);
                        parser.GetCallingConvInfo(out _);
                        parser.GetElemType(out _);

                        Type? underlying = ((ClrElementType)pdwCPlusTypeFlag).GetTypeForElementType();
                        if (underlying != null)
                        {
                            object o = Marshal.PtrToStructure(ppValue, underlying);
                            values.Add((name, o));
                        }
                        else
                        {
                            values.Add((name, null));
                        }
                    }
                }
            }

            return values.ToArray();
        }
    }
}
