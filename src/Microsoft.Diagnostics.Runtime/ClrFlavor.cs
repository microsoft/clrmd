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
        /// For .Net Core
        /// </summary>
        Core = 3
    }
}
