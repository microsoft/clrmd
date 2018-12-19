// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Represents a Portable Executable (PE) Data directory.  This is just a well known optional 'Blob' of memory (has a starting point and size)
    /// </summary>
    [Obsolete]
    public struct IMAGE_DATA_DIRECTORY
    {
        /// <summary>
        /// The start of the data blob when the file is mapped into memory
        /// </summary>
        public int VirtualAddress;
        /// <summary>
        /// The length of the data blob.
        /// </summary>
        public int Size;
    }
}