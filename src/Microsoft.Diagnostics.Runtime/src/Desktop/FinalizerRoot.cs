// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class FinalizerRoot : ClrRoot
    {
        public FinalizerRoot(ulong obj, ClrType type)
        {
            Object = obj;
            Type = type;
        }

        public override GCRootKind Kind => GCRootKind.Finalizer;
        public override string Name => "finalization handle";
        public override ClrType Type { get; }
    }
}