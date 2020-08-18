// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    public enum ModuleLayout
    {
        Unknown = 0,
        Mapped = 1,
        Flat = 2,
        Loaded = 4
    }
}