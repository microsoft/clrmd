// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal enum ElfProgramHeaderType : uint
    {
        Null = 0,
        Load = 1,
        Dynamic = 2,
        Interp = 3,
        Note = 4,
        Shlib = 5,
        Phdr = 6
    }
}