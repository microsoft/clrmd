// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Native
{
    internal class NativeRoot
    {
        public ulong Address { get; internal set; }
        public ulong Object { get; internal set; }
        public GCRootKind Kind { get; internal set; }
        public bool IsInterior { get; internal set; }
        public bool IsPinned { get; internal set; }
    }
}