using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [DynamicInterfaceCastableImplementation]
    internal unsafe interface IDebugDataSpacesWrapper : IDebugDataSpaces
    {
        bool IDebugDataSpaces.ReadVirtual(ulong address, Span<byte> buffer, out int read)
        {
            GetVTable(this, out nint self, out IDebugDataSpacesVtable* vtable);

            int bytesRead = 0;
            fixed (byte* ptr = buffer)
            {
                int hr = vtable->ReadVirtual(self, address, ptr, buffer.Length, &bytesRead);
                read = bytesRead;
                return hr >= 0;
            }
        }

        bool IDebugDataSpaces.Search(ulong offset, ulong length, Span<byte> pattern, int granularity, out ulong offsetFound)
        {
            GetVTable(this, out nint self, out IDebugDataSpacesVtable* vtable);
            fixed (byte* ptr = pattern)
            {
                ulong found = 0;
                int hr = vtable->SearchVirtual(self, offset, length, ptr, pattern.Length, granularity, &found);

                offsetFound = found;
                return hr == 0;
            }
        }

        private static void GetVTable(object ths, out nint self, out IDebugDataSpacesVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugDataSpaces;
            vtable = *(IDebugDataSpacesVtable**)self;
        }
    }
}
