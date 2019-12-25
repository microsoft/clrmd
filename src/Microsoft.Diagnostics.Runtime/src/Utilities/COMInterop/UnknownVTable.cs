// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// The basic VTable for an IUnknown interface.
    /// </summary>
    public struct IUnknownVTable
    {
        public IntPtr QueryInterface;
        public IntPtr AddRef;
        public IntPtr Release;
    }
}