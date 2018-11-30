using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("d50b1d22-dc01-4d68-b71d-761f9d49f980")]
    internal interface ISerializedExceptionEnumerator
    {
        bool HasNext();
        ISerializedException Next();
    }
}