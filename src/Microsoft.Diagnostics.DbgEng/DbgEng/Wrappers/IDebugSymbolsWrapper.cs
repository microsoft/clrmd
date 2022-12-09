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


        private static void GetVTable(object ths, out nint self, out IDebugSymbolsVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugSymbols;
            vtable = *(IDebugSymbolsVtable**)self;
        }
    }
}
