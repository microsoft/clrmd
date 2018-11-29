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
