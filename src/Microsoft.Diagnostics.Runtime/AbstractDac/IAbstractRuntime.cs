// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// The base interface for building an abstract ClrRuntime.
    ///
    /// This interface is required.  We need to be able to at least
    /// enumerate AppDomains and modules to construct a ClrRuntime.
    /// If the target version of CLR does not support AppDomains,
    /// this API needs to produce a single "AppDomainInfo" to use
    /// as a pseudo-AppDomain.
    ///
    /// This interface is not "stable" and may change even in minor or patch
    /// versions of ClrMD.
    /// </summary>
    public interface IAbstractRuntime
    {
        IEnumerable<ClrThreadInfo> EnumerateThreads();
        IEnumerable<AppDomainInfo> EnumerateAppDomains();
        IEnumerable<ulong> GetModuleList(ulong appDomain);
        IEnumerable<ClrHandleInfo> EnumerateHandles();
        IEnumerable<JitManagerInfo> EnumerateClrJitManagers();
        string? GetJitHelperFunctionName(ulong address);
    }

    /// <summary>
    /// Information about a single coreclr!Thread.
    /// </summary>
    public struct ClrThreadInfo
    {
        /// <summary>
        /// The address of the underlying coreclr!Thread.
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// The AppDomain that this thread is currently running in.
        /// </summary>
        public ulong AppDomain { get; set; }

        /// <summary>
        /// The thread ID as the Operating System sees it.
        /// </summary>
        public uint OSThreadId { get; set; }

        /// <summary>
        /// The managed thread id.
        /// </summary>
        public int ManagedThreadId { get; set; }

        /// <summary>
        /// The lock count of the Thread, if available.
        /// </summary>
        public uint LockCount { get; set; }

        /// <summary>
        /// The Windows TEB, if available, 0 otherwise.
        /// </summary>
        public ulong Teb { get; set; }

        /// <summary>
        /// The base address range of the stack space for this thread.
        /// </summary>
        public ulong StackBase { get; set; }

        /// <summary>
        /// The limit of the stack space for this thread.
        /// </summary>
        public ulong StackLimit { get; set; }

        /// <summary>
        /// If an exception is in flight on this thread, a pointer directly to
        /// the exception object itself.
        /// </summary>
        public ulong ExceptionInFlight { get; set; }

        /// <summary>
        /// Whether this thread is a finalizer thread or not.
        /// </summary>
        public bool IsFinalizer { get; set; }

        /// <summary>
        /// Whether this thread is a GC thread or not.
        /// </summary>
        public bool IsGC { get; set; }

        /// <summary>
        /// The GCMode of this thread (cooperative, preemptive).
        /// </summary>
        public GCMode GCMode { get; set; }

        /// <summary>
        /// The state of this thread.
        /// </summary>
        public ClrThreadState State { get; set; }
    }


    /// <summary>
    /// Information about a single app domain.
    /// </summary>
    public struct AppDomainInfo
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

    public enum AppDomainKind
    {
        Normal,
        System,
        Shared,
    }

    /// <summary>
    /// Information about a ClrHandle
    /// </summary>
    public struct ClrHandleInfo
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
    public struct JitManagerInfo
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