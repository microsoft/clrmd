// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{

    /// <summary>
    /// Represents a single runtime in a target process or crash dump.  This serves as the primary
    /// entry point for getting diagnostic information.
    /// </summary>
    public abstract class ClrRuntime
    {
        /// <summary>
        /// In .NET native crash dumps, we have a list of serialized exceptions objects. This property expose them as ClrException objects.
        /// </summary>
        public abstract IEnumerable<ClrException> EnumerateSerializedExceptions();

        /// <summary>
        /// Used for internal purposes.
        /// </summary>
        public DacLibrary DacLibrary { get; protected set; }

        /// <summary>
        /// The ClrInfo of the current runtime.
        /// </summary>
        public ClrInfo ClrInfo { get; protected set; }

        /// <summary>
        /// Returns the DataTarget associated with this runtime.
        /// </summary>
        public abstract DataTarget DataTarget { get; }

        /// <summary>
        /// Whether or not the process is running in server GC mode or not.
        /// </summary>
        public bool ServerGC { get; protected set; }

        /// <summary>
        /// Enumerates the OS thread ID of GC threads in the runtime.  
        /// </summary>
        public abstract IEnumerable<int> EnumerateGCThreads();

        /// <summary>
        /// The number of logical GC heaps in the process.  This is always 1 for a workstation
        /// GC, and usually it's the number of logical processors in a server GC application.
        /// </summary>
        public int HeapCount { get; protected set; }

        /// <summary>
        /// Returns the pointer size of the target process.
        /// </summary>
        abstract public int PointerSize { get; }

        /// <summary>
        /// Enumerates the list of appdomains in the process.  Note the System appdomain and Shared
        /// AppDomain are omitted.
        /// </summary>
        abstract public IList<ClrAppDomain> AppDomains { get; }

        /// <summary>
        /// Give access to the System AppDomain
        /// </summary>
        abstract public ClrAppDomain SystemDomain { get; }

        /// <summary>
        /// Give access to the Shared AppDomain
        /// </summary>
        abstract public ClrAppDomain SharedDomain { get; }

        /// <summary>
        /// Enumerates all managed threads in the process.  Only threads which have previously run managed
        /// code will be enumerated.
        /// </summary>
        abstract public IList<ClrThread> Threads { get; }

        /// <summary>
        /// Enumerates all objects currently on the finalizer queue.  (Not finalizable objects, but objects
        /// which have been collected and will be imminently finalized.)
        /// </summary>
        abstract public IEnumerable<ulong> EnumerateFinalizerQueueObjectAddresses();

        /// <summary>
        /// Returns a ClrMethod by its internal runtime handle (on desktop CLR this is a MethodDesc).
        /// </summary>
        /// <param name="methodHandle">The method handle (MethodDesc) to look up.</param>
        /// <returns>The ClrMethod for the given method handle, or null if no method was found.</returns>
        abstract public ClrMethod GetMethodByHandle(ulong methodHandle);

        /// <summary>
        /// Returns the CCW data associated with the given address.  This is used when looking at stowed
        /// exceptions in CLR.
        /// </summary>
        /// <param name="addr">The address of the CCW obtained from stowed exception data.</param>
        /// <returns>The CcwData describing the given CCW, or null.</returns>
        public abstract CcwData GetCcwDataByAddress(ulong addr);


        /// <summary>
        /// Read data out of the target process.
        /// </summary>
        /// <param name="address">The address to start the read from.</param>
        /// <param name="buffer">The buffer to write memory to.</param>
        /// <param name="bytesRequested">How many bytes to read (must be less than/equal to buffer.Length)</param>
        /// <param name="bytesRead">The number of bytes actually read out of the process.  This will be less than
        /// bytes requested if the request falls off the end of an allocation.</param>
        /// <returns>False if the memory is not readable (free or no read permission), true if *some* memory was read.</returns>
        abstract public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead);


        /// <summary>
        /// Reads a pointer value out of the target process.  This function reads only the target's pointer size,
        /// so if this is used on an x86 target, only 4 bytes is read and written to val.
        /// </summary>
        /// <param name="address">The address to read from.</param>
        /// <param name="value">The value at that address.</param>
        /// <returns>True if the read was successful, false otherwise.</returns>
        abstract public bool ReadPointer(ulong address, out ulong value);

        /// <summary>
        /// Enumerates a list of GC handles currently in the process.  Note that this list may be incomplete
        /// depending on the state of the process when we attempt to walk the handle table.
        /// </summary>
        /// <returns>The list of GC handles in the process, NULL on catastrophic error.</returns>
        public abstract IEnumerable<ClrHandle> EnumerateHandles();

        /// <summary>
        /// Gets the GC heap of the process.
        /// </summary>
        [Obsolete("Use the Heap property instead.")]
        abstract public ClrHeap GetHeap();

        /// <summary>
        /// Gets the GC heap of the process.
        /// </summary>
        abstract public ClrHeap Heap { get; }

        /// <summary>
        /// Returns data on the CLR thread pool for this runtime.
        /// </summary>
        virtual public ClrThreadPool ThreadPool { get { throw new NotImplementedException(); } }

        /// <summary>
        /// Returns data on the CLR thread pool for this runtime.
        /// </summary>
        [Obsolete("Use ThreadPool property instead.")]
        virtual public ClrThreadPool GetThreadPool() { throw new NotImplementedException(); }

        /// <summary>
        /// Enumerates regions of memory which CLR has allocated with a description of what data
        /// resides at that location.  Note that this does not return every chunk of address space
        /// that CLR allocates.
        /// </summary>
        /// <returns>An enumeration of memory regions in the process.</returns>
        abstract public IEnumerable<ClrMemoryRegion> EnumerateMemoryRegions();

        /// <summary>
        /// Attempts to get a ClrMethod for the given instruction pointer.  This will return NULL if the
        /// given instruction pointer is not within any managed method.
        /// </summary>
        abstract public ClrMethod GetMethodByAddress(ulong ip);


        /// <summary>
        /// A list of all modules loaded into the process.
        /// </summary>
        public abstract IList<ClrModule> Modules { get; }

        /// <summary>
        /// Flushes the dac cache.  This function MUST be called any time you expect to call the same function
        /// but expect different results.  For example, after walking the heap, you need to call Flush before
        /// attempting to walk the heap again.  After calling this function, you must discard ALL ClrMD objects
        /// you have cached other than DataTarget and ClrRuntime and re-request the objects and data you need.
        /// (E.G. if you want to use the ClrHeap object after calling flush, you must call ClrRuntime.GetHeap
        /// again after Flush to get a new instance.)
        /// </summary>
        abstract public void Flush();

        /// <summary>
        /// Delegate called when the RuntimeFlushed event is triggered.
        /// </summary>
        /// <param name="runtime">Which runtime was flushed.</param>
        public delegate void RuntimeFlushedCallback(ClrRuntime runtime);

        /// <summary>
        /// Called whenever the runtime is being flushed.  All references to ClrMD objects need to be released
        /// and not used for the given runtime after this call.
        /// </summary>
        public event RuntimeFlushedCallback RuntimeFlushed;

        /// <summary>
        /// Call when flushing the runtime.
        /// </summary>
        protected void OnRuntimeFlushed()
        {
            RuntimeFlushed?.Invoke(this);
        }

        /// <summary>
        /// Whether or not the runtime has component method tables for arrays.  This is an extra field in
        /// array objects on the heap, which was removed in v4.6 of desktop clr.
        /// </summary>
        internal bool HasArrayComponentMethodTables
        {
            get
            {
                if (ClrInfo.Flavor == ClrFlavor.Desktop)
                {
                    VersionInfo version = ClrInfo.Version;
                    if (version.Major > 4)
                        return false;

                    if (version.Major == 4 && version.Minor >= 6)
                        return false;
                }
                else if (ClrInfo.Flavor == ClrFlavor.Core)
                {
                    return false;
                }

                return true;
            }
        }

        internal static bool IsPrimitive(ClrElementType cet)
        {
            return cet >= ClrElementType.Boolean && cet <= ClrElementType.Double
                || cet == ClrElementType.NativeInt || cet == ClrElementType.NativeUInt
                || cet == ClrElementType.Pointer || cet == ClrElementType.FunctionPointer;
        }

        internal static bool IsValueClass(ClrElementType cet)
        {
            return cet == ClrElementType.Struct;
        }

        internal static bool IsObjectReference(ClrElementType cet)
        {
            return cet == ClrElementType.String || cet == ClrElementType.Class
                || cet == ClrElementType.Array || cet == ClrElementType.SZArray
                || cet == ClrElementType.Object;
        }

        internal static Type GetTypeForElementType(ClrElementType type)
        {
            switch (type)
            {
                case ClrElementType.Boolean:
                    return typeof(bool);

                case ClrElementType.Char:
                    return typeof(char);

                case ClrElementType.Double:
                    return typeof(double);

                case ClrElementType.Float:
                    return typeof(float);

                case ClrElementType.Pointer:
                case ClrElementType.NativeInt:
                case ClrElementType.FunctionPointer:
                    return typeof(IntPtr);

                case ClrElementType.NativeUInt:
                    return typeof(UIntPtr);

                case ClrElementType.Int16:
                    return typeof(short);

                case ClrElementType.Int32:
                    return typeof(int);

                case ClrElementType.Int64:
                    return typeof(long);

                case ClrElementType.Int8:
                    return typeof(sbyte);

                case ClrElementType.UInt16:
                    return typeof(ushort);

                case ClrElementType.UInt32:
                    return typeof(uint);

                case ClrElementType.UInt64:
                    return typeof(ulong);

                case ClrElementType.UInt8:
                    return typeof(byte);

                default:
                    return null;
            }
        }
    }
}
