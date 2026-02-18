# Migrating from ClrMD 2.x to 3.x

This guide covers the breaking changes introduced in ClrMD 3.0 and 3.1, and how to
update your code.  For context on _why_ these changes were made, see
[ReleaseNotes3.0.md](ReleaseNotes3.0.md) and [ReleaseNotes3.1.md](ReleaseNotes3.1.md).

---

## Types changed from `abstract` to `sealed`

Most of the core types in ClrMD were `abstract` classes in v2.  In v3 they are `sealed`:

| Type | v2 | v3 |
|------|----|----|
| `ClrHeap` | `abstract class` | `sealed class` |
| `ClrRuntime` | `abstract class` | `sealed class` |
| `ClrAppDomain` | `abstract class` | `sealed class` |
| `ClrThread` | `abstract class` | `sealed class` |
| `ClrSegment` | `abstract class` | `sealed class` |
| `ClrModule` | `abstract class` | `sealed class` |
| `ClrException` | `class` | `sealed class` |
| `ClrStackFrame` | `abstract class` | `sealed class` |

If you were subclassing any of these types (e.g. in tests or wrappers), you will need
to switch to using the new `Interfaces` namespace.  Each type now implements a
corresponding interface (`IClrHeap`, `IClrRuntime`, etc.) in
`Microsoft.Diagnostics.Runtime.Interfaces`.

---

## Removed types

### `DacLibrary` / SOSDac interfaces

`DacLibrary` and all of the `SOSDac*` types (`SOSDac`, `SOSDac6`, `SOSDac8`,
`SOSDac12`, `SOSDac13`), `MetadataImport`, `ClrDataProcess`, and the various DAC data
structures (e.g. `AppDomainData`, `HeapDetails`, `ThreadData`, etc.) have been made
**internal**.

These were raw, private CLR runtime APIs that were never intended for external
consumption.  If you depend on functionality that was only accessible through these
types, please open an issue requesting that it be exposed through the public ClrMD API.

**v2:**
```csharp
ClrRuntime runtime = ...;
DacLibrary dac = runtime.DacLibrary;
SOSDac sos = dac.SOSDacInterface;
// ... call SOS methods directly
```

**v3:** Use the higher-level ClrMD APIs instead.  The `ClrRuntime.DacLibrary` property
has been removed entirely.

---

### `ClrInfo.CreateRuntime(DacLibrary)` overload removed

Because `DacLibrary` is now internal, the overload that accepted it has been removed.
Use `CreateRuntime()` or `CreateRuntime(string dacPath)` instead.

---

### `GCRootPath`

Replaced by the value tuple `(ClrRoot Root, ChainLink Path)`.

**v2:**
```csharp
GCRootPath path = ...;
IClrRoot root = path.Root;
ImmutableArray<ClrObject> chain = path.Path;
```

**v3:**
```csharp
(ClrRoot root, GCRoot.ChainLink path) = ...;
// Walk the chain:
for (GCRoot.ChainLink? link = path; link != null; link = link.Next)
{
    ulong obj = link.Object;
}
```

---

### `ClrFinalizerRoot`

Removed.  Finalizer roots are now represented by the base `ClrRoot` class with
`RootKind == ClrRootKind.FinalizerQueue`.

**v2:**
```csharp
foreach (ClrFinalizerRoot root in heap.EnumerateFinalizerRoots())
    Console.WriteLine(root.Object);
```

**v3:**
```csharp
foreach (ClrRoot root in heap.EnumerateFinalizerRoots())
    Console.WriteLine(root.Object);
```

---

### `ClrStackInteriorRoot`

Removed.  All stack roots now use `ClrStackRoot`.  Interior roots are identified by
the `IsInterior` property inherited from `ClrRoot`.

---

### `IAddressableTypedEntity` / `IAddressableTypedEntityExtensions`

Removed.  The replacement is `IClrValue` in the `Interfaces` namespace.
`ClrObject` and `ClrValueType` now implement `IClrValue` instead.

---

### `IClrStackRoot`

Removed.  Use `ClrStackRoot` (concrete class) instead.

---

### `ObjectSet`

Removed.  Use `HashSet<ulong>` or re-implement it from the old ClrMD source if you
need the memory-optimized version.

---

### `GCRootProgressUpdatedEventHandler`

Removed along with the `GCRoot.ProgressUpdated` event.  The new `GCRoot` API does not
provide progress callbacks.

