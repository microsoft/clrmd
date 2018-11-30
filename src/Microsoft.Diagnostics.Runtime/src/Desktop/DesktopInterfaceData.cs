// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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