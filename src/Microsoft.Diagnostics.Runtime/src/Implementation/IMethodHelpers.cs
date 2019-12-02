// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IMethodHelpers
    {
        IDataReader DataReader { get; }

        string GetSignature(ulong methodDesc);
        IReadOnlyList<ILToNativeMap> GetILMap(ulong nativeCode, in HotColdRegions hotColdInfo);
        ulong GetILForModule(ulong address, uint rva);
    }
}