---

## GCRoot — complete rewrite

The `GCRoot` class was rewritten with a different usage pattern.  In v2 you
constructed a `GCRoot` from a `ClrHeap` and called methods that took target addresses.
In v3 you specify the target(s) at construction time and then enumerate paths.

### Constructor

**v2:**
```csharp
GCRoot gcroot = new GCRoot(heap);
```

**v3:**
```csharp
// Provide specific target addresses:
GCRoot gcroot = new GCRoot(heap, new ulong[] { targetAddress });

// Or provide a predicate:
GCRoot gcroot = new GCRoot(heap, obj => obj.Type?.Name == "MyClass");
```

### Finding root paths (`EnumerateGCRoots` → `EnumerateRootPaths`)

**v2:**
```csharp
foreach (GCRootPath rootPath in gcroot.EnumerateGCRoots(targetAddress))
{
    Console.WriteLine(rootPath.Root);
    foreach (ClrObject obj in rootPath.Path)
        Console.WriteLine($"  -> {obj}");
}
```

**v3:**
```csharp
foreach ((ClrRoot root, GCRoot.ChainLink path) in gcroot.EnumerateRootPaths())
{
    Console.WriteLine(root);
    for (GCRoot.ChainLink? link = path; link != null; link = link.Next)
        Console.WriteLine($"  -> {link.Object:x}");
}
```

### Finding a single path (`FindSinglePath` → `FindPathFrom`)

**v2:**
```csharp
LinkedList<ClrObject>? path = gcroot.FindSinglePath(sourceAddress, targetAddress);
```

**v3:**
```csharp
GCRoot.ChainLink? path = gcroot.FindPathFrom(new ClrObject(sourceAddress, heap.GetObjectType(sourceAddress)));
```

### `EnumerateAllPaths` — no direct replacement

`GCRoot.EnumerateAllPaths` has been removed with no direct equivalent.  Use
`EnumerateRootPaths` to enumerate paths from GC roots to the target objects.

---

## ClrHeap changes

### `abstract` → `sealed`

`ClrHeap` is now `sealed` and implements `IClrHeap`.  You can no longer subclass it.

### `LogicalHeapCount` → `SubHeaps`

**v2:**
```csharp
int heapCount = heap.LogicalHeapCount;
```

**v3:**
```csharp
int heapCount = heap.SubHeaps.Length;
// Access individual sub-heaps:
ClrSubHeap subHeap = heap.SubHeaps[0];
```

### `EnumerateObjectReferences` / `EnumerateReferencesWithFields` / `EnumerateReferenceAddresses` moved

These methods moved from `ClrHeap` to `ClrObject`:

**v2:**
```csharp
foreach (ClrObject child in heap.EnumerateObjectReferences(obj.Address, obj.Type, carefully: false, considerDependantHandles: true))
    ...
```

**v3:**
```csharp
foreach (ClrObject child in obj.EnumerateReferences())
    ...
```

### `EnumerateFinalizerRoots` return type change

**v2:** `IEnumerable<ClrFinalizerRoot>`
**v3:** `IEnumerable<ClrRoot>`

### `EnumerateRoots` return type change

**v2:** `IEnumerable<IClrRoot>`
**v3:** `IEnumerable<ClrRoot>`

### `GetSyncBlock(ulong)` → `EnumerateSyncBlocks()` + `ClrObject.SyncBlock`

The `ClrHeap.GetSyncBlock(ulong)` method still exists as an internal mechanism, but
the primary API is now `ClrObject.SyncBlock` or `ClrHeap.EnumerateSyncBlocks()`.

### New members

- `EnumerateObjects(bool carefully)` — overload with a `carefully` parameter.
- `EnumerateObjects(MemoryRange range, bool carefully)` — enumerate objects in a range.
- `IsObjectCorrupted(ulong, out ObjectCorruption?)` — check for heap corruption.
- `VerifyHeap()` — verify the entire heap.
- `GetTypeByMethodTable(ulong)` — look up a type by its method table.
- `GetTypeByName(string)` / `GetTypeByName(ClrModule, string)` — look up types by name.
- `FindNextObjectOnSegment(ulong)` / `FindPreviousObjectOnSegment(ulong)` — navigate
  objects within a segment.

---

## ClrRuntime changes

### `DacLibrary` property removed

