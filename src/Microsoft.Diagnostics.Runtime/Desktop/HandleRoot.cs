// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class HandleRoot : ClrRoot
    {
        private GCRootKind _kind;
        private string _name;
        private ClrType _type;
        private ClrAppDomain _domain;

        public HandleRoot(ulong addr, ulong obj, ClrType type, HandleType hndType, GCRootKind kind, ClrAppDomain domain)
        {
            _name = Enum.GetName(typeof(HandleType), hndType) + " handle";
            Address = addr;
            Object = obj;
            _kind = kind;
            _type = type;
            _domain = domain;
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return _domain;
            }
        }

        public override bool IsPinned
        {
            get
            {
                return Kind == GCRootKind.Pinning || Kind == GCRootKind.AsyncPinning;
            }
        }

        public override GCRootKind Kind
        {
            get { return _kind; }
        }

        public override string Name
        {
            get
            {
                return _name;
            }
        }

        public override ClrType Type
        {
            get { return _type; }
        }
    }
}
