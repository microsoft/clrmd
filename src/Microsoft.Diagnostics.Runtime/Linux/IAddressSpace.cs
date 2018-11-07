namespace Microsoft.Diagnostics.Runtime.Linux
{
    interface IAddressSpace
    {
        int Read(long position, byte[] buffer, int bufferOffset, int count);
        long Length { get; }
        string Name { get; }
    }
}