See the [DacLibrary section](#daclibrary--sosdac-interfaces) above.

### `IsThreadSafe` — no longer abstract

Now delegates to `DataTarget.DataReader.IsThreadSafe`.

### New members

- `ClrThreadPool? ThreadPool` — access the thread pool.
- `GetAppDomainByAddress(ulong)` — look up an app domain by address.
- `EnumerateSyncBlockCleanupData()` — enumerate sync block cleanup data.
- `EnumerateRcwCleanupData()` — enumerate RCW cleanup data.
- `CanFlushData` — check if flushing is supported.

---

## IClrRoot changes

The `IClrRoot` interface moved from `Microsoft.Diagnostics.Runtime` to
`Microsoft.Diagnostics.Runtime.Interfaces`.  A new concrete class `ClrRoot` is now the
primary type used throughout the API.

`ClrHandle` now inherits from `ClrRoot` instead of directly implementing `IClrRoot`.

---

## ClrType changes

### `IsPublic` / `IsPrivate` / `IsInternal` / `IsProtected` / `IsAbstract` / `IsSealed` / `IsInterface` removed

These individual boolean properties have been replaced by a single `TypeAttributes`
property (from `System.Reflection`):

**v2:**
```csharp
if (type.IsPublic)
    ...
```

**v3:**
```csharp
if ((type.TypeAttributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public)
    ...
// or for IsAbstract:
if ((type.TypeAttributes & TypeAttributes.Abstract) != 0)
    ...
```

### `Module` is no longer nullable

**v2:** `ClrModule? Module { get; }`
**v3:** `ClrModule Module { get; }`

### New members

- `ThreadStaticFields` — access thread-static fields.

---

## ClrField changes

### `IsPublic` / `IsPrivate` / `IsInternal` / `IsProtected` removed

Replaced by `FieldAttributes Attributes`:

**v2:**
```csharp
if (field.IsPublic)
    ...
```

**v3:**
```csharp
if ((field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public)
    ...
```

---

## ClrThread changes

### `Is*` boolean properties removed

The following properties were removed:
- `IsAbortRequested`, `IsAborted`, `IsGCSuspendPending`, `IsUserSuspended`,
  `IsDebugSuspended`, `IsBackground`, `IsUnstarted`, `IsCoInitialized`, `IsSTA`,
  `IsMTA`

Check the corresponding flags on the new `ClrThreadState State` property instead:

**v2:**
```csharp
if (thread.IsBackground)
    ...
```

**v3:**
```csharp
if ((thread.State & ClrThreadState.TS_Background) != 0)
    ...
```

### `EnumerateStackRoots` return type change

**v2:** `IEnumerable<IClrStackRoot>`
**v3:** `IEnumerable<ClrStackRoot>`

### `CurrentAppDomain` is now nullable

**v2:** `ClrAppDomain CurrentAppDomain { get; }`
**v3:** `ClrAppDomain? CurrentAppDomain { get; }`

### New members

- `ClrThreadState State` — thread state flags.
- `bool IsGc` — whether this is the GC thread.
- `EnumerateStackTrace(bool includeContext, int maxFrames)` — overload with frame limit.

---

## ClrSegment changes

### `Is*Segment` boolean properties removed

`IsLargeObjectSegment`, `IsPinnedObjectSegment`, and `IsEphemeralSegment` were removed.
Use the new `GCSegmentKind Kind` property instead:

**v2:**
```csharp
if (segment.IsLargeObjectSegment)
    ...
```

**v3:**
```csharp
if (segment.Kind == GCSegmentKind.Large)
    ...
```

### `LogicalHeap` → `SubHeap`

**v2:** `int LogicalHeap { get; }`
**v3:** `ClrSubHeap SubHeap { get; }`

### `GetGeneration` return type change

**v2:** Returns `int`
**v3:** Returns `Generation` (an enum with values `Generation0`, `Generation1`,
`Generation2`, `Large`, `Pinned`, `Frozen`, `Unknown`)

### New members

- `GCSegmentKind Kind` — the kind of segment (Gen0, Gen1, Gen2, Large, Pinned, Frozen,
  Ephemeral).
- `ClrSegmentFlags Flags` — segment flags.
- `ulong Address` — the segment address.
- `EnumerateObjects(MemoryRange, bool)` — enumerate objects in a range.

---

## ClrModule changes

### `MetadataImport` property removed

`MetadataImport` was removed because the `MetadataImport` class is now internal.

### `ResolveToken` removed

`ClrModule.ResolveToken(int)` has been removed.

### New members

- `EnumerateTypeRefToMethodTableMap()` — enumerate type ref to method table mappings.

---

## DataTarget changes

### New members

- `AddClrInfoProvider(IClrInfoProvider)` — register a custom CLR info provider.
- `LoadDump(string filePath)` — a convenience overload without `CacheOptions`.

---

## ClrObject changes

### Implements `IClrValue` instead of `IAddressableTypedEntity`

See the [IAddressableTypedEntity section](#iaddressabletypedentity--iaddressabletypedentityextensions)
above.

### New members

- `GetThinLock()` — get thin lock information.
- `TryReadStringField(string, int?, out string?)` — try to read a string field.
- `Equals(ClrValueType)` — equality comparison with value types.

---

## New types in v3

The following notable types were added:

- **`ClrRoot`** — concrete class replacing `ClrFinalizerRoot` and
  `ClrStackInteriorRoot`.
- **`ClrSubHeap`** — represents an individual GC sub-heap (replaces the old logical
  heap index).
- **`ClrThreadPool`** — provides thread pool diagnostics.
- **`ClrThreadState`** — flags enum for thread state (replaces individual `Is*`
  properties).
- **`GCSegmentKind`** — enum for segment kinds (replaces individual `Is*Segment`
  properties).
- **`Generation`** — enum for GC generations.
- **`ObjectCorruption` / `ObjectCorruptionKind`** — heap corruption detection.
- **`ClrThinLock`** — thin lock information.
- **`ClrThreadStaticField`** — thread-static field support.
- **`ClrGenerationData`** — per-generation heap data.
- **`GCRoot.ChainLink`** — linked list of objects in a GC root path.
- **`IClrValue`** — interface replacing `IAddressableTypedEntity`.
- **`Interfaces` namespace** — contains interfaces (`IClrHeap`, `IClrRuntime`,
  `IClrType`, etc.) for testability and abstraction.

---

## Summary of namespace changes

| v2 Namespace / Type | v3 Status |
|---------------------|-----------|
| `Microsoft.Diagnostics.Runtime.IClrRoot` | Moved to `Microsoft.Diagnostics.Runtime.Interfaces.IClrRoot` |
| `Microsoft.Diagnostics.Runtime.IAddressableTypedEntity` | Replaced by `Interfaces.IClrValue` |
| `Microsoft.Diagnostics.Runtime.IClrStackRoot` | Removed, use `ClrStackRoot` |
| `Microsoft.Diagnostics.Runtime.DacInterface.*` (public types) | Made `internal` |
| `Microsoft.Diagnostics.Runtime.DacLibrary` | Made `internal` |

---

## Quick reference: common migration patterns

| v2 Pattern | v3 Replacement |
|-----------|---------------|
| `new GCRoot(heap)` | `new GCRoot(heap, targets)` or `new GCRoot(heap, predicate)` |
| `gcroot.EnumerateGCRoots(target)` | `gcroot.EnumerateRootPaths()` |
| `gcroot.FindSinglePath(src, tgt)` | `gcroot.FindPathFrom(startObj)` |
| `GCRootPath` | `(ClrRoot Root, GCRoot.ChainLink Path)` |
| `runtime.DacLibrary` | _(removed — use higher-level APIs)_ |
| `type.IsPublic` | `(type.TypeAttributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public` |
| `field.IsPublic` | `(field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public` |
| `thread.IsBackground` | `(thread.State & ClrThreadState.TS_Background) != 0` |
| `segment.IsLargeObjectSegment` | `segment.Kind == GCSegmentKind.Large` |
| `segment.LogicalHeap` | `segment.SubHeap` |
| `heap.LogicalHeapCount` | `heap.SubHeaps.Length` |
| `heap.EnumerateObjectReferences(addr, type, ...)` | `obj.EnumerateReferences()` |
| `heap.EnumerateFinalizerRoots()` | Returns `IEnumerable<ClrRoot>` (was `ClrFinalizerRoot`) |
| `new ObjectSet(heap)` | `new HashSet<ulong>()` |
| `ClrInfo.CreateRuntime(dacLibrary)` | `ClrInfo.CreateRuntime()` or `CreateRuntime(dacPath)` |
