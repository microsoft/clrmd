// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal interface IElfPRStatus
    {
        uint ProcessId { get; }

        uint ThreadId { get; }

        unsafe bool CopyContext(uint contextFlags, uint contextSize, void* context);
    }
}