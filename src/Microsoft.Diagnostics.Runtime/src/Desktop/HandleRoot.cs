// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class HandleRoot : ClrRoot
    {
        private static readonly Dictionary<HandleType, string> s_nameByHandleType = Enum.GetValues(typeof(HandleType))
            .Cast<HandleType>()
            .ToDictionary(o => o, o => $"{o} handle");

        public HandleRoot(ulong addr, ulong obj, ClrType type, HandleType handleType, GCRootKind kind, ClrAppDomain domain)
        {
            Address = addr;
            Object = obj;
            Kind = kind;
            Type = type;
            HandleType = handleType;
            AppDomain = domain;
        }

        public override ClrAppDomain AppDomain { get; }
        public override bool IsPinned => Kind == GCRootKind.Pinning || Kind == GCRootKind.AsyncPinning;
        public override GCRootKind Kind { get; }
        public override string Name => s_nameByHandleType[HandleType];
        public override ClrType Type { get; }
        public HandleType HandleType { get; }
    }
}