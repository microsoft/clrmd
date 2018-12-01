// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The type of GCRoot that a ClrRoot represnts.
    /// </summary>
    public enum GCRootKind
    {
        /// <summary>
        /// The root is a static variable.
        /// </summary>
        StaticVar,

        /// <summary>
        /// The root is a thread static.
        /// </summary>
        ThreadStaticVar,

        /// <summary>
        /// The root is a local variable (or compiler generated temporary variable).
        /// </summary>
        LocalVar,

        /// <summary>
        /// The root is a strong handle.
        /// </summary>
        Strong,

        /// <summary>
        /// The root is a weak handle.
        /// </summary>
        Weak,

        /// <summary>
        /// The root is a strong pinning handle.
        /// </summary>
        Pinning,

        /// <summary>
        /// The root comes from the finalizer queue.
        /// </summary>
        Finalizer,

        /// <summary>
        /// The root is an async IO (strong) pinning handle.
        /// </summary>
        AsyncPinning,

        /// <summary>
        /// The max value of this enum.
        /// </summary>
        Max = AsyncPinning
    }
}