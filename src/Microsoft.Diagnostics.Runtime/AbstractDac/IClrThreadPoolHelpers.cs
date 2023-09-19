// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IClrThreadPoolHelpers
    {
        bool GetLegacyThreadPoolData(out ThreadPoolData data, out bool usePortableThreadPool);
        bool GetLegacyWorkRequestData(ulong workRequest, out WorkRequestData workRequestData);
    }
}