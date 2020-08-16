// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class ClrElementTypeExtensions
    {
        public static bool IsPrimitive(this ClrElementType cet)
        {
            return cet >= ClrElementType.Boolean && cet <= ClrElementType.Double
                || cet == ClrElementType.NativeInt || cet == ClrElementType.NativeUInt;
        }

        public static bool IsValueType(this ClrElementType cet)
        {
            return IsPrimitive(cet) || cet == ClrElementType.Struct;
        }

        public static bool IsObjectReference(this ClrElementType cet)
        {
            return cet == ClrElementType.String || cet == ClrElementType.Class
                || cet == ClrElementType.Array || cet == ClrElementType.SZArray
                || cet == ClrElementType.Object;
        }

        public static Type? GetTypeForElementType(this ClrElementType type) => type switch
        {
            ClrElementType.Boolean => typeof(bool),
            ClrElementType.Char => typeof(char),
            ClrElementType.Double => typeof(double),
            ClrElementType.Float => typeof(float),
            ClrElementType.Pointer or
            ClrElementType.NativeInt or
            ClrElementType.FunctionPointer => typeof(IntPtr),
            ClrElementType.NativeUInt => typeof(UIntPtr),
            ClrElementType.Int16 => typeof(short),
            ClrElementType.Int32 => typeof(int),
            ClrElementType.Int64 => typeof(long),
            ClrElementType.Int8 => typeof(sbyte),
            ClrElementType.UInt16 => typeof(ushort),
            ClrElementType.UInt32 => typeof(uint),
            ClrElementType.UInt64 => typeof(ulong),
            ClrElementType.UInt8 => typeof(byte),
            _ => null,
        };
    }
}