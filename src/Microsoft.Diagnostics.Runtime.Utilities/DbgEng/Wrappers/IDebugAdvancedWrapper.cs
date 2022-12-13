using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{

    [DynamicInterfaceCastableImplementation]
    internal unsafe interface IDebugAdvancedWrapper : IDebugAdvanced
    {
        int IDebugAdvanced.GetThreadContext(Span<byte> buffer)
        {
            GetVTable(this, out nint self, out IDebugAdvancedVtable* vtable);

            fixed (byte* ptr = buffer)
                return vtable->GetThreadContext(self, ptr, buffer.Length);
        }

        private static void GetVTable(object ths, out nint self, out IDebugAdvancedVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugAdvanced;
            vtable = *(IDebugAdvancedVtable**)self;
        }
    }
}
