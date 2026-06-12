// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Host-supplied symbol resolver. Set via <see cref="DataTargetOptions.SymbolProvider"/>;
    /// ClrMD's COM data-target wrapper forwards <c>ICLRSymbolProvider</c> calls here.
    /// Implementations must be thread-safe.
    /// </summary>
    public interface IClrSymbolProvider
    {
        /// <summary>
        /// Resolves <paramref name="address"/> to the symbol that owns it.
        /// <paramref name="symbolName"/> should be module-qualified
        /// (<c>"Module!Symbol"</c>); callers split on '!' to recover the module name.
        /// </summary>
        bool TryGetSymbolName(ulong address, out string? symbolName, out ulong displacement);

        /// <summary>
        /// Resolves a symbol name (optionally <c>"Module!Symbol"</c>-qualified) to its address.
        /// Unqualified names should be searched across all loaded modules.
        /// </summary>
        bool TryGetSymbolAddress(string symbolName, out ulong address);
    }
}
