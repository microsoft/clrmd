// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public interface IDebugAdvanced
    {
        int GetThreadContext(Span<byte> buffer);
        int GetCommentWide(out string? comment);
    }
}