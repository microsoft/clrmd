// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a work item on CLR's thread pool (native side).
    /// </summary>
    public abstract class NativeWorkItem
    {
        /// <summary>
        /// The type of work item this is.
        /// </summary>
        public abstract WorkItemKind Kind { get; }

        /// <summary>
        /// Returns the callback's address.
        /// </summary>
        public abstract ulong Callback { get; }

        /// <summary>
        /// Returns the pointer to the user's data.
        /// </summary>
        public abstract ulong Data { get; }
    }
}