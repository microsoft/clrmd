// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    // Technically 0x40 is the only flag that could be set in combination with others, but we might
    // want to test for the presence of this value so we'll mark the enum as 'Flags'.
    // But in almost all cases we just use one of the individual values.
    // Reflection (Enum.ToString) appears to do a good job of picking the simplest combination to
    // represent a value as a set of flags, but the Visual Studio debugger does not - it just does
    // a linear search from the start looking for matches.  To make debugging these values easier,
    // their order is reversed so that VS always produces the simplest representation.
    [Flags]
    public enum CorElementType
    {
        ELEMENT_TYPE_PINNED = 0x45,
        ELEMENT_TYPE_SENTINEL = 0x41,
        ELEMENT_TYPE_MODIFIER = 0x40,

        ELEMENT_TYPE_MAX = 0x22,

        ELEMENT_TYPE_INTERNAL = 0x21,

        ELEMENT_TYPE_CMOD_OPT = 0x20,
        ELEMENT_TYPE_CMOD_REQD = 0x1f,

        ELEMENT_TYPE_MVAR = 0x1e,
        ELEMENT_TYPE_SZARRAY = 0x1d,
        ELEMENT_TYPE_OBJECT = 0x1c,
        ELEMENT_TYPE_FNPTR = 0x1b,
        ELEMENT_TYPE_U = 0x19,
        ELEMENT_TYPE_I = 0x18,

        ELEMENT_TYPE_TYPEDBYREF = 0x16,
        ELEMENT_TYPE_GENERICINST = 0x15,
        ELEMENT_TYPE_ARRAY = 0x14,
        ELEMENT_TYPE_VAR = 0x13,
        ELEMENT_TYPE_CLASS = 0x12,
        ELEMENT_TYPE_VALUETYPE = 0x11,

        ELEMENT_TYPE_BYREF = 0x10,
        ELEMENT_TYPE_PTR = 0xf,

        ELEMENT_TYPE_STRING = 0xe,
        ELEMENT_TYPE_R8 = 0xd,
        ELEMENT_TYPE_R4 = 0xc,
        ELEMENT_TYPE_U8 = 0xb,
        ELEMENT_TYPE_I8 = 0xa,
        ELEMENT_TYPE_U4 = 0x9,
        ELEMENT_TYPE_I4 = 0x8,
        ELEMENT_TYPE_U2 = 0x7,
        ELEMENT_TYPE_I2 = 0x6,
        ELEMENT_TYPE_U1 = 0x5,
        ELEMENT_TYPE_I1 = 0x4,
        ELEMENT_TYPE_CHAR = 0x3,
        ELEMENT_TYPE_BOOLEAN = 0x2,
        ELEMENT_TYPE_VOID = 0x1,
        ELEMENT_TYPE_END = 0x0
    }
}