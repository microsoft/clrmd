// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
        public abstract ClrType GetObjectType(ulong objRef);

        /// <summary>
        /// Gets a <see cref="ClrObject"/> for the given address on this heap.
        /// </summary>
        /// <remarks>
        /// The returned object will have a <c>null</c> <see cref="ClrObject.Type"/> if objRef does not point to
        /// a valid managed object.
        /// </remarks>
        /// <param name="objRef"></param>
        /// <returns></returns>
        public ClrObject GetObject(ulong objRef) => ClrObject.Create(objRef, GetObjectType(objRef));

        /// <summary>
        /// Returns whether this version of CLR has component MethodTables.  Component MethodTables were removed from
        /// desktop CLR in v4.6, and do not exist at all on .Net Native.  If this method returns false, all component
        /// MethodTables will be 0, and expected to be 0 when an argument to a function.
        /// </summary>
        public virtual bool HasComponentMethodTables => true;

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
        public abstract bool TryGetMethodTable(ulong obj, out ulong methodTable, out ulong componentMethodTable);

        /// <summary>
        /// Attempts to retrieve the MethodTable from the given object.
        /// Note that this some ClrTypes cannot be uniquely determined by MethodTable alone.  In
        /// Desktop CLR, arrays of reference types all use the same MethodTable.  To uniquely
        /// determine an array of references you must also have its component type.
        /// Note this function has undefined behavior if you do not pass a valid object reference
        /// to it.
        /// </summary>
        /// <param name="obj">The object to get the MethodTable of.</param>
        /// <returns>The MethodTable of the object, or 0 if the address could not be read from.</returns>
        public abstract ulong GetMethodTable(ulong obj);

        /// <summary>
        /// Retrieves the EEClass from the given MethodTable.  EEClasses do not exist on
        /// .Net Native.
        /// </summary>
        /// <param name="methodTable">The MethodTable to get the EEClass from.</param>
        /// <returns>
        /// The EEClass for the given MethodTable, 0 if methodTable is invalid or
        /// does not exist.
        /// </returns>
        public virtual ulong GetEEClassByMethodTable(ulong methodTable)
        {
            return 0;
        }

        /// <summary>
        /// Retrieves the MethodTable associated with the given EEClass.
        /// </summary>
        /// <param name="eeclass">The eeclass to get the method table from.</param>
        /// <returns>
        /// The MethodTable for the given EEClass, 0 if eeclass is invalid
        /// or does not exist.
        /// </returns>
        public virtual ulong GetMethodTableByEEClass(ulong eeclass)
        {
            return 0;
        }

        /// <summary>
        /// Returns a  wrapper around a System.Exception object (or one of its subclasses).
        /// </summary>
        public virtual ClrException GetExceptionObject(ulong objRef)
        {
            return null;
        }

        /// <summary>
        /// Returns the runtime associated with this heap.
        /// </summary>
        public abstract ClrRuntime Runtime { get; }

        /// <summary>
        /// A heap is has a list of contiguous memory regions called segments.  This list is returned in order of
        /// of increasing object addresses.
        /// </summary>
        public abstract IList<ClrSegment> Segments { get; }

        /// <summary>
        /// Enumerate the roots of the process.  (That is, all objects which keep other objects alive.)
        /// Equivalent to EnumerateRoots(true).
        /// </summary>
        public abstract IEnumerable<ClrRoot> EnumerateRoots();

        /// <summary>
        /// Sets the stackwalk policy for enumerating roots.  See <see cref="ClrRootStackwalkPolicy" /> for more information.
        /// Setting this field can invalidate the root cache.
        /// </summary>
        public abstract ClrRootStackwalkPolicy StackwalkPolicy { get; set; }

        /// <summary>
        /// Caches all relevant heap information into memory so future heap operations run faster and
        /// do not require touching the debuggee.
        /// </summary>
        /// <param name="cancelToken">A cancellation token to stop caching the heap.</param>
        public virtual void CacheHeap(CancellationToken cancelToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Releases all cached object data to reclaim memory.
        /// </summary>
        public virtual void ClearHeapCache()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns true if the heap is cached, false otherwise.
        /// </summary>
        public virtual bool IsHeapCached => false;

        /// <summary>
        /// Returns whether the roots of the process are cached or not.
        /// </summary>
        public abstract bool AreRootsCached { get; }

        /// <summary>
        /// This method caches many roots so that subsequent calls to EnumerateRoots run faster.
        /// </summary>
        public abstract void CacheRoots(CancellationToken cancelToken);

        protected internal virtual void BuildDependentHandleMap(CancellationToken cancelToken)
        {
        }

        protected internal virtual IEnumerable<ClrRoot> EnumerateStackRoots()
        {
            throw new NotImplementedException();
        }

        protected internal virtual IEnumerable<ClrHandle> EnumerateStrongHandles()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This method clears any previously cached roots to reclaim memory.
        /// </summary>
        public abstract void ClearRootCache();

        /// <summary>
        /// Looks up a type by name.
        /// </summary>
        /// <param name="name">The name of the type.</param>
        /// <returns>
        /// The ClrType matching 'name', null if the type was not found, and undefined if more than one
        /// type shares the same name.
        /// </returns>
        public abstract ClrType GetTypeByName(string name);

        /// <summary>
        /// Retrieves the given type by its MethodTable/ComponentMethodTable pair.
        /// </summary>
        /// <param name="methodTable">The ClrType.MethodTable for the requested type.</param>
        /// <param name="componentMethodTable">The ClrType's component MethodTable for the requested type.</param>
        /// <returns>A ClrType object, or null if no such type exists.</returns>
        public abstract ClrType GetTypeByMethodTable(ulong methodTable, ulong componentMethodTable);

        /// <summary>
        /// Retrieves the given type by its MethodTable/ComponentMethodTable pair.  Note this is only valid if
        /// the given type's component MethodTable is 0.
        /// </summary>
        /// <param name="methodTable">The ClrType.MethodTable for the requested type.</param>
        /// <returns>A ClrType object, or null if no such type exists.</returns>
        public virtual ClrType GetTypeByMethodTable(ulong methodTable)
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
        /// <param name="enumerateStatics">
        /// True if we should enumerate static variables.  Enumerating with statics
        /// can take much longer than enumerating without them.  Additionally these will be be "double reported",
        /// since all static variables are pinned by handles on the HandleTable (which is also enumerated with
        /// EnumerateRoots).  You would want to enumerate statics with roots if you care about what exact statics
        /// root what objects, but not if you care about performance.
        /// </param>
        public abstract IEnumerable<ClrRoot> EnumerateRoots(bool enumerateStatics);

        /// <summary>
        /// Enumerates all types in the runtime.
        /// </summary>
        /// <returns>
        /// An enumeration of all types in the target process.  May return null if it's unsupported for
        /// that version of CLR.
        /// </returns>
        public virtual IEnumerable<ClrType> EnumerateTypes()
        {
            return null;
        }

        /// <summary>
        /// Enumerates all finalizable objects on the heap.
        /// </summary>
        public virtual IEnumerable<ulong> EnumerateFinalizableObjectAddresses()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Enumerates all managed locks in the process.  That is anything using System.Monitor either explicitly
        /// or implicitly through "lock (obj)".  This is roughly equivalent to combining SOS's !syncblk command
        /// with !dumpheap -thinlock.
        /// </summary>
        public virtual IEnumerable<BlockingObject> EnumerateBlockingObjects()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns true if the GC heap is in a consistent state for heap enumeration.  This will return false
        /// if the process was stopped in the middle of a GC, which can cause the GC heap to be unwalkable.
        /// Note, you may still attempt to walk the heap if this function returns false, but you will likely
        /// only be able to partially walk each segment.
        /// </summary>
        public abstract bool CanWalkHeap { get; }

        /// <summary>
        /// Enumerates all objects on the heap.  This is equivalent to enumerating all segments then walking
        /// each object with ClrSegment.FirstObject, ClrSegment.NextObject, but in a simple enumerator
        /// for easier use in linq queries.
        /// </summary>
        /// <returns>An enumerator for all objects on the heap.</returns>
        public abstract IEnumerable<ulong> EnumerateObjectAddresses();

        /// <summary>
        /// Enumerates all objects on the heap.
        /// </summary>
        /// <returns>An enumerator for all objects on the heap.</returns>
        public abstract IEnumerable<ClrObject> EnumerateObjects();

        /// <summary>
        /// TotalHeapSize is defined as the sum of the length of all segments.
        /// </summary>
        public abstract ulong TotalHeapSize { get; }

        /// <summary>
        /// Get the size by generation 0, 1, 2, 3.  The large object heap is Gen 3 here.
        /// The sum of all of these should add up to the TotalHeapSize.
        /// </summary>
        public abstract ulong GetSizeByGen(int gen);

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
        public bool IsInHeap(ulong address)
        {
            return GetSegmentByAddress(address) != null;
        }

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
            double sizeMb = TotalHeapSize / 1000000.0;
            int segCount = Segments != null ? Segments.Count : 0;
            return $"ClrHeap {sizeMb}mb {segCount} segments";
        }

        /// <summary>
        /// Read 'count' bytes from the ClrHeap at 'address' placing it in 'buffer' starting at offset 'offset'
        /// </summary>
        public virtual int ReadMemory(ulong address, byte[] buffer, int offset, int count)
        {
            return 0;
        }

        /// <summary>
        /// Attempts to efficiently read a pointer from memory.  This acts exactly like ClrRuntime.ReadPointer, but
        /// there is a greater chance you will hit a cache for a more efficient memory read.
        /// </summary>
        /// <param name="addr">The address to read.</param>
        /// <param name="value">The pointer value.</param>
        /// <returns>True if we successfully read the value, false if addr is not mapped into the process space.</returns>
        public abstract bool ReadPointer(ulong addr, out ulong value);

        protected internal abstract IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully);
        protected internal abstract void EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, Action<ulong, int> callback);

        /// <summary>
        /// This might be useful to be public, but we actually don't know the total number objects without walking the entire
        /// heap.  This property is only valid if we have cached the heap...which leads to a weird programmatic interface (that
        /// this simply property would throw InvalidOperationException unless the heap is cached).  I'm leaving this internal
        /// until I am convinced there's a good way to surface this.
        /// </summary>
        protected internal virtual long TotalObjects => -1;
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