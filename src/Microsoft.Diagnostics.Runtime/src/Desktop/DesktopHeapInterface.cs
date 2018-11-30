// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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