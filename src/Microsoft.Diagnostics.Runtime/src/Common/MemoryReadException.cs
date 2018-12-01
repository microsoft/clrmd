// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Thrown when we fail to read memory from the target process.
    /// </summary>
    public class MemoryReadException : IOException
    {
        /// <summary>
        /// The address of memory that could not be read.
        /// </summary>
        public ulong Address { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="address">The address of memory that could not be read.</param>
        public MemoryReadException(ulong address)
            : base($"Could not read memory at {address:x}.")
        {
        }
    }
}