﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
