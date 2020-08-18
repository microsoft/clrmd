// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IMethodData
    {
        IMethodHelpers Helpers { get; }

        ulong MethodDesc { get; }
        int Token { get; }
        MethodCompilationType CompilationType { get; }
        ulong HotStart { get; }
        uint HotSize { get; }
        ulong ColdStart { get; }
        uint ColdSize { get; }
    }
}