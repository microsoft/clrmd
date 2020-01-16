// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdHandle : ClrHandle
    {
        public override ulong Address { get; }

        public override ClrObject Object { get; }

        public override ClrHandleKind HandleKind { get; }

        public override uint ReferenceCount => uint.MaxValue;

        public override ClrObject Dependent => default;

        public override ClrAppDomain AppDomain { get; }

        public ClrmdHandle(ClrAppDomain parent, ulong address, ClrObject obj, ClrHandleKind kind)
        {
            if (kind == ClrHandleKind.Dependent)
                throw new InvalidOperationException($"{nameof(ClrmdHandle)} cannot represent a dependent handle, use {nameof(ClrmdDependentHandle)} instead.");

            if (kind == ClrHandleKind.RefCounted)
                throw new InvalidOperationException($"{nameof(ClrmdHandle)} cannot represent a ref counted handle, use {nameof(ClrmdRefCountedHandle)} instead.");

            AppDomain = parent;
            Address = address;
            Object = obj;
            HandleKind = kind;
        }
    }
}
