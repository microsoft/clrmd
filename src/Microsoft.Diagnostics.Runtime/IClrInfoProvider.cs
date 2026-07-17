// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime
{
    public interface IClrInfoProvider
    {
        /// <summary>
        /// Inspect the given module and if it's a CLR runtime module, provide a ClrInfo for it.
        /// </summary>
        /// <param name="dataTarget">The DataTarget for the given module.</param>
        /// <param name="module">The module to inspect.</param>
        /// <returns>A ClrInfo if module is a CLR runtime, null otherwise.</returns>
        ClrInfo? ProvideClrInfoForModule(DataTarget dataTarget, ModuleInfo module);
    }
}