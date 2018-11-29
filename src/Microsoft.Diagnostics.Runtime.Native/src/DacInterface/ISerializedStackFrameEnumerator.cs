using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6091d53a-9371-4573-ae00-93b61d17ca04")]
    internal interface ISerializedStackFrameEnumerator
    {
        bool HasNext();
        ISerializedStackFrame Next();
    }
}