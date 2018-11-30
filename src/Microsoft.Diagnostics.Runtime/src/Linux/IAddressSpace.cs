namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal interface IAddressSpace
    {
        int Read(long position, byte[] buffer, int bufferOffset, int count);
        long Length { get; }
        string Name { get; }
    }
}