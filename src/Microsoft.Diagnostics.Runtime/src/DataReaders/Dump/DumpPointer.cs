// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Immutable pointer into the dump file. Has associated size for runtime checking.
    /// </summary>
    internal unsafe struct DumpPointer
    {
        // This is dangerous because its lets you create a new arbitrary dump pointer.
        public static DumpPointer DangerousMakeDumpPointer(IntPtr rawPointer, uint size)
        {
            return new DumpPointer(rawPointer, size);
        }

        // Private ctor used to create new pointers from existing ones.
        private DumpPointer(IntPtr rawPointer, uint size)
        {
            _pointer = rawPointer;
            _size = size;
        }

        /// <summary>
        /// Returns a DumpPointer to the same memory, but associated with a smaller size.
        /// </summary>
        /// <param name="size">smaller size to shrink the pointer to.</param>
        /// <returns>new DumpPointer</returns>
        public DumpPointer Shrink(uint size)
        {
            // Can't use this to grow.
            EnsureSizeRemaining(size);

            return new DumpPointer(_pointer, _size - size);
        }

        public DumpPointer Adjust(uint delta)
        {
            EnsureSizeRemaining(delta);
            IntPtr pointer = new IntPtr(_pointer.ToInt64() + delta);

            return new DumpPointer(pointer, _size - delta);
        }

        public DumpPointer Adjust(ulong delta64)
        {
            uint delta = (uint)delta64;
            EnsureSizeRemaining(delta);
            ulong ptr = unchecked((ulong)_pointer.ToInt64()) + delta;
            IntPtr pointer = new IntPtr(unchecked((long)ptr));

            return new DumpPointer(pointer, _size - delta);
        }

        /// <summary>
        /// Copy numberBytesToCopy from the DumpPointer into &amp;destinationBuffer[indexDestination].
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="destinationBufferSizeInBytes"></param>
        /// <param name="numberBytesToCopy"></param>
        public void Copy(IntPtr dest, uint destinationBufferSizeInBytes, uint numberBytesToCopy)
        {
            // Esnure that both source and destination are large enough.
            EnsureSizeRemaining(numberBytesToCopy);
            if (numberBytesToCopy > destinationBufferSizeInBytes)
                throw new ArgumentException("Buffer too small");

            RawCopy(_pointer, dest, numberBytesToCopy);
        }

        // All of the Marshal.Copy methods copy to arrays. We need to copy between IntPtrs. 
        // Provide a friendly wrapper over a raw pinvoke to RtlMoveMemory.
        // Note that we actually want a copy, but RtlCopyMemory is a macro and compiler intrinisic 
        // that we can't pinvoke to.
        private static void RawCopy(IntPtr src, IntPtr dest, uint numBytes)
        {
            DumpReader.RtlMoveMemory(dest, src, new IntPtr(numBytes));
        }

        internal ulong GetUlong()
        {
            return *((ulong*)_pointer.ToPointer());
        }

        internal uint GetDword()
        {
            return *((uint*)_pointer.ToPointer());
        }

        public void Copy(byte[] dest, int offset, int numberBytesToCopy)
        {
            // Esnure that both source and destination are large enough.
            EnsureSizeRemaining((uint)numberBytesToCopy);

            Marshal.Copy(_pointer, dest, offset, numberBytesToCopy);
        }

        /// <summary>
        /// Copy raw bytes to buffer
        /// </summary>
        /// <param name="destinationBuffer">buffer to copy to.</param>
        /// <param name="sizeBytes">
        /// number of bytes to copy. Caller ensures the destinationBuffer
        /// is large enough
        /// </param>
        public void Copy(IntPtr destinationBuffer, uint sizeBytes)
        {
            EnsureSizeRemaining(sizeBytes);
            RawCopy(_pointer, destinationBuffer, sizeBytes);
        }

        public int ReadInt32()
        {
            EnsureSizeRemaining(4);
            return Marshal.ReadInt32(_pointer);
        }

        public long ReadInt64()
        {
            EnsureSizeRemaining(8);
            return Marshal.ReadInt64(_pointer);
        }

        public uint ReadUInt32()
        {
            EnsureSizeRemaining(4);
            return (uint)Marshal.ReadInt32(_pointer);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StructUInt64
        {
            public readonly ulong Value;
        }

        public ulong ReadUInt64()
        {
            EnsureSizeRemaining(8);
            return (ulong)Marshal.ReadInt64(_pointer);
        }

        public string ReadAsUnicodeString(int lengthChars)
        {
            int lengthBytes = lengthChars * 2;

            EnsureSizeRemaining((uint)lengthBytes);
            string s = Marshal.PtrToStringUni(_pointer, lengthChars);
            return s;
        }

        public T PtrToStructure<T>(uint offset)
        {
            return Adjust(offset).PtrToStructure<T>();
        }

        public T PtrToStructureAdjustOffset<T>(ref uint offset)
        {
            T ret = Adjust(offset).PtrToStructure<T>();
            offset += (uint)Marshal.SizeOf(ret);
            return ret;
        }

        /// <summary>
        /// Marshal this into a managed structure, and do bounds checks.
        /// </summary>
        /// <typeparam name="T">Type of managed structure to marshal as</typeparam>
        /// <returns>a managed copy of the structure</returns>
        public T PtrToStructure<T>()
        {
            // Runtime check to ensure we have enough space in the minidump. This should
            // always be safe for well formed dumps.
            uint size = (uint)Marshal.SizeOf(typeof(T));
            EnsureSizeRemaining(size);

            T element = (T)Marshal.PtrToStructure(_pointer, typeof(T));
            return element;
        }

        private void EnsureSizeRemaining(uint requestedSize)
        {
            if (requestedSize > _size)
                throw new ClrDiagnosticsException("The given crash dump is in an incorrect format.", ClrDiagnosticsExceptionKind.CrashDumpError);
        }

        // The actual raw pointer into the dump file (which is memory mapped into this process space.
        // We can directly dereference this to get data. 
        // We need to ensure that the pointer points into the dump file (and not stray memory).
        // 
        // Buffer safety is enforced through the type-system. The only way to get a DumpPointer is:
        // 1) From the mapped file. Pointer, Size provided by File-system APIs. This describes the
        //    largest possible region.
        // 2) From a Minidump stream. Pointer,Size are provided by MiniDumpReadDumpStream.

        // 3) From shrinking operations on existing dump-pointers. These operations return a
        //   DumpPointer that refers to a subset of the original. Since the original DumpPointer
        //   is in ranage, any subset must be in range too.
        //     Adjust(x) - moves pointer += x, shrinks size -= x. 
        //     Shrink(x) - size-= x.
        // 
        // All read operations in the dump-file then go through a DumpPointer, which validates
        // that there is enough size to fill the request.
        // All read operatiosn are still dangerous because there is no way that we can enforce that the data is 
        // what we expect it to be. However, since all operations are bounded, we should at worst
        // return corrupted data, but never read outside the dump-file.
        private readonly IntPtr _pointer;

        // This is a 4-byte integer, which limits the dump operations to 4 gb. If we want to
        // handle dumps larger than that, we need to make this a 8-byte integer, (ulong), but that
        // then widens all of the DumpPointer structures.
        // Alternatively, we could make this an IntPtr so that it's 4-bytes on 32-bit and 8-bytes on
        // 64-bit OSes. 32-bit OS can't load 4gb+ dumps anyways, so that may give us the best of
        // both worlds.
        // We explictly keep the size private because clients should not need to access it. Size
        // expectations are already described by the minidump format.
        private readonly uint _size;
    }
}