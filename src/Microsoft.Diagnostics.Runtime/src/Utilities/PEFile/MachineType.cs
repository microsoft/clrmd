// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// The Machine types supporte by the portable executable (PE) File format
    /// </summary>
    public enum MachineType : ushort
    {
        /// <summary>
        /// Unknown machine type
        /// </summary>
        Native = 0,
        /// <summary>
        /// Intel X86 CPU
        /// </summary>
        X86 = 0x014c,
        /// <summary>
        /// Intel IA64
        /// </summary>
        ia64 = 0x0200,
        /// <summary>
        /// ARM 32 bit
        /// </summary>
        ARM = 0x01c0,
        /// <summary>
        /// Arm 64 bit
        /// </summary>
        Amd64 = 0x8664
    }
}