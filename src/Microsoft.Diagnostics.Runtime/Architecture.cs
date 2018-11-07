// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The architecture of a process.
    /// </summary>
    public enum Architecture
    {
        /// <summary>
        /// Unknown.  Should never be exposed except in case of error.
        /// </summary>
        Unknown,

        /// <summary>
        /// x86.
        /// </summary>
        X86,

        /// <summary>
        /// x64
        /// </summary>
        Amd64,

        /// <summary>
        /// ARM
        /// </summary>
        Arm
    }

}
