using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [DynamicInterfaceCastableImplementation]
    internal unsafe interface IDebugSymbolsWrapper : IDebugSymbols
    {
        int IDebugSymbols.GetNumberModules(out int modules, out int unloadedModules)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
            return vtable->GetNumberModules(self, out modules, out unloadedModules);
        }

        int IDebugSymbols.GetImageBase(int index, out ulong baseAddress)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
            return vtable->GetModuleByIndex(self, index, out baseAddress);
        }

        int IDebugSymbols.GetModuleParameters(ReadOnlySpan<ulong> baseAddresses, Span<DEBUG_MODULE_PARAMETERS> parameters)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);

            int count = Math.Min(baseAddresses.Length, parameters.Length);

            fixed (ulong* basesPtr = baseAddresses)
            fixed (DEBUG_MODULE_PARAMETERS* parametersPtr = parameters)
                return vtable->GetModuleParameters(self, count, basesPtr, 0, parametersPtr);
        }

        int IDebugSymbols.GetModuleVersionInformation(int index, ulong address, string item, Span<byte> buffer)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);

            byte[] itemBuffer = Encoding.ASCII.GetBytes(item);
            fixed (byte* bufferPtr = buffer)
            fixed (byte* itemBufferPtr = itemBuffer)
                return vtable->GetModuleVersionInformation(self, index, address, itemBufferPtr, bufferPtr, buffer.Length, out int size);
        }

        int IDebugSymbols.GetModuleName(DEBUG_MODNAME which, ulong baseAddress, out string name)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);

            int hr = vtable->GetModuleNameStringWide(self, which, DEBUG_ANY_ID, baseAddress, null, 0, out int size);
            if (hr < 0)
            {
                name = string.Empty;
                return hr;
            }

            char[] buffer = ArrayPool<char>.Shared.Rent(size);
            fixed(char* strPtr = buffer)
                hr = vtable->GetModuleNameStringWide(self, which, DEBUG_ANY_ID, baseAddress, strPtr, size, out _);

            if (hr < 0)
                name = string.Empty;
            else
                name = new string(new Span<char>(buffer)[0..(size-1)]);

            ArrayPool<char>.Shared.Return(buffer);

            return hr;
        }

        int IDebugSymbols.GetModuleByOffset(ulong baseAddr, int nextIndex, out int index, out ulong claimedBaseAddr)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
            return vtable->GetModuleByOffset(self, baseAddr, nextIndex, out index, out claimedBaseAddr);
        }

        private static void GetVTable(object ths, out nint self, out IDebugSymbolsVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugSymbols;
            vtable = *(IDebugSymbolsVtable**)self;
        }
    }
}
