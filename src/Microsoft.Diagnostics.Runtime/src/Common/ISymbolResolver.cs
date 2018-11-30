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