// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class TypeHelpers
    {
        internal static Guid GetGuid(this Type self)
        {
            return self.GUID;
        }
    }
}