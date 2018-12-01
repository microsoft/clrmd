// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IMethodDescData
    {
        ulong GCInfo { get; }
        ulong MethodDesc { get; }
        ulong Module { get; }
        uint MDToken { get; }
        ulong NativeCodeAddr { get; }
        ulong MethodTable { get; }
        MethodCompilationType JITType { get; }
        ulong ColdStart { get; }
        uint ColdSize { get; }
        uint HotSize { get; }
    }
}