// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopManagedWorkItem : ManagedWorkItem
    {
        private ClrType _type;
        private ulong _addr;

        public DesktopManagedWorkItem(ClrType type, ulong addr)
        {
            _type = type;
            _addr = addr;
        }

        public override ulong Object
        {
            get { return _addr; }
        }

        public override ClrType Type
        {
            get { return _type; }
        }
    }
}
