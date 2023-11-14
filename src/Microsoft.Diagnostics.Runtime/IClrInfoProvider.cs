// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Diagnostics.Runtime.AbstractDac;

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

        /// <summary>
        /// Creates an instance of an <see cref="IServiceProvider"/> for the given <see cref="ClrInfo"/>.
        /// Note that this will only be called on the interface which previously provided the given ClrInfo.
        /// </summary>
        /// <param name="clrInfo">A ClrInfo previously returned by this same instance.</param>
        /// <param name="providedPath">The path provided to DataTarget.CreateRuntime.</param>
        /// <param name="ignorePathMismatch">The ignore mismatch parameter provided to DataTarget.CreateRuntime.</param>
        /// <returns>An <see cref="IServiceProvider"/> interface to use with the specified clr runtime that provides
        /// <see cref="IAbstractRuntime"/> related services.</returns>
        IServiceProvider GetDacServices(ClrInfo clrInfo, string? providedPath, bool ignorePathMismatch);
    }
}