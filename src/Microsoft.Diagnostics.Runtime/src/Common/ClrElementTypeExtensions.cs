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

        public static Type? GetTypeForElementType(this ClrElementType type)
        {
            switch (type)
            {
                case ClrElementType.Boolean:
                    return typeof(bool);

                case ClrElementType.Char:
                    return typeof(char);

                case ClrElementType.Double:
                    return typeof(double);

                case ClrElementType.Float:
                    return typeof(float);

                case ClrElementType.Pointer:
                case ClrElementType.NativeInt:
                case ClrElementType.FunctionPointer:
                    return typeof(IntPtr);

                case ClrElementType.NativeUInt:
                    return typeof(UIntPtr);

                case ClrElementType.Int16:
                    return typeof(short);

                case ClrElementType.Int32:
                    return typeof(int);

                case ClrElementType.Int64:
                    return typeof(long);

                case ClrElementType.Int8:
                    return typeof(sbyte);

                case ClrElementType.UInt16:
                    return typeof(ushort);

                case ClrElementType.UInt32:
                    return typeof(uint);

                case ClrElementType.UInt64:
                    return typeof(ulong);

                case ClrElementType.UInt8:
                    return typeof(byte);

                default:
                    return null;
            }
        }
    }
}