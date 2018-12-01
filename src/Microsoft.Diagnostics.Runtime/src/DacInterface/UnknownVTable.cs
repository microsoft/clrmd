// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
#pragma warning disable CS0169
#pragma warning disable CS0649

    internal struct IUnknownVTable
    {
        public IntPtr QueryInterface;
        public IntPtr AddRef;
        public IntPtr Release;
    }
}