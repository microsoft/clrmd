// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal enum ElfClass : byte
    {
        None = 0,
        Class32 = 1,
        Class64 = 2
    }
}