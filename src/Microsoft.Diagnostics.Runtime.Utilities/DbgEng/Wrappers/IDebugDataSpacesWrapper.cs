// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [DynamicInterfaceCastableImplementation]
    internal unsafe interface IDebugDataSpacesWrapper : IDebugDataSpaces
    {
        int IDebugDataSpaces.WriteVirtual(ulong address, Span<byte> buffer, out int written)
        {
            GetVTable(this, out nint self, out IDebugDataSpacesVtable* vtable);
            fixed (byte* ptr = buffer)
                return vtable->WriteVirtual(self, address, ptr, buffer.Length, out written);
        }

        bool IDebugDataSpaces.ReadVirtual(ulong address, Span<byte> buffer, out int read)
        {
            GetVTable(this, out nint self, out IDebugDataSpacesVtable* vtable);

            fixed (byte* ptr = buffer)
            {
                int hr = vtable->ReadVirtual(self, address, ptr, buffer.Length, out read);
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