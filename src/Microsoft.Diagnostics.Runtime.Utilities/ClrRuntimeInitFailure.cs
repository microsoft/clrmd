// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    public class ClrRuntimeInitFailure
    {
        public ClrRuntimeInitFailure(ClrInfo clr, Exception ex)
        {
            Clr = clr;
            Exception = ex;
        }

        public ClrInfo Clr { get; internal init; }
        public Exception Exception { get; internal init; }
    }
}