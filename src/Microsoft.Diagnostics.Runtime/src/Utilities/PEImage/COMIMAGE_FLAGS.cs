// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [Flags]
    public enum COMIMAGE_FLAGS
    {
        ILONLY = 0x00000001,
        _32BITREQUIRED = 0x00000002,
        IL_LIBRARY = 0x00000004,
        STRONGNAMESIGNED = 0x00000008,
        NATIVE_ENTRYPOINT = 0x00000010,
        TRACKDEBUGDATA = 0x00010000,
        _32BITPREFERRED = 0x00020000,
    }
}
