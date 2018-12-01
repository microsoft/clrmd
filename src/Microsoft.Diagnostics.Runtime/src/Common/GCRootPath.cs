// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a path of objects from a root to an object.
    /// </summary>
    public struct GCRootPath
    {
        /// <summary>
        /// The location that roots the object.
        /// </summary>
        public ClrRoot Root { get; set; }

        /// <summary>
        /// The path from Root to a given target object.
        /// </summary>
        public ClrObject[] Path { get; set; }
    }
}