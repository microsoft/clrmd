namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public interface IDebugDataSpaces
    {
        bool ReadVirtual(ulong address, Span<byte> buffer, out int read);
    }
}
