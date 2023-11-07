// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// Helpers for COM information.
    ///
    /// This interface is optional.
    /// </summary>
    internal interface IAbstractComHelpers
    {
        // COM
        IEnumerable<ClrRcwCleanupData> EnumerateRcwCleanupData();
        bool GetCcwInfo(ulong obj, out CcwInfo info);
        bool GetRcwInfo(ulong obj, out RcwInfo info);
    }
}