// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class HandleRoot : ClrRoot
    {
        public HandleRoot(ulong addr, ulong obj, ClrType type, HandleType hndType, GCRootKind kind, ClrAppDomain domain)
        {
            Name = Enum.GetName(typeof(HandleType), hndType) + " handle";
            Address = addr;
            Object = obj;
            Kind = kind;
            Type = type;
            AppDomain = domain;
        }

        public override ClrAppDomain AppDomain { get; }
        public override bool IsPinned => Kind == GCRootKind.Pinning || Kind == GCRootKind.AsyncPinning;
        public override GCRootKind Kind { get; }
        public override string Name { get; }
        public override ClrType Type { get; }
    }
}