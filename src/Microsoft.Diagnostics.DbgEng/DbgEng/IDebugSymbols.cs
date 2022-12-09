namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public interface IDebugSymbols
    {
        public const int DEBUG_ANY_ID = unchecked((int)0xffffffff);

        int GetNumberModules(out int modules, out int unloadedModules);
        int GetImageBase(int index, out ulong baseAddress);
        int GetModuleParameters(ReadOnlySpan<ulong> baseAddresses, Span<DEBUG_MODULE_PARAMETERS> parameters);
        int GetModuleVersionInformation(int index, ulong address, string item, Span<byte> buffer);
    }
}
