// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [DynamicInterfaceCastableImplementation]
    internal unsafe interface IDebugSymbolsWrapper : IDebugSymbols
    {
        string? IDebugSymbols.SymbolPath
        {
            get
            {
                GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
                int hr = vtable->GetSymbolPathWide(self, null, 0, out int size);

                if (hr < 0)
                    return null;
                else if (size is 0 or 1)
                    return string.Empty;

                char[] buffer = ArrayPool<char>.Shared.Rent(size);
                try
                {
                    fixed (char* ptr = buffer)
                        hr = vtable->GetSymbolPathWide(self, ptr, size, out size);

                    if (hr < 0)
                        return null;

                    return new(buffer, 0, size - 1);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }
            }
            set
            {
                GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
                if (string.IsNullOrWhiteSpace(value))
                {
                    fixed (char* ptr = string.Empty)
                        vtable->SetSymbolPathWide(self, ptr);
                }
                else
                {
                    fixed (char* ptr = value)
                        vtable->SetSymbolPathWide(self, ptr);
                }
            }
        }

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

        int IDebugSymbols.Reload(string module)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
            fixed (char* ptr = module)
                return vtable->ReloadWide(self, ptr);
        }

        int IDebugSymbols.GetModuleParameters(ulong baseAddresses, out DEBUG_MODULE_PARAMETERS parameters)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);

            fixed (DEBUG_MODULE_PARAMETERS* parametersPtr = &parameters)
                return vtable->GetModuleParameters(self, 1, &baseAddresses, 0, parametersPtr);
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
            fixed (char* strPtr = buffer)
                hr = vtable->GetModuleNameStringWide(self, which, DEBUG_ANY_ID, baseAddress, strPtr, size, out _);

            if (hr < 0)
                name = string.Empty;
            else
                name = new string(new Span<char>(buffer)[0..(size - 1)]);

            ArrayPool<char>.Shared.Return(buffer);

            return hr;
        }

        int IDebugSymbols.GetTypeId(ulong moduleBase, string name, out ulong typeId)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
            fixed (char* namePtr = name)
                return vtable->GetTypeIdWide(self, moduleBase, namePtr, out typeId);
        }

        int IDebugSymbols.GetFieldOffset(ulong moduleBase, ulong typeId, string name, out ulong offset)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
            fixed (char* namePtr = name)
                return vtable->GetFieldOffsetWide(self, moduleBase, typeId, namePtr, out offset);
        }

        int IDebugSymbols.GetModuleByOffset(ulong baseAddr, int nextIndex, out int index, out ulong claimedBaseAddr)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
            return vtable->GetModuleByOffset(self, baseAddr, nextIndex, out index, out claimedBaseAddr);
        }

        bool IDebugSymbols.GetOffsetByName(string name, out ulong offset)
        {
            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);

            fixed (char* namePtr = name)
            {
                int hr = vtable->GetOffsetByNameWide(self, namePtr, out offset);
                if (hr == 0)
                    return true;
            }

            offset = 0;
            return false;
        }

        bool IDebugSymbols.GetNameByOffset(ulong address, [NotNullWhen(true)] out string? name, out ulong displacement)
        {
            displacement = 0;

            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
            int hr = vtable->GetNameByOffsetWide(self, address, null, 0, out int size, out _);
            if (hr < 0)
            {
                name = null;
                return false;
            }

            if (size is 0 or 1)
            {
                name = string.Empty;
                return true;
            }

            char[] buffer = ArrayPool<char>.Shared.Rent(size);
            try
            {
                fixed (char* ptr = buffer)
                    hr = vtable->GetNameByOffsetWide(self, address, ptr, size, out size, out displacement);

                if (hr == 0)
                    name = new(buffer, 0, size - 1);
                else
                    name = null;

                return hr == 0;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }


        bool IDebugSymbols.GetNameByInlineContext(ulong offset, uint inlineContext, [NotNullWhen(true)] out string? name, out ulong displacement)
        {
            displacement = 0;

            GetVTable(this, out nint self, out IDebugSymbolsVtable* vtable);
            int hr = vtable->GetNameByInlineContextWide(self, offset, inlineContext, null, 0, out int size, out _);
            if (hr < 0)
            {
                name = null;
                return false;
            }

            if (size is 0 or 1)
            {
                name = string.Empty;
                return true;
            }

            char[] buffer = ArrayPool<char>.Shared.Rent(size);
            try
            {
                fixed (char* ptr = buffer)
                    hr = vtable->GetNameByInlineContextWide(self, offset, inlineContext, ptr, size, out size, out displacement);

                if (hr == 0)
                    name = new(buffer, 0, size - 1);
                else
                    name = null;

                return hr == 0;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        private static void GetVTable(object ths, out nint self, out IDebugSymbolsVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugSymbols;
            vtable = *(IDebugSymbolsVtable**)self;
        }
    }
}