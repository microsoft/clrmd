// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.MacOS
{
    internal readonly struct MachOCommand
    {
        internal readonly MachOCommandType Command;
        internal readonly int CommandSize;
    }
}
