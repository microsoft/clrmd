// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Returns the "flavor" of CLR this module represents.
    /// </summary>
    public enum ClrFlavor
    {
        /// <summary>
        /// This is the full version of CLR included with windows.
        /// </summary>
        Desktop = 0,

        /// <summary>
        /// This originally was for Silverlight and other uses of "coreclr", but now
        /// there are several flavors of coreclr, some of which are no longer supported.
        /// </summary>
        [Obsolete]
        CoreCLR = 1,

        /// <summary>
        /// Used for .Net Native.
        /// </summary>
        [Obsolete(".Net Native support is being split out of this library into a different one.")]
        Native = 2,

        /// <summary>
        /// For .Net Core
        /// </summary>
        Core = 3
    }
}
