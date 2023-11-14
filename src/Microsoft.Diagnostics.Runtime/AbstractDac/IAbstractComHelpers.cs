// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// Helpers for COM information.
    ///
    /// This interface is optional.
    ///
    /// This interface is not "stable" and may change even in minor or patch
    /// versions of ClrMD.
    /// </summary>
    public interface IAbstractComHelpers
    {
        IEnumerable<ClrRcwCleanupData> EnumerateRcwCleanupData();
        bool GetCcwInfo(ulong obj, out CcwInfo info);
        bool GetRcwInfo(ulong obj, out RcwInfo info);
    }

    public struct RcwInfo
    {
        public RcwInfo()
        {
        }

        public ulong Object { get; set; }
        public ulong Address { get; set; }
        public ulong IUnknown { get; set; }
        public ulong VTablePointer { get; set; }
        public int RefCount { get; set; }
        public ulong CreatorThread { get; set; }
        public bool IsDisconnected { get; set; }
        public ComInterfaceEntry[] Interfaces { get; set; } = Array.Empty<ComInterfaceEntry>();
    }

    public struct CcwInfo
    {
        public CcwInfo()
        {
        }

        public ulong Object { get; set; }
        public ulong Address { get; set; }
        public ulong IUnknown { get; set; }
        public ulong Handle { get; set; }
        public int RefCount { get; set; }
        public int JupiterRefCount { get; set; }
        public ComInterfaceEntry[] Interfaces { get; set; } = Array.Empty<ComInterfaceEntry>();
    }

    public struct ComInterfaceEntry
    {
        public ulong MethodTable { get; set; }
        public ulong InterfacePointer { get; set; }
    }
}