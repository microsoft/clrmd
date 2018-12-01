// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A method's mapping from IL to native offsets.
    /// </summary>
    public struct ILToNativeMap
    {
        /// <summary>
        /// The IL offset for this entry.
        /// </summary>
        public int ILOffset;

        /// <summary>
        /// The native start offset of this IL entry.
        /// </summary>
        public ulong StartAddress;

        /// <summary>
        /// The native end offset of this IL entry.
        /// </summary>
        public ulong EndAddress;

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A visual display of the map entry.</returns>
        public override string ToString()
        {
            return $"{ILOffset,2:X} - [{StartAddress:X}-{EndAddress:X}]";
        }

#pragma warning disable 0169
        /// <summary>
        /// Reserved.
        /// </summary>
        private int _reserved;
#pragma warning restore 0169
    }
}