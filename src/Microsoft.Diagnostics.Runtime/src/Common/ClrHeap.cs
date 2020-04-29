// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#pragma warning disable CA1721 // Property names should not match get methods
namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A representation of the CLR heap.
    /// </summary>
    public abstract class ClrHeap
    {
        /// <summary>
        /// Gets the runtime associated with this heap.
        /// </summary>
        public abstract ClrRuntime Runtime { get; }

        /// <summary>
        /// Returns true if the GC heap is in a consistent state for heap enumeration.  This will return false
        /// if the process was stopped in the middle of a GC, which can cause the GC heap to be unwalkable.
        /// Note, you may still attempt to walk the heap if this function returns false, but you will likely
        /// only be able to partially walk each segment.
        /// </summary>
        public abstract bool CanWalkHeap { get; }

        /// <summary>
        /// Returns the number of logical heaps in the process.
        /// </summary>
        public abstract int LogicalHeapCount { get; }

        /// <summary>
        /// A heap is has a list of contiguous memory regions called segments.  This list is returned in order of
        /// of increasing object addresses.
        /// </summary>
        public abstract ImmutableArray<ClrSegment> Segments { get; }

        /// <summary>
        /// Gets the <see cref="ClrType"/> representing free space on the GC heap.
        /// </summary>
        public abstract ClrType FreeType { get; }

        /// <summary>
        /// Gets the <see cref="ClrType"/> representing <see cref="string"/>.
        /// </summary>
        public abstract ClrType StringType { get; }

        /// <summary>
        /// Gets the <see cref="ClrType"/> representing <see cref="object"/>.
        /// </summary>
        public abstract ClrType ObjectType { get; }

        /// <summary>
        /// Gets the <see cref="ClrType"/> representing <see cref="System.Exception"/>.
        /// </summary>
        public abstract ClrType ExceptionType { get; }

        /// <summary>
        /// Gets a value indicating whether the GC heap is in Server mode.
        /// </summary>
        public abstract bool IsServer { get; }

        /// <summary>
        /// Gets a <see cref="ClrObject"/> for the given address on this heap.
        /// </summary>
        /// <remarks>
        /// The returned object will have a <see langword="null"/> <see cref="ClrObject.Type"/> if objRef does not point to
        /// a valid managed object.
        /// </remarks>
        /// <param name="objRef"></param>
        /// <returns></returns>
        public ClrObject GetObject(ulong objRef) => new ClrObject(objRef, GetObjectType(objRef));

        /// <summary>
        /// Obtains the type of an object at the given address.  Returns <see langword="null"/> if objRef does not point to
        /// a valid managed object.
        /// </summary>
        public abstract ClrType? GetObjectType(ulong objRef);

        /// <summary>
        /// Enumerates all objects on the heap.
        /// </summary>
        /// <returns>An enumerator for all objects on the heap.</returns>
        public abstract IEnumerable<ClrObject> EnumerateObjects();

        /// <summary>
        /// Enumerates all roots in the process.  Equivalent to the combination of:
        ///     ClrRuntime.EnumerateHandles().Where(handle => handle.IsStrong)
        ///     ClrRuntime.EnumerateThreads().SelectMany(thread => thread.EnumerateStackRoots())
        ///     ClrHeap.EnumerateFinalizerRoots()
        /// </summary>
        public abstract IEnumerable<IClrRoot> EnumerateRoots();

        /// <summary>
        /// Returns the GC segment which contains the given address.  This only searches ClrSegment.ObjectRange.
        /// </summary>
        public abstract ClrSegment? GetSegmentByAddress(ulong address);

        /// <summary>
        /// Enumerates all finalizable objects on the heap.
        /// </summary>
        public abstract IEnumerable<ClrObject> EnumerateFinalizableObjects();

        /// <summary>
        /// Enumerates all finalizable objects on the heap.
        /// </summary>
        public abstract IEnumerable<ClrFinalizerRoot> EnumerateFinalizerRoots();

        /// <summary>
        /// Enumerates all AllocationContexts for all segments.  Allocation contexts are locations on the GC
        /// heap which the GC uses to allocate new objects.  These regions of memory do not contain objects.
        /// AllocationContexts are the reason that you cannot simply enumerate the heap by adding each object's
        /// size to itself to get the next object on the segment, since if the address is an allocation context
        /// you will have to skip past it to find the next valid object.
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<MemoryRange> EnumerateAllocationContexts();

        /// <summary>
        /// Returns a string representation of this heap, including the size and number of segments.
        /// </summary>
        /// <returns>The string representation of this heap.</returns>
        public override string ToString()
        {
            ImmutableArray<ClrSegment> segments = Segments;
            double sizeMb = segments.Sum(s => (long)s.Length) / 1000000.0;
            return $"ClrHeap {sizeMb}mb {segments.Length} segments";
        }

        /// <summary>
        /// Use ClrObject.Size instead.
        /// </summary>
        /// <param name="objRef"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public abstract ulong GetObjectSize(ulong objRef, ClrType type);

        /// <summary>
        /// Enumerates all objects that the given object references.  This method is meant for internal use to
        /// implement ClrObject.EnumerateReferences, which you should use instead of calling this directly.
        /// </summary>
        /// <param name="obj">The object in question.</param>
        /// <param name="type">The type of the object.</param>
        /// <param name="considerDependantHandles">Whether to consider dependant handle mappings.</param>
        /// <param name="carefully">
        /// Whether to bounds check along the way (useful in cases where
        /// the heap may be in an inconsistent state.)
        /// </param>
        public abstract IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, bool considerDependantHandles);


        /// <summary>
        /// Enumerates all objects that the given object references.  This method is meant for internal use to
        /// implement ClrObject.EnumerateReferencesWithFields, which you should use instead of calling this directly.
        /// </summary>
        /// <param name="obj">The object in question.</param>
        /// <param name="type">The type of the object.</param>
        /// <param name="considerDependantHandles">Whether to consider dependant handle mappings.</param>
        /// <param name="carefully">
        /// Whether to bounds check along the way (useful in cases where
        /// the heap may be in an inconsistent state.)
        /// </param>
        public abstract IEnumerable<ClrReference> EnumerateReferencesWithFields(ulong obj, ClrType type, bool carefully, bool considerDependantHandles);
    }
}