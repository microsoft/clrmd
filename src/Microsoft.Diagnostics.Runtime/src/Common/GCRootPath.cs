// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a path of objects from a root to an object.
    /// </summary>
    public readonly struct GCRootPath
    {
        /// <summary>
        /// Gets the location that roots the object.
        /// </summary>
        public IClrRoot Root { get; }

        /// <summary>
        /// Gets the path from Root to a given target object.
        /// </summary>
        public ImmutableArray<ClrObject> Path { get; }

        public GCRootPath(IClrRoot root, ImmutableArray<ClrObject> path)
        {
            Root = root;
            Path = path;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append($"{Root.RootKind.GetName()} @{Root.Address:x12}");

            foreach (ClrObject obj in Path)
                builder.Append($" -> {obj}");

            return builder.ToString();
        }
    }
}
