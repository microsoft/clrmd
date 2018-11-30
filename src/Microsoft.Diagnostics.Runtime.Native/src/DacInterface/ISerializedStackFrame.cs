using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("4bf18ce1-8166-4dfc-b540-4a79dd1ebe19")]
    internal interface ISerializedStackFrame
    {
        ulong IP { get; }
    }
}