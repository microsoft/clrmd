// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// The base interface for building an abstract ClrRuntime.
    /// </summary>
    internal interface IAbstractRuntime
    {
        // Threads
        IAbstractThreadProvider ThreadHelpers { get; }
        IEnumerable<ClrThreadInfo> EnumerateThreads();

        // AppDomains
        IEnumerable<AppDomainInfo> EnumerateAppDomains();

        // Modules
        IAbstractModuleProvider? ModuleHelpers { get; }
        IEnumerable<ulong> GetModuleList(ulong appDomain);
        ClrModuleInfo GetModuleInfo(ulong module);

        // Methods
        ulong GetMethodHandleContainingType(ulong methodDesc);
        ulong GetMethodHandleByInstructionPointer(ulong ip);

        // HandleTable
        IEnumerable<ClrHandleInfo> EnumerateHandles();

        // JIT
        IEnumerable<JitManagerInfo> EnumerateClrJitManagers();
        string? GetJitHelperFunctionName(ulong address);

        // ThreadPool
        IAbstractThreadPoolProvider? LegacyThreadPoolHelpers { get; }

        // Native Heaps
        IAbstractNativeHeapProvider? NativeHeapHelpers { get; }
        IEnumerable<ClrNativeHeapInfo> EnumerateGCFreeRegions();
        IEnumerable<ClrNativeHeapInfo> EnumerateHandleTableRegions();
        IEnumerable<ClrNativeHeapInfo> EnumerateGCBookkeepingRegions();
        IEnumerable<ClrSyncBlockCleanupData> EnumerateSyncBlockCleanupData();

        // COM
        IEnumerable<ClrRcwCleanupData> EnumerateRcwCleanupData();
        bool GetCcwInfo(ulong obj, out CcwInfo info);
        bool GetRcwInfo(ulong obj, out RcwInfo info);

        // Helpers
        void Flush();
    }

    internal struct GCState
    {
        public GCKind Kind { get; set; }
        public bool AreGCStructuresValid { get; set; }
        public int HeapCount { get; set; }
        public int MaxGeneration { get; set; }
        public ulong StringMethodTable { get; set; }
        public ulong ObjectMethodTable { get; set; }
        public ulong ExceptionMethodTable { get; set; }
        public ulong FreeMethodTable { get; set; }
    }

    internal enum GCKind
    {
        Unknown,
        Workstation,
        Server,
    }

    internal struct RcwInfo
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

    internal struct CcwInfo
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

    internal struct ComInterfaceEntry
    {
        public ulong MethodTable { get; set; }
        public ulong InterfacePointer { get; set; }
    }

    /// <summary>
    /// Information about a single app domain.
    /// </summary>
    internal struct AppDomainInfo
    {
        /// <summary>
        /// The address of coreclr!AppDomain
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// The kind of AppDomain.
        /// </summary>
        public AppDomainKind Kind { get; set; }

        /// <summary>
        /// The AppDomain's Id.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The name of the AppDomain.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The ConfigFile associated with this AppDomain (or null if there isn't one).
        /// </summary>
        public string? ConfigFile { get; set; }

        /// <summary>
        /// The path associated with this AppDomain (or null if not available in this runtime).
        /// </summary>
        public string? ApplicationBase { get; set; }

        /// <summary>
        /// The LoaderAllocator pointer for this AppDomain.
        /// </summary>
        public ulong LoaderAllocator { get; set; }
    }

    internal enum AppDomainKind
    {
        Normal,
        System,
        Shared,
    }

    /// <summary>
    /// Information about a ClrHandle
    /// </summary>
    internal struct ClrHandleInfo
    {
        /// <summary>
        /// The address of the handle.  AKA the handle itself.
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// The object this handle points to.
        /// </summary>
        public ulong Object { get; set; }

        /// <summary>
        /// The kind of handle.
        /// </summary>
        public ClrHandleKind Kind { get; set; }

        /// <summary>
        /// The AppDomain this handle lives in.
        /// </summary>
        public ulong AppDomain { get; set; }

        /// <summary>
        /// The dependent handle target, only valid for Kind == Dependent.
        /// </summary>
        public ulong DependentTarget { get; set; }

        /// <summary>
        /// The RefCount of a reference count handle.  Only valid for Kind == Dependent.
        /// </summary>
        public uint RefCount { get; set; }
    }

    /// <summary>
    /// Information about a single JitManager.
    /// </summary>
    internal struct JitManagerInfo
    {
        /// <summary>
        /// The address of the JitManager.
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// The kind of code heap this JitManager contains.
        /// </summary>
        public CodeHeapKind Kind { get; set; }

        /// <summary>
        /// The location of the heap list.
        /// </summary>
        public ulong HeapList { get; set; }
    }
}