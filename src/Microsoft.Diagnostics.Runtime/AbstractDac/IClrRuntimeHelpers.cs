// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IClrRuntimeHelpers : IDisposable
    {
        // Heap
        IClrHeapHelpers GetHeapHelpers();

        // Threads
        IClrThreadHelpers ThreadHelpers { get; }
        IEnumerable<ClrThreadInfo> EnumerateThreads();

        // AppDomains
        IEnumerable<AppDomainInfo> EnumerateAppDomains();

        // Modules
        IClrModuleHelpers? ModuleHelpers { get; }
        IEnumerable<ulong> GetModuleList(ulong appDomain);
        ClrModuleInfo GetModuleInfo(ulong module);

        // Methods
        ulong GetMethodHandleContainingType(ulong methodDesc);
        ulong GetMethodHandleByInstructionPointer(ulong ip);

        // HandleTable
        IEnumerable<ClrHandleInfo> EnumerateHandles();

        // JIT
        IEnumerable<ClrJitManager> EnumerateClrJitManagers();
        string? GetJitHelperFunctionName(ulong address);

        // ThreadPool
        ClrThreadPool? GetThreadPool();

        // Native Heaps
        IClrNativeHeapHelpers? NativeHeapHelpers { get; }
        IEnumerable<ClrNativeHeapInfo> EnumerateGCFreeRegions();
        IEnumerable<ClrNativeHeapInfo> EnumerateHandleTableRegions();
        IEnumerable<ClrNativeHeapInfo> EnumerateGCBookkeepingRegions();
        IEnumerable<ClrSyncBlockCleanupData> EnumerateSyncBlockCleanupData();
        IEnumerable<ClrRcwCleanupData> EnumerateRcwCleanupData();

        // Helpers
        void Flush();
    }

    internal struct AppDomainInfo
    {
        public ulong Address { get; set; }
        public AppDomainKind Kind { get; set; }

        public int Id { get; set; }
        public string Name { get; set; }

        public string? ConfigFile { get; set; }
        public string? ApplicationBase { get; set; }

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
}