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
        /// </summary>
        bool TryGetSymbolName(ulong address, [NotNullWhen(true)] out string? symbolName, out ulong displacement);

        /// <summary>
        /// Resolves a <paramref name="symbolName"/> to its address.
        /// </summary>
        bool TryGetSymbolAddress(ulong moduleBase, string symbolName, out ulong address);

        /// <summary>
        /// Resolves the byte offset of instance field <paramref name="fieldName"/>
        /// on the type named <paramref name="typeName"/>.
        /// </summary>
        bool TryGetFieldOffset(ulong moduleBase, string typeName, string fieldName, out uint offset);
    }
}
