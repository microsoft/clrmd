// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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