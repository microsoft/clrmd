// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IAbstractThreadPoolProvider
    {
        bool GetLegacyThreadPoolData(out ThreadPoolData data);
        bool GetLegacyWorkRequestData(ulong workRequest, out WorkRequestData workRequestData);
    }
}