// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The COM implementation details of a single CCW entry.
    /// </summary>
    public abstract class ComInterfaceData
    {
        /// <summary>
        /// The CLR type this represents.
        /// </summary>
        public abstract ClrType Type { get; }

        /// <summary>
        /// The interface pointer of Type.
        /// </summary>
        public abstract ulong InterfacePointer { get; }
    }
}