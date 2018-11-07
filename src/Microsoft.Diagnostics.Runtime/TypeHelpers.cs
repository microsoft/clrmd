// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Reflection;

namespace Microsoft.Diagnostics.Runtime
{
    static class TypeHelpers
    {
        static internal Guid GetGuid(this Type self)
        {
            return self.GUID;
        }
    }
}