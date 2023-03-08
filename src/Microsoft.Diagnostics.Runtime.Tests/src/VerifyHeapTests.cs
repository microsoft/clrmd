using Microsoft.Diagnostics.Runtime.Utilities.DbgEng;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class VerifyHeapTests
    {
        [Fact]
        public void ObjectNotOnHeapTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDumpWithDbgEng(GCMode.Server);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IDebugDataSpaces spaces = GetDataReader(dt).DebugDataSpaces;

            ClrHeap heap = runtime.Heap;
            ClrObject obj = new(0x12345678, null);

            Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
            Assert.NotNull(result);

            Assert.Equal(ObjectCorruptionKind.ObjectNotOnTheHeap, result.Kind);
            Assert.Equal(obj, result.Object);
            Assert.Equal(0, result.Offset);
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

            WriteAndRun(spaces, obj - 4, (ushort)0, () =>
            {
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

            WriteAndRun(spaces, obj - 4, (ushort)0xcc, () =>
            {
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

            WriteAndRun(spaces, obj, 0xcccccc, () =>
            {
                Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
                Assert.NotNull(result);

                Assert.Equal(ObjectCorruptionKind.BadMethodTable, result.Kind);
                Assert.Equal(obj, result.Object);
                Assert.Equal(0, result.Offset); // MT offset is at 0
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

            foreach (var reference in obj.EnumerateReferencesWithFields())
            {
                uint offset = (uint)IntPtr.Size + (uint)reference.Offset;
                WriteAndRun(spaces, obj + offset, 0xcccccc, () =>
                {
                    Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
                    Assert.NotNull(result);

                    Assert.Equal(ObjectCorruptionKind.BadObjectReference, result.Kind);
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

            WriteAndRun(spaces, obj + (uint)IntPtr.Size, 0xcccccccc, () =>
            {
                Assert.True(heap.IsObjectCorrupted(obj, out ObjectCorruption result));
                Assert.NotNull(result);

                Assert.Equal(ObjectCorruptionKind.ObjectTooLarge, result.Kind);
                Assert.Equal(obj, result.Object);
                Assert.Equal(IntPtr.Size, result.Offset); // Array count is after the MethodTable
            });
        }

        private static unsafe void WriteAndRun<T>(IDebugDataSpaces spaces, ulong location, T value, Action action)
            where T: unmanaged
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
