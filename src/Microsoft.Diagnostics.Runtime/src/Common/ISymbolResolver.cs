// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// ISymbolResolver represents a single symbol module (PDB) loaded into the process.
    /// </summary>
    public interface ISymbolResolver
    {
        /// <summary>
        /// Retrieves the given symbol's name based on its RVA.
        /// </summary>
        /// <param name="rva">A relative virtual address in the module.</param>
        /// <returns>The symbol corresponding to RVA.</returns>
        string GetSymbolNameByRVA(uint rva);
    }
}