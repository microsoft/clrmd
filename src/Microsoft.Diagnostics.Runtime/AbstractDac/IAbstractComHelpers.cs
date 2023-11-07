// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// Helpers for COM information.
    ///
    /// This interface is optional.
    ///
    /// This interface is not "stable" and may change even in minor or patch
    /// versions of ClrMD.
    /// </summary>
    internal interface IAbstractComHelpers
    {
        IEnumerable<ClrRcwCleanupData> EnumerateRcwCleanupData();
        bool GetCcwInfo(ulong obj, out CcwInfo info);
        bool GetRcwInfo(ulong obj, out RcwInfo info);
    }
}