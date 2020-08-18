// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal enum ElfNoteType : uint
    {
        PrpsStatus = 1,
        PrpsFpreg = 2,
        PrpsInfo = 3,
        TASKSTRUCT = 4,
        Aux = 6,

        File = 0x46494c45 // "FILE" in ascii
    }
}