// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Module export symbol reader
    /// </summary>
    public interface IExportReader
    {
        /// <summary>
        /// Returns the address of a module export symbol if found
        /// </summary>
        /// <param name="baseAddress">module base address</param>
        /// <param name="name">symbol name (without the module name prepended)</param>
        /// <param name="offset">address returned</param>
        /// <returns>true if found</returns>
        bool TryGetSymbolAddress(ulong baseAddress, string name, out ulong offset);
    }
}
