// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Diagnostics.Runtime.Utilities;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A ClrHeap is a abstraction for the whole GC Heap.   Subclasses allow you to implement this for 
    /// a particular kind of heap (whether live,
    /// </summary>
    public abstract class ClrHeap
    {
        /// <summary>
        /// And the ability to take an address of an object and fetch its type (The type alows further exploration)
        /// </summary>
        abstract public ClrType GetObjectType(Address objRef);

        /// <summary>
        /// Returns a  wrapper around a System.Exception object (or one of its subclasses).
        /// </summary>
        virtual public ClrException GetExceptionObject(Address objRef) { return null; }

        /// <summary>
        /// Returns the runtime associated with this heap.
        /// </summary>
        virtual public ClrRuntime GetRuntime() { return null; }

        /// <summary>
        /// A heap is has a list of contiguous memory regions called segments.  This list is returned in order of
        /// of increasing object addresses.  
        /// </summary>
        abstract public IList<ClrSegment> Segments { get; }

        /// <summary>
        /// Enumerate the roots of the process.  (That is, all objects which keep other objects alive.)
        /// Equivalent to EnumerateRoots(true).
        /// </summary>
        abstract public IEnumerable<ClrRoot> EnumerateRoots();

        /// <summary>
        /// Returns a type by its index.
        /// </summary>
        /// <param name="index">The type to get.</param>
        /// <returns>The ClrType of that index.</returns>
        [Obsolete]
        abstract public ClrType GetTypeByIndex(int index);

        /// <summary>
        /// Looks up a type by name.
        /// </summary>
        /// <param name="name">The name of the type.</param>
        /// <returns>The ClrType matching 'name', null if the type was not found, and undefined if more than one
        /// type shares the same name.</returns>
        abstract public ClrType GetTypeByName(string name);

        /// <summary>
        /// Retrieves the given type by its TypeHandle/ComponentTypeHandle pair.
        /// </summary>
        /// <param name="typeHandle">The ClrType.TypeHandle for the requested type.</param>
        /// <param name="componentTypeHandle">The ClrType.ComponentTypeHandle for the requested type.</param>
        /// <returns>A ClrType object, or null if no such type exists.</returns>
        abstract public ClrType GetTypeByTypeHandle(ulong typeHandle, ulong componentTypeHandle);

        /// <summary>
        /// Retrieves the given type by its TypeHandle/ComponentTypeHandle pair.  Note this is only valid if
        /// the given ClrType.ComponentTypeHandle is 0.
        /// </summary>
        /// <param name="typeHandle">The ClrType.TypeHandle for the requested type.</param>
        /// <returns>A ClrType object, or null if no such type exists.</returns>
        virtual public ClrType GetTypeByTypeHandle(ulong typeHandle)
        {
            return GetTypeByTypeHandle(typeHandle, 0);
        }

        /// <summary>
        /// Returns the max index.
        /// </summary>
        abstract public int TypeIndexLimit { get; }

        /// <summary>
        /// Enumerate the roots in the process.
        /// </summary>
        /// <param name="enumerateStatics">True if we should enumerate static variables.  Enumerating with statics 
        /// can take much longer than enumerating without them.  Additionally these will be be "double reported",
        /// since all static variables are pinned by handles on the HandleTable (which is also enumerated with 
        /// EnumerateRoots).  You would want to enumerate statics with roots if you care about what exact statics
        /// root what objects, but not if you care about performance.</param>
        abstract public IEnumerable<ClrRoot> EnumerateRoots(bool enumerateStatics);

        /// <summary>
        /// Enumerates all types in the runtime.
        /// </summary>
        /// <returns>An enumeration of all types in the target process.  May return null if it's unsupported for
        /// that version of CLR.</returns>
        virtual public IEnumerable<ClrType> EnumerateTypes() { return null; }

        /// <summary>
        /// Enumerates all finalizable objects on the heap.
        /// </summary>
        virtual public IEnumerable<Address> EnumerateFinalizableObjects() { throw new NotImplementedException(); }

        /// <summary>
        /// Enumerates all managed locks in the process.  That is anything using System.Monitor either explictly
        /// or implicitly through "lock (obj)".  This is roughly equivalent to combining SOS's !syncblk command
        /// with !dumpheap -thinlock.
        /// </summary>
        virtual public IEnumerable<BlockingObject> EnumerateBlockingObjects() { throw new NotImplementedException(); }

        /// <summary>
        /// Returns true if the GC heap is in a consistent state for heap enumeration.  This will return false
        /// if the process was stopped in the middle of a GC, which can cause the GC heap to be unwalkable.
        /// Note, you may still attempt to walk the heap if this function returns false, but you will likely
        /// only be able to partially walk each segment.
        /// </summary>
        abstract public bool CanWalkHeap { get; }

        /// <summary>
        /// Enumerates all objects on the heap.  This is equivalent to enumerating all segments then walking
        /// each object with ClrSegment.FirstObject, ClrSegment.NextObject, but in a simple enumerator
        /// for easier use in linq queries.
        /// </summary>
        /// <returns>An enumerator for all objects on the heap.</returns>
        abstract public IEnumerable<Address> EnumerateObjects();

        /// <summary>
        /// TotalHeapSize is defined as the sum of the length of all segments.  
        /// </summary>
        abstract public ulong TotalHeapSize { get; }

        /// <summary>
        /// Get the size by generation 0, 1, 2, 3.  The large object heap is Gen 3 here. 
        /// The sum of all of these should add up to the TotalHeapSize.  
        /// </summary>
        abstract public ulong GetSizeByGen(int gen);

        /// <summary>
        /// Returns the generation of an object.
        /// </summary>
        public int GetGeneration(Address obj)
        {
            ClrSegment seg = GetSegmentByAddress(obj);
            if (seg == null)
                return -1;

            return seg.GetGeneration(obj);
        }

        /// <summary>
        /// Returns the object after this one on the segment.
        /// </summary>
        /// <param name="obj">The object to find the next for.</param>
        /// <returns>The next object on the segment, or 0 if the object was the last one on the segment.</returns>
        public virtual ulong NextObject(ulong obj)
        {
            ClrSegment seg = GetSegmentByAddress(obj);
            if (seg == null)
                return 0;

            return seg.NextObject(obj);
        }

        /// <summary>
        /// Returns the GC segment for the given object.
        /// </summary>
        public abstract ClrSegment GetSegmentByAddress(Address objRef);

        /// <summary>
        /// Returns true if the given address resides somewhere on the managed heap.
        /// </summary>
        public bool IsInHeap(Address address) { return GetSegmentByAddress(address) != null; }

        /// <summary>
        /// Pointer size of on the machine (4 or 8 bytes).  
        /// </summary>
        public abstract int PointerSize { get; }

        /// <summary>
        /// Returns a string representation of this heap, including the size and number of segments.
        /// </summary>
        /// <returns>The string representation of this heap.</returns>
        public override string ToString()
        {
            var sizeMB = TotalHeapSize / 1000000.0;
            int segCount = Segments != null ? Segments.Count : 0;
            return string.Format("ClrHeap {0}mb {1} segments", sizeMB, segCount);
        }

        /// <summary>
        /// Read 'count' bytes from the ClrHeap at 'address' placing it in 'buffer' starting at offset 'offset'
        /// </summary>
        virtual public int ReadMemory(Address address, byte[] buffer, int offset, int count) { return 0; }

        /// <summary>
        /// Attempts to efficiently read a pointer from memory.  This acts exactly like ClrRuntime.ReadPointer, but
        /// there is a greater chance you will hit a chache for a more efficient memory read.
        /// </summary>
        /// <param name="addr">The address to read.</param>
        /// <param name="value">The pointer value.</param>
        /// <returns>True if we successfully read the value, false if addr is not mapped into the process space.</returns>
        public abstract bool ReadPointer(Address addr, out Address value);
    }

    /// <summary>
    /// Represents a managed lock within the runtime.
    /// </summary>
    public abstract class BlockingObject
    {
        /// <summary>
        /// The object associated with the lock.
        /// </summary>
        abstract public Address Object { get; }

        /// <summary>
        /// Whether or not the object is currently locked.
        /// </summary>
        abstract public bool Taken { get; }

        /// <summary>
        /// The recursion count of the lock (only valid if Locked is true).
        /// </summary>
        abstract public int RecursionCount { get; }

        /// <summary>
        /// The thread which currently owns the lock.  This is only valid if Taken is true and
        /// only valid if HasSingleOwner is true.
        /// </summary>
        abstract public ClrThread Owner { get; }

        /// <summary>
        /// Returns true if this lock has only one owner.  Returns false if this lock
        /// may have multiple owners (for example, readers on a RW lock).
        /// </summary>
        abstract public bool HasSingleOwner { get; }

        /// <summary>
        /// Returns the list of owners for this object.
        /// </summary>
        abstract public IList<ClrThread> Owners { get; }

        /// <summary>
        /// Returns the list of threads waiting on this object.
        /// </summary>
        abstract public IList<ClrThread> Waiters { get; }

        /// <summary>
        /// The reason why it's blocking.
        /// </summary>
        abstract public BlockingReason Reason { get; internal set; }
    }

    /// <summary>
    /// The type of GCRoot that a ClrRoot represnts.
    /// </summary>
    public enum GCRootKind
    {
        /// <summary>
        /// The root is a static variable.
        /// </summary>
        StaticVar,

        /// <summary>
        /// The root is a thread static.
        /// </summary>
        ThreadStaticVar,

        /// <summary>
        /// The root is a local variable (or compiler generated temporary variable).
        /// </summary>
        LocalVar,

        /// <summary>
        /// The root is a strong handle.
        /// </summary>
        Strong,

        /// <summary>
        /// The root is a weak handle.
        /// </summary>
        Weak,

        /// <summary>
        /// The root is a strong pinning handle.
        /// </summary>
        Pinning,

        /// <summary>
        /// The root comes from the finalizer queue.
        /// </summary>
        Finalizer,

        /// <summary>
        /// The root is an async IO (strong) pinning handle.
        /// </summary>
        AsyncPinning,

        /// <summary>
        /// The max value of this enum.
        /// </summary>
        Max = AsyncPinning
    }

    /// <summary>
    /// Represents a root in the target process.  A root is the base entry to the GC's mark and sweep algorithm.
    /// </summary>
    public abstract class ClrRoot
    {
        /// <summary>
        /// A GC Root also has a Kind, which says if it is a strong or weak root
        /// </summary>
        abstract public GCRootKind Kind { get; }

        /// <summary>
        /// The name of the root. 
        /// </summary>
        virtual public string Name { get { return ""; } }

        /// <summary>
        /// The type of the object this root points to.  That is, ClrHeap.GetObjectType(ClrRoot.Object).
        /// </summary>
        abstract public ClrType Type { get; }

        /// <summary>
        /// The object on the GC heap that this root keeps alive.
        /// </summary>
        virtual public Address Object { get; protected set; }

        /// <summary>
        /// The address of the root in the target process.
        /// </summary>
        virtual public Address Address { get; protected set; }

        /// <summary>
        /// If the root can be identified as belonging to a particular AppDomain this is that AppDomain.
        /// It an be null if there is no AppDomain associated with the root.  
        /// </summary>
        virtual public ClrAppDomain AppDomain { get { return null; } }

        /// <summary>
        /// If the root has a thread associated with it, this will return that thread.
        /// </summary>
        virtual public ClrThread Thread { get { return null; } }

        /// <summary>
        /// Returns true if Object is an "interior" pointer.  This means that the pointer may actually
        /// point inside an object instead of to the start of the object.
        /// </summary>
        virtual public bool IsInterior { get { return false; } }

        /// <summary>
        /// Returns true if the root "pins" the object, preventing the GC from relocating it.
        /// </summary>
        virtual public bool IsPinned { get { return false; } }

        /// <summary>
        /// Unfortunately some versions of the APIs we consume do not give us perfect information.  If
        /// this property is true it means we used a heuristic to find the value, and it might not
        /// actually be considered a root by the GC.
        /// </summary>
        virtual public bool IsPossibleFalsePositive { get { return false; } }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return string.Format("GCRoot {0:X8}->{1:X8} {2}", Address, Object, Name);
        }
    }

    /// <summary>
    /// A GCHeapSegment represents a contiguous region of memory that is devoted to the GC heap. 
    /// Segments.  It has a start and end and knows what heap it belongs to.   Segments can
    /// optional have regions for Gen 0, 1 and 2, and Large properties.  
    /// </summary>
    public abstract class ClrSegment
    {
        /// <summary>
        /// The start address of the segment.  All objects in this segment fall within Start &lt;= object &lt; End.
        /// </summary>
        abstract public Address Start { get; }

        /// <summary>
        /// The end address of the segment.  All objects in this segment fall within Start &lt;= object &lt; End.
        /// </summary>
        abstract public Address End { get; }

        /// <summary>
        /// The number of bytes in the segment.
        /// </summary>
        public ulong Length { get { return (End - Start); } }

        /// <summary>
        /// The GC heap associated with this segment.  There's only one GCHeap per process, so this is
        /// only a convenience method to keep from having to pass the heap along with a segment.
        /// </summary>
        abstract public ClrHeap Heap { get; }

        /// <summary>
        /// The processor that this heap is affinitized with.  In a workstation GC, there is no processor
        /// affinity (and the return value of this property is undefined).  In a server GC each segment
        /// has a logical processor in the PC associated with it.  This property returns that logical
        /// processor number (starting at 0).
        /// </summary>
        abstract public int ProcessorAffinity { get; }

        /// <summary>
        /// The address of the end of memory reserved for the segment, but not committed.
        /// </summary>
        [Obsolete("Use ReservedEnd instead", false)]
        virtual public Address Reserved { get { return ReservedEnd; } }

        /// <summary>
        /// The address of the end of memory committed for the segment (this may be longer than Length).
        /// </summary>
        [Obsolete("Use CommittedEnd instead", false)]
        virtual public Address Committed { get { return CommittedEnd; } }

        /// <summary>
        /// The address of the end of memory reserved for the segment, but not committed.
        /// </summary>
        virtual public Address ReservedEnd { get { return 0; } }

        /// <summary>
        /// The address of the end of memory committed for the segment (this may be longer than Length).
        /// </summary>
        virtual public Address CommittedEnd { get { return 0; } }

        /// <summary>
        /// If it is possible to move from one object to the 'next' object in the segment. 
        /// Then FirstObject returns the first object in the heap (or null if it is not
        /// possible to walk the heap.
        /// </summary>
        virtual public Address FirstObject { get { return 0; } }

        /// <summary>
        /// Given an object on the segment, return the 'next' object in the segment.  Returns
        /// 0 when there are no more objects.   (Or enumeration is not possible)  
        /// </summary>
        virtual public Address NextObject(Address objRef) { return 0; }

        /// <summary>
        /// Returns true if this is a segment for the Large Object Heap.  False otherwise.
        /// Large objects (greater than 85,000 bytes in size), are stored in their own segments and
        /// only collected on full (gen 2) collections. 
        /// </summary>
        virtual public bool IsLarge { get { return false; } }

        /// <summary>
        /// Obsolete
        /// </summary>
        [Obsolete("Use IsLarge instead.")]
        virtual public bool Large { get { return IsLarge; } }

        /// <summary>
        /// Returns true if this segment is the ephemeral segment (meaning it contains gen0 and gen1
        /// objects).
        /// </summary>
        virtual public bool IsEphemeral { get { return false; } }

        /// <summary>
        /// Obsolete.
        /// </summary>
        [Obsolete("Use IsEphemeral instead.")]
        virtual public bool Ephemeral { get { return IsEphemeral; } }

        /// <summary>
        /// Ephemeral heap sements have geneation 0 and 1 in them.  Gen 1 is always above Gen 2 and
        /// Gen 0 is above Gen 1.  This property tell where Gen 0 start in memory.   Note that
        /// if this is not an Ephemeral segment, then this will return End (which makes Gen 0 empty
        /// for this segment)
        /// </summary>
        virtual public Address Gen0Start { get { return Start; } }

        /// <summary>
        /// The length of the gen0 portion of this segment.
        /// </summary>
        virtual public ulong Gen0Length { get { return Length; } }

        /// <summary>
        /// The start of the gen1 portion of this segment.
        /// </summary>
        virtual public Address Gen1Start { get { return End; } }

        /// <summary>
        /// The length of the gen1 portion of this segment.
        /// </summary>
        virtual public ulong Gen1Length { get { return 0; } }

        /// <summary>
        /// The start of the gen2 portion of this segment.
        /// </summary>
        virtual public Address Gen2Start { get { return End; } }

        /// <summary>
        /// The length of the gen2 portion of this segment.
        /// </summary>
        virtual public ulong Gen2Length { get { return 0; } }

        /// <summary>
        /// Enumerates all objects on the segment.
        /// </summary>
        abstract public IEnumerable<ulong> EnumerateObjects();

        /// <summary>
        /// Returns the generation of an object in this segment.
        /// </summary>
        /// <param name="obj">An object in this segment.</param>
        /// <returns>The generation of the given object if that object lies in this segment.  The return
        ///          value is undefined if the object does not lie in this segment.
        /// </returns>
        virtual public int GetGeneration(Address obj)
        {
            if (Gen0Start <= obj && obj < (Gen0Start + Gen0Length))
            {
                return 0;
            }

            if (Gen1Start <= obj && obj < (Gen1Start + Gen1Length))
            {
                return 1;
            }

            if (Gen2Start <= obj && obj < (Gen2Start + Gen2Length))
            {
                return 2;
            }

            return -1;
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return string.Format("HeapSegment {0:n2}mb [{1:X8}, {2:X8}]", Length / 1000000.0, Start, End);
        }
    }

    /// <summary>
    /// Every thread which is blocking on an object specifies why the object is waiting.
    /// </summary>
    public enum BlockingReason
    {
        /// <summary>
        /// Object is not locked.
        /// </summary>
        None,

        /// <summary>
        /// Not able to determine why the object is blocking.
        /// </summary>
        Unknown,

        /// <summary>
        /// The thread is waiting for a Mutex or Semaphore (such as Monitor.Enter, lock(obj), etc).
        /// </summary>
        Monitor,

        /// <summary>
        /// The thread is waiting for a mutex with Monitor.Wait.
        /// </summary>
        MonitorWait,

        /// <summary>
        /// The thread is waiting for an event (ManualResetEvent.WaitOne, AutoResetEvent.WaitOne).
        /// </summary>
        WaitOne,

        /// <summary>
        /// The thread is waiting in WaitHandle.WaitAll.
        /// </summary>
        WaitAll,

        /// <summary>
        /// The thread is waiting in WaitHandle.WaitAny.
        /// </summary>
        WaitAny,

        /// <summary>
        /// The thread is blocked on a call to Thread.Join.
        /// </summary>
        ThreadJoin,

        /// <summary>
        /// ReaderWriterLock, reader lock is taken.
        /// </summary>
        ReaderAcquired,


        /// <summary>
        /// ReaderWriterLock, writer lock is taken.
        /// </summary>
        WriterAcquired
    }
    /// <summary>
    /// Types of GC segments.
    /// </summary>
    public enum GCSegmentType
    {
        /// <summary>
        /// Ephemeral segments are the only segments to contain Gen0 and Gen1 objects.
        /// It may also contain Gen2 objects, but not always.  Objects are only allocated
        /// on the ephemeral segment.  There is one ephemeral segment per logical GC heap.
        /// It is important to not have too many pinned objects in the ephemeral segment,
        /// or you will run into a performance problem where the runtime runs too many GCs.
        /// </summary>
        Ephemeral,

        /// <summary>
        /// Regular GC segments only contain Gen2 objects.
        /// </summary>
        Regular,

        /// <summary>
        /// The large object heap contains objects greater than a certain threshold.  Large
        /// object segments are never compacted.  Large objects are directly allocated
        /// onto LargeObject segments, and all large objects are considered gen2.
        /// </summary>
        LargeObject
    }

    /// <summary>
    /// Defines the state of the thread from the runtime's perspective.
    /// </summary>
    public enum GcMode
    {
        /// <summary>
        /// In Cooperative mode the thread must cooperate before a GC may proceed.  This means when a GC
        /// starts, the runtime will attempt to suspend the thread at a safepoint but cannot immediately
        /// stop the thread until it synchronizes.
        /// </summary>
        Cooperative,
        /// <summary>
        /// In Preemptive mode the runtime is free to suspend the thread at any time for a GC to occur.
        /// </summary>
        Preemptive
    }

}