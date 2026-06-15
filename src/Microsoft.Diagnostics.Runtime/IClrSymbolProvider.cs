// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

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
        /// When <paramref name="moduleBase"/> is non-zero the lookup is scoped to the module
        /// loaded at that image base, otherwise the search spans every loaded module.
        /// </summary>
        bool TryGetSymbolName(ulong moduleBase, ulong address, [NotNullWhen(true)] out string? symbolName, out ulong displacement);

        /// <summary>
        /// Resolves a <paramref name="symbolName"/> to its address. When
        /// <paramref name="moduleBase"/> is non-zero the lookup is scoped to the module
        /// loaded at that image base; otherwise it searches every loaded module.
        /// </summary>
        bool TryGetSymbolAddress(ulong moduleBase, string symbolName, out ulong address);
    }
}
