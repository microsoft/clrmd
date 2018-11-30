// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A managed threadpool object.
    /// </summary>
    public abstract class ManagedWorkItem
    {
        /// <summary>
        /// The object address of this entry.
        /// </summary>
        public abstract ulong Object { get; }

        /// <summary>
        /// The type of Object.
        /// </summary>
        public abstract ClrType Type { get; }
    }
}