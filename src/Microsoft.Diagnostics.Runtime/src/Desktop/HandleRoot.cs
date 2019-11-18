// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class HandleRoot : ClrRoot
    {
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

        public override string Name => HandleType switch
        {
            HandleType.WeakShort => "weak short handle",
            HandleType.WeakLong => "weak long handle",
            HandleType.Strong => "strong handle",
            HandleType.Pinned => "pinned handle",
            HandleType.RefCount => "ref counted handle",
            HandleType.Dependent => "dependent handle",
            HandleType.AsyncPinned => "async pinned handle",
            HandleType.SizedRef => "sized ref handle",
            _ => throw new NotImplementedException()
        };

        public override ClrType Type { get; }
        public HandleType HandleType { get; }
    }
}