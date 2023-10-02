// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime
{
    internal class SingleFileClrInfoProvider : DotNetClrInfoProvider
    {
        public override ClrInfo? ProvideClrInfoForModule(DataTarget dataTarget, ModuleInfo module)
        {
            ulong runtimeInfo = 0;
            if ((dataTarget.DataReader.TargetPlatform != OSPlatform.Windows) || Path.GetExtension(module.FileName).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                runtimeInfo = module.GetExportSymbolAddress(ClrRuntimeInfo.SymbolValue);

            if (runtimeInfo == 0)
                return null;

            ClrInfo result = CreateClrInfo(dataTarget, module, runtimeInfo, ClrFlavor.Core);
            result.IsSingleFile = true;
            return result;
        }
    }
}