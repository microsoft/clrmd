// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.Interfaces;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal interface IClrThreadHelpers
    {
        IDataReader DataReader { get; }
        IEnumerable<ClrStackRoot> EnumerateStackRoots(ClrThread thread);
        IEnumerable<ClrStackFrame> EnumerateStackTrace(ClrThread thread, bool includeContext);
    }
}