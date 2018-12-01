// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopInterfaceData : ComInterfaceData
    {
        public override ClrType Type { get; }

        public override ulong InterfacePointer { get; }

        public DesktopInterfaceData(ClrType type, ulong ptr)
        {
            Type = type;
            InterfacePointer = ptr;
        }
    }
}