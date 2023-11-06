// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public interface IDebugSymbols
    {
        public const int DEBUG_ANY_ID = unchecked((int)0xffffffff);

        string? SymbolPath { get; set; }

        int GetNumberModules(out int modules, out int unloadedModules);
        int GetImageBase(int index, out ulong baseAddress);
        int Reload(string module);
        int GetModuleParameters(ReadOnlySpan<ulong> baseAddresses, Span<DEBUG_MODULE_PARAMETERS> parameters);
        int GetModuleParameters(ulong baseAddress, out DEBUG_MODULE_PARAMETERS parameters);
        int GetModuleVersionInformation(int index, ulong address, string item, Span<byte> buffer);
        int GetModuleName(DEBUG_MODNAME which, ulong baseAddress, out string name);
        int GetModuleByOffset(ulong baseAddr, int nextIndex, out int index, out ulong claimedBaseAddr);
        bool GetNameByOffset(ulong address, [NotNullWhen(true)] out string? name, out ulong displacement);
        bool GetNameByInlineContext(ulong offset, uint inlineContext, [NotNullWhen(true)] out string? name, out ulong displacement);
        bool GetOffsetByName(string name, out ulong offset);
        int GetTypeId(ulong moduleBase, string name, out ulong typeId);
        int GetFieldOffset(ulong moduleBase, ulong typeId, string name, out ulong offset);
    }
}