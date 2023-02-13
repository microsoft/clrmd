﻿// Licensed to the .NET Foundation under one or more agreements.
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
    }
}