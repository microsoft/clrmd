// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public interface IDebugDataSpaces
    {
        bool ReadVirtual(ulong address, Span<byte> buffer, out int read);
        int WriteVirtual(ulong address, Span<byte> buffer, out int written);
        bool Search(ulong offset, ulong length, Span<byte> pattern, int granularity, out ulong offsetFound);
    }
}