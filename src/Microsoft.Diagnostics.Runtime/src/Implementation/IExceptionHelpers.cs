// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IExceptionHelpers
    {
        IDataReader DataReader { get; }
        ImmutableArray<ClrStackFrame> GetExceptionStackTrace(ClrThread? thread, ClrObject obj);
        uint GetInnerExceptionOffset(ClrType type);
        uint GetHResultOffset(ClrType type);
        uint GetMessageOffset(ClrType type);
    }
}
