// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopHeapInterface : ClrInterface
    {
        public DesktopHeapInterface(string name, ClrInterface baseInterface)
        {
            Name = name;
            BaseInterface = baseInterface;
        }

        public override string Name { get; }

        public override ClrInterface BaseInterface { get; }
    }
}