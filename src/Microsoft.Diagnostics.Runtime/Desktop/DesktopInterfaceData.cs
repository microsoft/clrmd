// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopInterfaceData : ComInterfaceData
    {
        public override ClrType Type
        {
            get { return _type; }
        }

        public override ulong InterfacePointer
        {
            get { return _interface; }
        }

        public DesktopInterfaceData(ClrType type, ulong ptr)
        {
            _type = type;
            _interface = ptr;
        }

        private ulong _interface;
        private ClrType _type;
    }
}
