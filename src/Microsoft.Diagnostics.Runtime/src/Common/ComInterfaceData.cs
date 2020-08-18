// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The COM implementation details of a single CCW entry.
    /// </summary>
    public readonly struct ComInterfaceData
    {
        /// <summary>
        /// Gets the CLR type this represents.
        /// </summary>
        public ClrType? Type { get; }

        /// <summary>
        /// Gets the interface pointer of Type.
        /// </summary>
        public ulong InterfacePointer { get; }

        public ComInterfaceData(ClrType? type, ulong pointer)
        {
            Type = type;
            InterfacePointer = pointer;
        }
    }
}