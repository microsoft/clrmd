// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Tests.Fixtures
{
    /// <summary>
    /// Single-file variant of <see cref="ArrayConnection"/>.
    /// </summary>
    public class SingleFileArrayConnection : ArrayConnection
    {
        public SingleFileArrayConnection() : base(singleFile: true)
        {
        }
    }
}
