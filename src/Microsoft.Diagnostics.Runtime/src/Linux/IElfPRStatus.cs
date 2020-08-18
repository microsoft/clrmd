// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal interface IElfPRStatus
    {
        uint ProcessId { get; }

        uint ThreadId { get; }

        bool CopyContext(uint contextFlags, Span<byte> context);
    }
}