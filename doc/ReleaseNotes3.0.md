# ClrMD 3.0 Release Notes

Changes in the .Net Runtime can often change the meaning or structure of how we build diagnostic tools.  Recent versions of .Net introduced a feature called GC_REGIONS.  This change restructured the meaning of some of the internals of the CLR runtime.

This feature required changes to not only properly represent the GC heap correctly, it also means that versions of ClrMD prior to 3.0 cannot properly walk the managed heap when the GC_REGIONS feature is used in the runtime.  In .Net 8, GC_REGIONS is the default version of the GC heap for x64 bit processes.  There may be other circumstances where it is used, which we will not attempt to document here.

We also used this required breaking change as an opportunity to refactor and fix parts of the library which could only be changed in a major breaking version.

## Major Changes

1.  Support for the GC_REGIONS feature.  Versions of ClrMD prior to 3.0 cannot be used to correctly walk the managed heap if GC_REGIONS is turned on.  This includes now giving information about ClrSubHeaps (`ClrHeap.SubHeaps`) instead of a heap index.
2.  Marked SOSDac interfaces as internal.  These are the raw, private APIs that the runtime itself exposes to build tools on top of.  These are not meant for public use or consumption, and marking them public was originally a shortcut to sidestep having to add new ClrMD APIs.
3.  Changed how ClrMD creates and stores roots.
4.  Rewrote GCRoot class, including restructuring how it is used.

## Migration Guide

### Removed or Rewritten

1.  **ClrFinalizerRoot** - removed - Use ClrRoot.Kind == FinalizerQueue
2. **ClrStackInteriorRoot** - removed - Use ClrRoot properties
3. **GCRoot** - rewrote - Write code using it from scratch using the new version of the class
4. **ObjectSet** - removed - Use either HashSet<ulong> or re-implement ObjectSet based on the old ClrMD mechanism
5. **DacLibrary/MetadataImport/SOSDac interfaces** - removed - If there's functionality there it needs to be exposed through the public ClrMD interfaces

### Modified

1. **ClrField/ClrMethod/ClrType** - IsPublic/IsPrivate/etc were removed, use .Attributes and check the attributes for the correct protection on the field/method/type
2. **ClrThread.Is___** - Check the appropriate flags on ClrThread.State
3. **ClrHeap.LogicalHeapCount/ClrSegment.LogicalHeap** - "LogicalHeaps" are now ClrSubHeaps.  They can be accessed via ClrHeap.SubHeaps and ClrSegment.SubHeap
4. **ClrSegment Is___Segment** - ClrSegment.Kind
