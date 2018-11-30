// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopManagedWorkItem : ManagedWorkItem
    {
        public DesktopManagedWorkItem(ClrType type, ulong addr)
        {
            Type = type;
            Object = addr;
        }

        public override ulong Object { get; }

        public override ClrType Type { get; }
    }
}