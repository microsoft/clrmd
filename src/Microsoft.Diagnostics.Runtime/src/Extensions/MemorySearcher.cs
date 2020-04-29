// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A public extension methods to support searching an IMemoryReader for a given span.
    /// </summary>
    internal static class MemorySearcher
    {
        /// <summary>
        /// Searches memory from startAddress to endAddress, looking for the memory specified by `searchFor`.  Note
        /// that this is NOT meant to be used to search the entire address space.  This method will attempt to read
        /// all memory from startAddress to endAddress, so providing very large ranges of memory will make this take
        /// a long time.
        /// </summary>
        /// <param name="reader">The memory reader to search through.</param>
        /// <param name="startAddress">The address to start searching memory.</param>
        /// <param name="length">The length of memory to search.</param>
        /// <param name="searchFor">The memory to search for.</param>
        /// <returns>The address of the value if found, 0 if not found.</returns>
        public static ulong SearchMemory(this IMemoryReader reader, ulong startAddress, int length, ReadOnlySpan<byte> searchFor)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            // While not strictly disallowed, providing a sanity check to make sure the user isn't
            // searching from 0 to ulong.MaxValue is a good idea.
            if (startAddress == 0)
                throw new ArgumentException($"{nameof(startAddress)} must be within allocated memory.");

            if (length <= searchFor.Length)
                return 0;

            ulong endAddress = startAddress + (uint)length;

            // Reading memory is slow, we want to read in reasonably large chunks.
            byte[] array = ArrayPool<byte>.Shared.Rent(768 + searchFor.Length);
            try
            {
                while (startAddress < endAddress)
                {
                    int bytesRemaining = (int)(endAddress - startAddress);
                    if (bytesRemaining < searchFor.Length)
                        break;

                    Span<byte> buffer = array.AsSpan(0, Math.Min(bytesRemaining, array.Length));
                    if (!reader.Read(startAddress, buffer, out int read) || read < searchFor.Length)
                    {
                        startAddress += (uint)searchFor.Length;
                        continue;
                    }

                    buffer = buffer.Slice(0, read);
                    int index = buffer.IndexOf(searchFor);
                    if (index >= 0)
                        return startAddress + (uint)index;

                    // To keep the code simple, we'll re-read the last bit of the buffer each time instead of
                    // block copying the leftover bits at the end and having to keep track of the buffer offset
                    // along the way.
                    int increment = buffer.Length - searchFor.Length;
                    if (increment <= 0)
                        increment = searchFor.Length;

                    startAddress += (uint)increment;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }

            return 0;
        }
    }
}
