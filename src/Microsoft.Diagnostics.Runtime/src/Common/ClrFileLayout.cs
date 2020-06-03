// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    public enum ClrFileLayout
    {
        /// <summary>
        /// No known mapping associated
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Specifies that the sections of the image were mapped to their corresponding virtual addresses, as if it was loaded with LoadLibrary (not file layout).
        /// </summary>
        Mapped = 1,

        /// <summary>
        /// Specifies that the image was directly mapped via a single mmap/CreateFileMapping call (file layout).
        /// </summary>
        Flat = 2,

        /// <summary>
        /// Windows specific: Specifies that the image was loaded using LoadLibrary (not file layout).
        /// </summary>
        Loaded = 4
    }
}
