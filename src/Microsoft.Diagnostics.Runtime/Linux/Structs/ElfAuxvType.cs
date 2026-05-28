// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal enum ElfAuxvType
    {
        Null = 0,           // end of vector
        Base = 7,           // base address of interpreter
        Execfn = 31,        // pointer to NUL-terminated absolute path of the
                            // executable that was originally exec()'d. Lives
                            // in the target process's address space — must
                            // be dereferenced via ReadMemory to recover the
                            // string itself.
    }
}