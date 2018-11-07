// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopHeapInterface : ClrInterface
    {
        private string _name;
        private ClrInterface _base;
        public DesktopHeapInterface(string name, ClrInterface baseInterface)
        {
            _name = name;
            _base = baseInterface;
        }

        public override string Name
        {
            get { return _name; }
        }

        public override ClrInterface BaseInterface
        {
            get { return _base; }
        }
    }
}
