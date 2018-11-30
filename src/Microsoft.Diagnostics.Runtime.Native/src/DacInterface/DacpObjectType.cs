// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public enum DacpObjectType
    {
        OBJ_FREE = 0,
        OBJ_OBJECT = 1,
        OBJ_VALUETYPE = 2,
        OBJ_ARRAY = 3,
        OBJ_OTHER = 4
    }
}