// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// Bit flags identifying which runtime subsystem emitted a stress log message.
    /// The values mirror the runtime's <c>loglf.h</c> facility bits exactly (the raw
    /// facility field from the message header is reinterpreted as these flags).
    /// </summary>
    [Flags]
    public enum StressLogFacility : uint
    {
        /// <summary>No facility.</summary>
        None              = 0x00000000,

        /// <summary>Garbage collector. (<c>LF_GC</c>)</summary>
        GC                = 0x00000001,

        /// <summary>GC information / detailed tracing. (<c>LF_GCINFO</c>)</summary>
        GCInfo            = 0x00000002,

        /// <summary>Stubs. (<c>LF_STUBS</c>)</summary>
        Stubs             = 0x00000004,

        /// <summary>Just-in-time compiler. (<c>LF_JIT</c>)</summary>
        Jit               = 0x00000008,

        /// <summary>Assembly/module loader. (<c>LF_LOADER</c>)</summary>
        Loader            = 0x00000010,

        /// <summary>Metadata. (<c>LF_METADATA</c>)</summary>
        Metadata          = 0x00000020,

        /// <summary>Thread synchronization. (<c>LF_SYNC</c>)</summary>
        Sync              = 0x00000040,

        /// <summary>EE memory subsystem. (<c>LF_EEMEM</c>)</summary>
        EEMem             = 0x00000080,

        /// <summary>Garbage collector allocation. (<c>LF_GCALLOC</c>)</summary>
        GCAlloc           = 0x00000100,

        /// <summary>Debugger interface. (<c>LF_CORDB</c>)</summary>
        Cordb             = 0x00000200,

        /// <summary>Class loader. (<c>LF_CLASSLOADER</c>)</summary>
        ClassLoader       = 0x00000400,

        /// <summary>Profiler interface. (<c>LF_CORPROF</c>)</summary>
        CorProf           = 0x00000800,

        /// <summary>Diagnostics port. (<c>LF_DIAGNOSTICS_PORT</c>)</summary>
        DiagnosticsPort   = 0x00001000,

        /// <summary>Debug allocation. (<c>LF_DBGALLOC</c>)</summary>
        DbgAlloc          = 0x00002000,

        /// <summary>Exception handling. (<c>LF_EH</c>)</summary>
        ExceptionHandling = 0x00004000,

        /// <summary>Edit and continue. (<c>LF_ENC</c>)</summary>
        EnC               = 0x00008000,

        /// <summary>Assertions. (<c>LF_ASSERT</c>)</summary>
        Assert            = 0x00010000,

        /// <summary>Verifier. (<c>LF_VERIFIER</c>)</summary>
        Verifier          = 0x00020000,

        /// <summary>Thread pool. (<c>LF_THREADPOOL</c>)</summary>
        ThreadPool        = 0x00040000,

        /// <summary>Garbage collector roots. (<c>LF_GCROOTS</c>)</summary>
        GCRoots           = 0x00080000,

        /// <summary>Interop. (<c>LF_INTEROP</c>)</summary>
        Interop           = 0x00100000,

        /// <summary>Marshaler. (<c>LF_MARSHALER</c>)</summary>
        Marshaler         = 0x00200000,

        /// <summary>Tiered compilation. (<c>LF_TIEREDCOMPILATION</c>)</summary>
        TieredCompilation = 0x00400000,

        /// <summary>Native image (ZAP). (<c>LF_ZAP</c>)</summary>
        Zap               = 0x00800000,

        /// <summary>Startup and shutdown. (<c>LF_STARTUP</c>)</summary>
        Startup           = 0x01000000,

        /// <summary>AppDomain. (<c>LF_APPDOMAIN</c>)</summary>
        AppDomain         = 0x02000000,

        /// <summary>Code sharing. (<c>LF_CODESHARING</c>)</summary>
        CodeSharing       = 0x04000000,

        /// <summary>Store. (<c>LF_STORE</c>)</summary>
        Store             = 0x08000000,

        /// <summary>Security. (<c>LF_SECURITY</c>)</summary>
        Security          = 0x10000000,

        /// <summary>Locks. (<c>LF_LOCKS</c>)</summary>
        Locks             = 0x20000000,

        /// <summary>Base class library. (<c>LF_BCL</c>)</summary>
        Bcl               = 0x40000000,

        /// <summary>Always logged regardless of facility filter. (<c>LF_ALWAYS</c>)</summary>
        Always            = 0x80000000,
    }
}
