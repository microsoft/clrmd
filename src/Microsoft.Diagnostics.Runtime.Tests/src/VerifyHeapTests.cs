// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.Utilities.DbgEng;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class VerifyHeapTests
    {
        [Fact]
        public void ServerNoCorruption()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;
            Assert.Empty(heap.VerifyHeap());
        }

        [Fact]
        public void WorkstationNoCorruption()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(GCMode.Workstation);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;
            Assert.Empty(heap.VerifyHeap());
        }

        [WindowsFact]
        public void HeapCorruptionStillEnumeratesFact()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpWithDbgEng(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IDebugDataSpaces spaces = GetDataReader(dt).DebugDataSpaces;

            ClrHeap heap = runtime.Heap;
            ClrObject obj = FindMostInterestingObject(heap);
            ClrObject arr = FindArrayObject(heap);

            Assert.Empty(heap.VerifyHeap());

            ClrObject[] objs = heap.EnumerateObjects().ToArray();
            Assert.Contains(obj, objs);
            Assert.Contains(arr, objs);

            Assert.Empty(heap.VerifyHeap());

            WriteAndRun(spaces, obj, 0xcccccccc, () => {
                Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption objCorruption));
                Assert.NotNull(objCorruption);

                WriteAndRun(spaces, arr, 0xcccccccc, () => {
                    Assert.True(heap.IsObjectCorrupted(arr, out ObjectCorruption arrCorruption));
                    Assert.NotNull(arrCorruption);

                    // Ensure if we use "carefully" that we can step past object corruption.
                    ClrObject[] foundObjs = heap.EnumerateObjects(carefully: true).ToArray();
                    Assert.Equal(objs.Length, foundObjs.Length);

                    ClrObject corruptedObject = Assert.Single(foundObjs.Where(f => f.Address == obj.Address));
                    Assert.False(corruptedObject.IsValid);

                    ClrObject corruptedArray = Assert.Single(foundObjs.Where(f => f.Address == arr.Address));
                    Assert.False(corruptedArray.IsValid);

                    ObjectCorruption[] corrupted = heap.VerifyHeap().ToArray();
                    Assert.Equal(2, corrupted.Length);

                    Assert.Single(corrupted.Where(c => c.Object == corruptedObject));
                    Assert.Single(corrupted.Where(c => c.Object == corruptedArray));
                });
            });
        }

        [WindowsFact]
        public void HeapCorruptionPrevNextObj()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpWithDbgEng(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IDebugDataSpaces spaces = GetDataReader(dt).DebugDataSpaces;

            ClrHeap heap = runtime.Heap;
            ClrObject obj = FindMostInterestingObject(heap);
            ClrSegment segment = heap.GetSegmentByAddress(obj);

            WriteAndRun(spaces, obj, 0xcccccccc, () => {
                Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption objCorruption));
                Assert.NotNull(objCorruption);

                ClrObject prev = heap.FindPreviousObjectOnSegment(obj, carefully: true);
                Assert.True(prev.IsValid);
                Assert.True(prev < obj);
                Assert.Same(segment, heap.GetSegmentByAddress(prev));

                ClrObject next = heap.FindNextObjectOnSegment(obj, carefully: true);
                Assert.True(next.IsValid);
                Assert.True(obj < next);
                Assert.Same(segment, heap.GetSegmentByAddress(next));

                ClrObject[] objects = heap.EnumerateObjects(new MemoryRange(prev.Address, next.Address + 1), carefully: true).ToArray();
                Assert.Equal(3, objects.Length);

                Assert.Equal(prev, objects[0]);
                Assert.Equal(obj, objects[1]);
                Assert.Equal(next, objects[2]);

                objects = segment.EnumerateObjects(new MemoryRange(prev.Address, next.Address + 1), carefully: true).ToArray();
                Assert.Equal(3, objects.Length);

                Assert.Equal(prev, objects[0]);
                Assert.Equal(obj, objects[1]);
                Assert.Equal(next, objects[2]);
            });
        }


        [Fact]
        public void ObjectNotOnHeapTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpWithDbgEng(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IDebugDataSpaces spaces = GetDataReader(dt).DebugDataSpaces;

            ClrHeap heap = runtime.Heap;
            ClrObject obj = heap.GetObject(0x12345678);

            Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
            Assert.NotNull(result);
            Verify(obj, result);

            Assert.False(heap.FullyVerifyObject(obj, out IEnumerable<ObjectCorruption> enumResult));
            result = Assert.Single(enumResult);
            Verify(obj, result);

            static void Verify(ClrObject obj, ObjectCorruption result)
            {
                Assert.Equal(ObjectCorruptionKind.ObjectNotOnTheHeap, result.Kind);
                Assert.Equal(obj, result.Object);
                Assert.Equal(0, result.Offset);
            }
        }

        [WindowsFact]
        public void SyncBlockZeroTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpWithDbgEng(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IDebugDataSpaces spaces = GetDataReader(dt).DebugDataSpaces;

            ClrHeap heap = runtime.Heap;
            ClrObject obj = FindMostInterestingObject(heap);

            Assert.NotNull(obj.SyncBlock);
            Assert.False(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
            Assert.Null(result);

            WriteAndRun(spaces, obj - 4, (ushort)0, () => {
                Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
                Assert.NotNull(result);

                Assert.Equal(ObjectCorruptionKind.SyncBlockZero, result.Kind);
                Assert.Equal(obj, result.Object);
                Assert.Equal(-4, result.Offset); // MT offset is at 0
            });
        }


        [WindowsFact]
        public void SyncBlockMismatchTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpWithDbgEng(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IDebugDataSpaces spaces = GetDataReader(dt).DebugDataSpaces;

            ClrHeap heap = runtime.Heap;
            ClrObject obj = FindMostInterestingObject(heap);

            Assert.NotNull(obj.SyncBlock);
            Assert.False(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
            Assert.Null(result);

            WriteAndRun(spaces, obj - 4, (ushort)0xcc, () => {
                Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
                Assert.NotNull(result);

                Assert.Equal(ObjectCorruptionKind.SyncBlockMismatch, result.Kind);
                Assert.Equal(obj, result.Object);
                Assert.Equal(-4, result.Offset); // MT offset is at 0
            });
        }

        [WindowsFact]
        public void BadMethodTableTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpWithDbgEng(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IDebugDataSpaces spaces = GetDataReader(dt).DebugDataSpaces;

            ClrHeap heap = runtime.Heap;
            ClrObject obj = FindMostInterestingObject(heap);

            WriteAndRun(spaces, obj, 0xcccccc, () => {
                Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
                Verify(obj, result);

                Assert.False(heap.FullyVerifyObject(obj, out IEnumerable<ObjectCorruption> detectedCorruption));
                var item = detectedCorruption.ToArray();
                result = Assert.Single(detectedCorruption);
                Verify(obj, result);

                static void Verify(ClrObject obj, ObjectCorruption result)
                {
                    Assert.NotNull(result);

                    Assert.Equal(ObjectCorruptionKind.InvalidMethodTable, result.Kind);
                    Assert.Equal(obj, result.Object);
                    Assert.Equal(0, result.Offset); // MT offset is at 0
                }
            });


            // Ensure WriteAndRun sets the value back
            Assert.False(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
            Assert.Null(result);
        }

        [WindowsFact]
        public void BadObjectReferenceTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpWithDbgEng(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IDebugDataSpaces spaces = GetDataReader(dt).DebugDataSpaces;

            ClrHeap heap = runtime.Heap;
            ClrObject obj = FindMostInterestingObject(heap);

            Assert.False(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
            Assert.Null(result);

            ClrObject free = heap.EnumerateObjects().First(obj => obj.IsFree);
            foreach (ClrReference reference in obj.EnumerateReferencesWithFields())
            {
                uint offset = (uint)IntPtr.Size + (uint)reference.Offset;
                WriteAndRun(spaces, obj + offset, 0xccccc0, () => {
                    Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
                    Assert.NotNull(result);

                    Assert.Equal(ObjectCorruptionKind.InvalidObjectReference, result.Kind);
                    Assert.Equal(obj, result.Object);
                    Assert.Equal((int)offset, result.Offset);
                });

                WriteAndRun(spaces, obj + offset, 0xcccccc, () => {
                    Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
                    Assert.NotNull(result);

                    Assert.Equal(ObjectCorruptionKind.ObjectReferenceNotPointerAligned, result.Kind);
                    Assert.Equal(obj, result.Object);
                    Assert.Equal((int)offset, result.Offset);
                });


                WriteAndRun(spaces, obj + offset, (ulong)free, () => {
                    Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
                    Assert.NotNull(result);

                    Assert.Equal(ObjectCorruptionKind.FreeObjectReference, result.Kind);
                    Assert.Equal(obj, result.Object);
                    Assert.Equal((int)offset, result.Offset);
                });
            }
        }

        [WindowsFact]
        public void ObjectTooLargeTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpWithDbgEng(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IDebugDataSpaces spaces = GetDataReader(dt).DebugDataSpaces;

            ClrHeap heap = runtime.Heap;
            ClrObject obj = FindArrayObject(heap);

            Assert.True(obj.IsArray);
            Assert.False(obj.Type.IsString);
            Assert.False(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
            Assert.Null(result);

            WriteAndRun(spaces, obj + (uint)IntPtr.Size, 0xcccccccc, () => {
                Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
                Assert.NotNull(result);

                Assert.Equal(ObjectCorruptionKind.ObjectTooLarge, result.Kind);
                Assert.Equal(obj, result.Object);
                Assert.Equal(IntPtr.Size, result.Offset); // Array count is after the MethodTable
            });
        }

        private static unsafe void WriteAndRun<T>(IDebugDataSpaces spaces, ulong location, T value, Action action)
            where T : unmanaged
        {
            byte[] old = new byte[sizeof(T)];
            byte[] newBuffer = new byte[sizeof(T)];

            Span<T> span = new(&value, 1);
            MemoryMarshal.Cast<T, byte>(span).CopyTo(newBuffer);

            if (!spaces.ReadVirtual(location, old, out int read) || read != old.Length || old.SequenceEqual(newBuffer))
                throw new IOException();

            if (spaces.WriteVirtual(location, newBuffer, out int written) != 0 || written != newBuffer.Length)
                throw new IOException();

            action();

            if (spaces.WriteVirtual(location, old, out written) != 0 || written != old.Length)
                throw new IOException();
        }

        private static DbgEngIDataReader GetDataReader(DataTarget dt)
        {
            return (DbgEngIDataReader)dt.DataReader;
        }

        private static ClrObject FindMostInterestingObject(ClrHeap heap)
        {
            foreach (ClrSegment seg in heap.Segments.OrderByDescending(s => s.ObjectRange.Length))
            {
                foreach (ClrObject obj in seg.EnumerateObjects().OrderByDescending(obj => obj.EnumerateReferenceAddresses().Count()))
                {
                    if (obj.IsFree)
                        continue;

                    if (obj.SyncBlock is null)
                        continue;

                    if (!obj.EnumerateReferenceAddresses().Any())
                        continue;

                    return obj;
                }
            }

            throw new InvalidDataException();
        }

        private static ClrObject FindArrayObject(ClrHeap heap)
        {
            return heap.Segments.OrderByDescending(s => s.ObjectRange.Length).SelectMany(seg => seg.EnumerateObjects()).First(obj => !obj.IsFree && obj.IsArray);
        }
    }
}
