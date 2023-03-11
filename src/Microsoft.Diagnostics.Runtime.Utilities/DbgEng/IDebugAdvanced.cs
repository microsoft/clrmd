namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public interface IDebugAdvanced
    {
        int GetThreadContext(Span<byte> buffer);
    }
}