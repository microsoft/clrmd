// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class FinalizerRoot : ClrRoot
    {
        private ClrType _type;
        public FinalizerRoot(ulong obj, ClrType type)
        {
            Object = obj;
            _type = type;
        }

        public override GCRootKind Kind
        {
            get { return GCRootKind.Finalizer; }
        }

        public override string Name
        {
            get
            {
                return "finalization handle";
            }
        }

        public override ClrType Type
        {
            get { return _type; }
        }
    }
}
