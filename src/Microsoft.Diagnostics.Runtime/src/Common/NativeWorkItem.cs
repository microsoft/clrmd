// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


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
