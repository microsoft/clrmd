// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


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
