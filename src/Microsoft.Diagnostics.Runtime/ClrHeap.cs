// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime.Desktop;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A ClrHeap is a abstraction for the whole GC Heap.   Subclasses allow you to implement this for 
    /// a particular kind of heap (whether live,
    /// </summary>
    public abstract class ClrHeap
    {
        /// <summary>
        /// Obtains the type of an object at the given address.  Returns null if objRef does not point to
        /// a valid managed object.
        /// </summary>
        abstract public ClrType GetObjectType(ulong objRef);

        /// <summary>
        /// Returns whether this version of CLR has component MethodTables.  Component MethodTables were removed from
        /// desktop CLR in v4.6, and do not exist at all on .Net Native.  If this method returns false, all component
        /// MethodTables will be 0, and expected to be 0 when an argument to a function.
        /// </summary>
        virtual public bool HasComponentMethodTables { get { return true; } }

        /// <summary>
        /// Attempts to retrieve the MethodTable and component MethodTable from the given object.
        /// Note that this some ClrTypes cannot be uniquely determined by MethodTable alone.  In
        /// Desktop CLR (prior to v4.6), arrays of reference types all use the same MethodTable but
        /// also carry a second MethodTable (called the component MethodTable) to determine the
        /// array element types. Note this function has undefined behavior if you do not pass a
        /// valid object reference to it.
        /// </summary>
        /// <param name="obj">The object to get the MethodTable of.</param>
        /// <param name="methodTable">The MethodTable for the given object.</param>
        /// <param name="componentMethodTable">The component MethodTable of the given object.</param>
        /// <returns>True if methodTable was filled, false if we failed to read memory.</returns>
        abstract public bool TryGetMethodTable(ulong obj, out ulong methodTable, out ulong componentMethodTable);

        /// <summary>
        /// Attempts to retrieve the MethodTable from the given object.
        /// Note that this some ClrTypes cannot be uniquely determined by MethodTable alone.  In
        /// Desktop CLR, arrays of reference types all use the same MethodTable.  To uniquely
        /// determine an array of referneces you must also have its component type.
        /// Note this function has undefined behavior if you do not pass a valid object reference
        /// to it.
        /// </summary>
        /// <param name="obj">The object to get the MethodTablee of.</param>
        /// <returns>The MethodTable of the object, or 0 if the address could not be read from.</returns>
        abstract public ulong GetMethodTable(ulong obj);

        /// <summary>
        /// Retrieves the EEClass from the given MethodTable.  EEClasses do not exist on
        /// .Net Native. 
        /// </summary>
        /// <param name="methodTable">The MethodTable to get the EEClass from.</param>
        /// <returns>The EEClass for the given MethodTable, 0 if methodTable is invalid or
        /// does not exist.</returns>
        virtual public ulong GetEEClassByMethodTable(ulong methodTable) { return 0; }

        /// <summary>
        /// Retrieves the MethodTable associated with the given EEClass.
        /// </summary>
        /// <param name="eeclass">The eeclass to get the method table from.</param>
        /// <returns>The MethodTable for the given EEClass, 0 if eeclass is invalid
        /// or does not exist.</returns>
        virtual public ulong GetMethodTableByEEClass(ulong eeclass) { return 0; }

        /// <summary>
        /// Returns a  wrapper around a System.Exception object (or one of its subclasses).
        /// </summary>
        virtual public ClrException GetExceptionObject(ulong objRef) { return null; }

        /// <summary>
        /// Returns the runtime associated with this heap.
        /// </summary>
        abstract public ClrRuntime Runtime { get; }

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
        /// Sets the stackwalk policy for enumerating roots.  See ClrRootStackwalkPolicy for more information.
        /// Setting this field can invalidate the root cache.
        /// <see cref="ClrRootStackwalkPolicy"/>
        /// </summary>
        abstract public ClrRootStackwalkPolicy StackwalkPolicy { get; set; }

        /// <summary>
        /// Caches all relevant heap information into memory so future heap operations run faster and
        /// do not require touching the debuggee.
        /// </summary>
        /// <param name="cancelToken">A cancellation token to stop caching the heap.</param>
        virtual public void CacheHeap(CancellationToken cancelToken) => throw new NotImplementedException();

        /// <summary>
        /// Releases all cached object data to reclaim memory.
        /// </summary>
        virtual public void ClearHeapCache() => throw new NotImplementedException();

        /// <summary>
        /// Returns true if the heap is cached, false otherwise.
        /// </summary>
        virtual public bool IsHeapCached { get => false; }

        /// <summary>
        /// Returns whether the roots of the process are cached or not.
        /// </summary>
        abstract public bool AreRootsCached { get; }

        /// <summary>
        /// This method caches many roots so that subsequent calls to EnumerateRoots run faster.
        /// </summary>
        abstract public void CacheRoots(CancellationToken cancelToken);

        virtual internal void BuildDependentHandleMap(CancellationToken cancelToken) { }
        virtual internal IEnumerable<ClrRoot> EnumerateStackRoots() => throw new NotImplementedException();
        virtual internal IEnumerable<ClrHandle> EnumerateStrongHandles() => throw new NotImplementedException();

        /// <summary>
        /// This method clears any previously cached roots to reclaim memory.
        /// </summary>
        abstract public void ClearRootCache();

        /// <summary>
        /// Looks up a type by name.
        /// </summary>
        /// <param name="name">The name of the type.</param>
        /// <returns>The ClrType matching 'name', null if the type was not found, and undefined if more than one
        /// type shares the same name.</returns>
        abstract public ClrType GetTypeByName(string name);

        /// <summary>
        /// Retrieves the given type by its MethodTable/ComponentMethodTable pair.
        /// </summary>
        /// <param name="methodTable">The ClrType.MethodTable for the requested type.</param>
        /// <param name="componentMethodTable">The ClrType's component MethodTable for the requested type.</param>
        /// <returns>A ClrType object, or null if no such type exists.</returns>
        abstract public ClrType GetTypeByMethodTable(ulong methodTable, ulong componentMethodTable);

        /// <summary>
        /// Retrieves the given type by its MethodTable/ComponentMethodTable pair.  Note this is only valid if
        /// the given type's component MethodTable is 0.
        /// </summary>
        /// <param name="methodTable">The ClrType.MethodTable for the requested type.</param>
        /// <returns>A ClrType object, or null if no such type exists.</returns>
        virtual public ClrType GetTypeByMethodTable(ulong methodTable)
        {
            return GetTypeByMethodTable(methodTable, 0);
        }

        /// <summary>
        /// Returns the ClrType representing free space on the GC heap.
        /// </summary>
        public abstract ClrType Free { get; }

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
        virtual public IEnumerable<ulong> EnumerateFinalizableObjectAddresses() { throw new NotImplementedException(); }

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
        abstract public IEnumerable<ulong> EnumerateObjectAddresses();

        /// <summary>
        /// Enumerates all objects on the heap.
        /// </summary>
        /// <returns>An enumerator for all objects on the heap.</returns>
        abstract public IEnumerable<ClrObject> EnumerateObjects();

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
        public int GetGeneration(ulong obj)
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
        public abstract ClrSegment GetSegmentByAddress(ulong objRef);

        /// <summary>
        /// Returns true if the given address resides somewhere on the managed heap.
        /// </summary>
        public bool IsInHeap(ulong address) { return GetSegmentByAddress(address) != null; }

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
        virtual public int ReadMemory(ulong address, byte[] buffer, int offset, int count) { return 0; }

        /// <summary>
        /// Attempts to efficiently read a pointer from memory.  This acts exactly like ClrRuntime.ReadPointer, but
        /// there is a greater chance you will hit a chache for a more efficient memory read.
        /// </summary>
        /// <param name="addr">The address to read.</param>
        /// <param name="value">The pointer value.</param>
        /// <returns>True if we successfully read the value, false if addr is not mapped into the process space.</returns>
        public abstract bool ReadPointer(ulong addr, out ulong value);

        internal abstract IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully);
        internal abstract void EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, Action<ulong, int> callback);

        /// <summary>
        /// This might be useful to be public, but we actually don't know the total number objects without walking the entire
        /// heap.  This property is only valid if we have cached the heap...which leads to a weird programatic interface (that
        /// this simply property would throw InvalidOperationException unless the heap is cached).  I'm leaving this internal
        /// until I am convinced there's a good way to surface this.
        /// </summary>
        internal virtual long TotalObjects { get => -1; }
    }

    /// <summary>
    /// This sets the policy for how ClrHeap walks the stack when enumerating roots.  There is a choice here because the 'Exact' stack walking
    /// gives the correct answer (without overreporting), but unfortunately is poorly implemented in CLR's debugging layer.
    /// This means it could take 10-30 minutes (!) to enumerate roots on crash dumps with 4000+ threads.
    /// </summary>
    public enum ClrRootStackwalkPolicy
    {
        /// <summary>
        /// The GCRoot class will attempt to select a policy for you based on the number of threads in the process.
        /// </summary>
        Automatic,

        /// <summary>
        /// Use real stack roots.  This is much slower than 'Fast', but provides no false-positives for more accurate
        /// results.  Note that this can actually take 10s of minutes to enumerate the roots themselves in the worst
        /// case scenario.
        /// </summary>
        Exact,

        /// <summary>
        /// Walks each pointer alighed address on all stacks and if it points to an object it treats that location
        /// as a real root.  This can over-report roots when a value is left on the stack, but the GC does not
        /// consider it a real root.
        /// </summary>
        Fast,

        /// <summary>
        /// Do not walk stack roots.
        /// </summary>
        SkipStack,
    }

    /// <summary>
    /// Represents a managed lock within the runtime.
    /// </summary>
    public abstract class BlockingObject
    {
        /// <summary>
        /// The object associated with the lock.
        /// </summary>
        abstract public ulong Object { get; }

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
        virtual public ulong Object { get; protected set; }

        /// <summary>
        /// The address of the root in the target process.
        /// </summary>
        virtual public ulong Address { get; protected set; }

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
        /// Returns the stack frame associated with this stack root.
        /// </summary>
        virtual public ClrStackFrame StackFrame { get { return null; } }

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
        abstract public ulong Start { get; }

        /// <summary>
        /// The end address of the segment.  All objects in this segment fall within Start &lt;= object &lt; End.
        /// </summary>
        abstract public ulong End { get; }

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
        virtual public ulong ReservedEnd { get { return 0; } }

        /// <summary>
        /// The address of the end of memory committed for the segment (this may be longer than Length).
        /// </summary>
        virtual public ulong CommittedEnd { get { return 0; } }

        /// <summary>
        /// FirstObject returns the first object on this segment or 0 if this segment contains no objects.
        /// </summary>
        abstract public ulong FirstObject { get; }

        /// <summary>
        /// FirstObject returns the first object on this segment or 0 if this segment contains no objects.
        /// </summary>
        /// <param name="type">The type of the first object.</param>
        /// <returns>The first object on this segment or 0 if this segment contains no objects.</returns>
        abstract public ulong GetFirstObject(out ClrType type);


        /// <summary>
        /// Given an object on the segment, return the 'next' object in the segment.  Returns
        /// 0 when there are no more objects.   (Or enumeration is not possible)  
        /// </summary>
        abstract public ulong NextObject(ulong objRef);

        /// <summary>
        /// Given an object on the segment, return the 'next' object in the segment.  Returns
        /// 0 when there are no more objects.   (Or enumeration is not possible)  
        /// </summary>
        abstract public ulong NextObject(ulong objRef, out ClrType type);


        /// <summary>
        /// Returns true if this is a segment for the Large Object Heap.  False otherwise.
        /// Large objects (greater than 85,000 bytes in size), are stored in their own segments and
        /// only collected on full (gen 2) collections. 
        /// </summary>
        virtual public bool IsLarge { get { return false; } }

        /// <summary>
        /// Returns true if this segment is the ephemeral segment (meaning it contains gen0 and gen1
        /// objects).
        /// </summary>
        virtual public bool IsEphemeral { get { return false; } }

        /// <summary>
        /// Ephemeral heap sements have geneation 0 and 1 in them.  Gen 1 is always above Gen 2 and
        /// Gen 0 is above Gen 1.  This property tell where Gen 0 start in memory.   Note that
        /// if this is not an Ephemeral segment, then this will return End (which makes Gen 0 empty
        /// for this segment)
        /// </summary>
        virtual public ulong Gen0Start { get { return Start; } }

        /// <summary>
        /// The length of the gen0 portion of this segment.
        /// </summary>
        virtual public ulong Gen0Length { get { return Length; } }

        /// <summary>
        /// The start of the gen1 portion of this segment.
        /// </summary>
        virtual public ulong Gen1Start { get { return End; } }

        /// <summary>
        /// The length of the gen1 portion of this segment.
        /// </summary>
        virtual public ulong Gen1Length { get { return 0; } }

        /// <summary>
        /// The start of the gen2 portion of this segment.
        /// </summary>
        virtual public ulong Gen2Start { get { return End; } }

        /// <summary>
        /// The length of the gen2 portion of this segment.
        /// </summary>
        virtual public ulong Gen2Length { get { return 0; } }

        /// <summary>
        /// Enumerates all objects on the segment.
        /// </summary>
        abstract public IEnumerable<ulong> EnumerateObjectAddresses();

        /// <summary>
        /// Returns the generation of an object in this segment.
        /// </summary>
        /// <param name="obj">An object in this segment.</param>
        /// <returns>The generation of the given object if that object lies in this segment.  The return
        ///          value is undefined if the object does not lie in this segment.
        /// </returns>
        virtual public int GetGeneration(ulong obj)
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

    internal abstract class HeapBase : ClrHeap
    {
        static protected readonly ClrObject[] s_emptyObjectSet = new ClrObject[0];
        private ulong _minAddr;          // Smallest and largest segment in the GC heap.  Used to make SegmentForObject faster.  
        private ulong _maxAddr;
        private ClrSegment[] _segments;
        private ulong[] _sizeByGen = new ulong[4];
        private ulong _totalHeapSize;
        private int _lastSegmentIdx;       // The last segment we looked at.
        private bool _canWalkHeap;
        private int _pointerSize;

        public HeapBase(RuntimeBase runtime)
        {
            _canWalkHeap = runtime.CanWalkHeap;
            MemoryReader = new MemoryReader(runtime.DataReader, 0x10000);
            _pointerSize = runtime.PointerSize;
        }

        public override ulong GetMethodTable(ulong obj)
        {
            if (MemoryReader.ReadPtr(obj, out ulong mt))
                return mt;

            return 0;
        }

        public override bool ReadPointer(ulong addr, out ulong value)
        {
            if (MemoryReader.Contains(addr))
                return MemoryReader.ReadPtr(addr, out value);

            return Runtime.ReadPointer(addr, out value);
        }

        internal int Revision { get; set; }

        protected abstract int GetRuntimeRevision();

        public override int PointerSize
        {
            get
            {
                return _pointerSize;
            }
        }

        public override bool CanWalkHeap
        {
            get
            {
                return _canWalkHeap;
            }
        }

        public override IList<ClrSegment> Segments
        {
            get
            {
                if (Revision != GetRuntimeRevision())
                    ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());
                return _segments;
            }
        }
        public override ulong TotalHeapSize
        {
            get { return _totalHeapSize; }
        }

        public override ulong GetSizeByGen(int gen)
        {
            Debug.Assert(gen >= 0 && gen < 4);
            return _sizeByGen[gen];
        }

        public override ClrType GetTypeByName(string name)
        {
            foreach (var module in Runtime.Modules)
            {
                var type = module.GetTypeByName(name);
                if (type != null)
                    return type;
            }

            return null;
        }

        internal MemoryReader MemoryReader { get; private set; }

        protected void UpdateSegmentData(HeapSegment segment)
        {
            _totalHeapSize += segment.Length;
            _sizeByGen[0] += segment.Gen0Length;
            _sizeByGen[1] += segment.Gen1Length;
            if (!segment.IsLarge)
                _sizeByGen[2] += segment.Gen2Length;
            else
                _sizeByGen[3] += segment.Gen2Length;
        }

        protected void InitSegments(RuntimeBase runtime)
        {
            // Populate segments
            if (runtime.GetHeaps(out SubHeap[] heaps))
            {
                var segments = new List<HeapSegment>();
                foreach (var heap in heaps)
                {
                    if (heap != null)
                    {
                        ISegmentData seg = runtime.GetSegmentData(heap.FirstLargeSegment);
                        while (seg != null)
                        {
                            var segment = new HeapSegment(runtime, seg, heap, true, this);
                            segments.Add(segment);

                            UpdateSegmentData(segment);
                            seg = runtime.GetSegmentData(seg.Next);
                        }

                        seg = runtime.GetSegmentData(heap.FirstSegment);
                        while (seg != null)
                        {
                            var segment = new HeapSegment(runtime, seg, heap, false, this);
                            segments.Add(segment);

                            UpdateSegmentData(segment);
                            seg = runtime.GetSegmentData(seg.Next);
                        }
                    }
                }

                UpdateSegments(segments.ToArray());
            }
            else
            {
                _segments = new ClrSegment[0];
            }
        }

        private void UpdateSegments(ClrSegment[] segments)
        {
            // sort the segments.  
            Array.Sort(segments, delegate (ClrSegment x, ClrSegment y) { return x.Start.CompareTo(y.Start); });
            _segments = segments;

            _minAddr = ulong.MaxValue;
            _maxAddr = ulong.MinValue;
            _totalHeapSize = 0;
            _sizeByGen = new ulong[4];
            foreach (var gcSegment in _segments)
            {
                if (gcSegment.Start < _minAddr)
                    _minAddr = gcSegment.Start;
                if (_maxAddr < gcSegment.End)
                    _maxAddr = gcSegment.End;

                _totalHeapSize += gcSegment.Length;
                if (gcSegment.IsLarge)
                    _sizeByGen[3] += gcSegment.Length;
                else
                {
                    _sizeByGen[2] += gcSegment.Gen2Length;
                    _sizeByGen[1] += gcSegment.Gen1Length;
                    _sizeByGen[0] += gcSegment.Gen0Length;
                }
            }
        }

        public override IEnumerable<ClrObject> EnumerateObjects()
        {
            if (Revision != GetRuntimeRevision())
                ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());

            for (int i = 0; i < _segments.Length; ++i)
            {
                ClrSegment seg = _segments[i];
                
                for (ulong obj = seg.GetFirstObject(out ClrType type); obj != 0; obj = seg.NextObject(obj, out type))
                {
                    _lastSegmentIdx = i;
                    yield return ClrObject.Create(obj, type);
                }
            }
        }

        public override IEnumerable<ulong> EnumerateObjectAddresses()
        {
            if (Revision != GetRuntimeRevision())
                ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());

            for (int i = 0; i < _segments.Length; ++i)
            {
                ClrSegment seg = _segments[i];
                for (ulong obj = seg.FirstObject; obj != 0; obj = seg.NextObject(obj))
                {
                    _lastSegmentIdx = i;
                    yield return obj;
                }
            }
        }

        public override ClrSegment GetSegmentByAddress(ulong objRef)
        {
            if (_minAddr <= objRef && objRef < _maxAddr)
            {
                // Start the segment search where you where last
                int curIdx = _lastSegmentIdx;
                for (;;)
                {
                    var segment = _segments[curIdx];
                    var offsetInSegment = (long)(objRef - segment.Start);
                    if (0 <= offsetInSegment)
                    {
                        var intOffsetInSegment = (long)offsetInSegment;
                        if (intOffsetInSegment < (long)segment.Length)
                        {
                            _lastSegmentIdx = curIdx;
                            return segment;
                        }
                    }

                    // Get the next segment loop until you come back to where you started.  
                    curIdx++;
                    if (curIdx >= Segments.Count)
                        curIdx = 0;
                    if (curIdx == _lastSegmentIdx)
                        break;
                }
            }
            return null;
        }

        internal override IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully)
        {
            if (type == null)
                type = GetObjectType(obj);
            else
                Debug.Assert(type == GetObjectType(obj));

            if (!type.ContainsPointers)
                return s_emptyObjectSet;

            GCDesc gcdesc = type.GCDesc;
            if (gcdesc == null)
                return s_emptyObjectSet;

            ulong size = type.GetSize(obj);
            if (carefully)
            {
                ClrSegment seg = GetSegmentByAddress(obj);
                if (seg == null || obj + size > seg.End || (!seg.IsLarge && size > 85000))
                    return s_emptyObjectSet;
            }

            List<ClrObject> result = new List<ClrObject>();
            gcdesc.WalkObject(obj, size, GetMemoryReaderForAddress(obj), (reference, offset) => result.Add(new ClrObject(reference, GetObjectType(reference))));
            return result;
        }

        internal override void EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, Action<ulong, int> callback)
        {
            if (type == null)
                type = GetObjectType(obj);
            else
                Debug.Assert(type == GetObjectType(obj));

            if (!type.ContainsPointers)
                return;

            GCDesc gcdesc = type.GCDesc;
            if (gcdesc == null)
                return;

            ulong size = type.GetSize(obj);
            if (carefully)
            {
                ClrSegment seg = GetSegmentByAddress(obj);
                if (seg == null || obj + size > seg.End || (!seg.IsLarge && size > 85000))
                    return;
            }

            gcdesc.WalkObject(obj, size, GetMemoryReaderForAddress(obj), callback);
        }

        protected abstract MemoryReader GetMemoryReaderForAddress(ulong obj);
    }


    internal class HeapSegment : ClrSegment
    {
        public override int ProcessorAffinity
        {
            get { return _subHeap.HeapNum; }
        }
        public override ulong Start { get { return _segment.Start; } }
        public override ulong End { get { return _subHeap.EphemeralSegment == _segment.Address ? _subHeap.EphemeralEnd : _segment.End; } }
        public override ClrHeap Heap { get { return _heap; } }

        public override bool IsLarge { get { return _large; } }

        public override ulong ReservedEnd { get { return _segment.Reserved; } }
        public override ulong CommittedEnd { get { return _segment.Committed; } }

        public override ulong Gen0Start
        {
            get
            {
                if (IsEphemeral)
                    return _subHeap.Gen0Start;
                else
                    return End;
            }
        }
        public override ulong Gen0Length { get { return End - Gen0Start; } }
        public override ulong Gen1Start
        {
            get
            {
                if (IsEphemeral)
                    return _subHeap.Gen1Start;
                else
                    return End;
            }
        }
        public override ulong Gen1Length { get { return Gen0Start - Gen1Start; } }
        public override ulong Gen2Start { get { return Start; } }
        public override ulong Gen2Length { get { return Gen1Start - Start; } }


        public override IEnumerable<ulong> EnumerateObjectAddresses()
        {
            for (ulong obj = FirstObject; obj != 0; obj = NextObject(obj))
                yield return obj;
        }

        public override ulong FirstObject
        {
            get
            {
                ulong start = Gen2Start;
                if (start >= End)
                    return 0;

                _heap.MemoryReader.EnsureRangeInCache(start);
                return start;
            }
        }

        public override ulong GetFirstObject(out ClrType type)
        {
            ulong start = Gen2Start;
            if (start >= End)
            {
                type = null;
                return 0;
            }

            _heap.MemoryReader.EnsureRangeInCache(start);
            type = _heap.GetObjectType(start);
            return start;
        }

        public override ulong NextObject(ulong objRef)
        {
            if (objRef >= CommittedEnd)
                return 0;

            uint minObjSize = (uint)_clr.PointerSize * 3;

            ClrType currType = _heap.GetObjectType(objRef);
            if (currType == null)
                return 0;

            ulong size = currType.GetSize(objRef);
            size = Align(size, _large);
            if (size < minObjSize)
                size = minObjSize;

            // Move to the next object
            objRef += size;

            // Check to make sure a GC didn't cause "count" to be invalid, leading to too large
            // of an object
            if (objRef >= End)
                return 0;

            // Ensure we aren't at the start of an alloc context
            while (!IsLarge && _subHeap.AllocPointers.TryGetValue(objRef, out ulong tmp))
            {
                tmp += Align(minObjSize, _large);

                // Only if there's data corruption:
                if (objRef >= tmp)
                    return 0;

                // Otherwise:
                objRef = tmp;

                if (objRef >= End)
                    return 0;
            }
            
            return objRef;
        }

        public override ulong NextObject(ulong objRef, out ClrType type)
        {
            if (objRef >= CommittedEnd)
            {
                type = null;
                return 0;
            }

            uint minObjSize = (uint)_clr.PointerSize * 3;

            ClrType currType = _heap.GetObjectType(objRef);
            if (currType == null)
            {
                type = null;
                return 0;
            }

            ulong size = currType.GetSize(objRef);
            size = Align(size, _large);
            if (size < minObjSize)
                size = minObjSize;

            // Move to the next object
            objRef += size;

            // Check to make sure a GC didn't cause "count" to be invalid, leading to too large
            // of an object
            if (objRef >= End)
            {
                type = null;
                return 0;
            }

            // Ensure we aren't at the start of an alloc context
            while (!IsLarge && _subHeap.AllocPointers.TryGetValue(objRef, out ulong tmp))
            {
                tmp += Align(minObjSize, _large);

                // Only if there's data corruption:
                if (objRef >= tmp)
                {
                    type = null;
                    return 0;
                }

                // Otherwise:
                objRef = tmp;

                if (objRef >= End)
                {
                    type = null;
                    return 0;
                }
            }

            type = _heap.GetObjectType(objRef);
            return objRef;
        }

        #region private
        internal static ulong Align(ulong size, bool large)
        {
            ulong AlignConst;
            ulong AlignLargeConst = 7;

            if (IntPtr.Size == 4)
                AlignConst = 3;
            else
                AlignConst = 7;

            if (large)
                return (size + AlignLargeConst) & ~(AlignLargeConst);

            return (size + AlignConst) & ~(AlignConst);
        }

        public override bool IsEphemeral { get { return _segment.Address == _subHeap.EphemeralSegment; ; } }
        internal HeapSegment(RuntimeBase clr, ISegmentData segment, SubHeap subHeap, bool large, HeapBase heap)
        {
            _clr = clr;
            _large = large;
            _segment = segment;
            _heap = heap;
            _subHeap = subHeap;
        }

        private bool _large;
        private RuntimeBase _clr;
        private ISegmentData _segment;
        private SubHeap _subHeap;
        private HeapBase _heap;
        #endregion
    }

}