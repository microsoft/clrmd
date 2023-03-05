// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime
{
    public interface IDataTarget
    {
        CacheOptions CacheOptions { get; }
        ImmutableArray<IClrInfo> ClrVersions { get; }
        IDataReader DataReader { get; }
        IFileLocator? FileLocator { get; set; }

        IEnumerable<ModuleInfo> EnumerateModules();
        void SetSymbolPath(string symbolPath);
    }
}