// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    ///  Class to represent chunks of memory from the target.
    ///  To add support for mapping files in and pretending they were part
    ///  of the dump, say for a MinidumpNormal when you can find the module
    ///  image on disk, you'd fall back on the image contents when 
    ///  ReadPartialMemory failed checking the chunks from the dump.
    ///  Practically speaking, that fallback could be in the
    ///  implementation of ICorDebugDataTarget.ReadVirtual.
    ///  Keep in mind this list presumes there are no overlapping chunks.
    /// </summary>
    internal class MinidumpMemoryChunks
    {
        private MinidumpMemory64List _memory64List;
        private MinidumpMemoryList _memoryList;
        private MinidumpMemoryChunk[] _chunks;
        private readonly DumpPointer _dumpStream;
        private readonly MINIDUMP_STREAM_TYPE _listType;

        public ulong Size(ulong i)
        {
            return _chunks[i].Size;
        }

        public ulong RVA(ulong i)
        {
            return _chunks[i].RVA;
        }

        public ulong StartAddress(ulong i)
        {
            return _chunks[i].TargetStartAddress;
        }

        public ulong EndAddress(ulong i)
        {
            return _chunks[i].TargetEndAddress;
        }

        public int GetChunkContainingAddress(ulong address)
        {
            MinidumpMemoryChunk targetChunk = new MinidumpMemoryChunk {TargetStartAddress = address};
            int index = Array.BinarySearch(_chunks, targetChunk);
            if (index >= 0)
            {
                Debug.Assert(_chunks[index].TargetStartAddress == address);
                return index; // exact match will contain the address
            }

            if (~index != 0)
            {
                int possibleIndex = Math.Min(_chunks.Length, ~index) - 1;
                if (_chunks[possibleIndex].TargetStartAddress <= address &&
                    _chunks[possibleIndex].TargetEndAddress > address)
                    return possibleIndex;
            }

            return -1;
        }

        public MinidumpMemoryChunks(DumpPointer rawStream, MINIDUMP_STREAM_TYPE type)
        {
            Count = 0;
            _memory64List = null;
            _memoryList = null;
            _listType = MINIDUMP_STREAM_TYPE.UnusedStream;

            if (type != MINIDUMP_STREAM_TYPE.MemoryListStream &&
                type != MINIDUMP_STREAM_TYPE.Memory64ListStream)
            {
                throw new ClrDiagnosticsException("Type must be either MemoryListStream or Memory64ListStream", ClrDiagnosticsExceptionKind.CrashDumpError);
            }

            _listType = type;
            _dumpStream = rawStream;
            if (MINIDUMP_STREAM_TYPE.Memory64ListStream == type)
            {
                InitFromMemory64List();
            }
            else
            {
                InitFromMemoryList();
            }
        }

        private void InitFromMemory64List()
        {
            _memory64List = new MinidumpMemory64List(_dumpStream);

            RVA64 currentRVA = _memory64List.BaseRva;
            ulong count = _memory64List.Count;

            // Initialize all chunks.
            MINIDUMP_MEMORY_DESCRIPTOR64 tempMD;
            List<MinidumpMemoryChunk> chunks = new List<MinidumpMemoryChunk>();
            for (ulong i = 0; i < count; i++)
            {
                tempMD = _memory64List.GetElement((uint)i);
                MinidumpMemoryChunk chunk = new MinidumpMemoryChunk
                {
                    Size = tempMD.DataSize,
                    TargetStartAddress = tempMD.StartOfMemoryRange,
                    TargetEndAddress = tempMD.StartOfMemoryRange + tempMD.DataSize,
                    RVA = currentRVA.Value
                };

                currentRVA.Value += tempMD.DataSize;
                chunks.Add(chunk);
            }

            chunks.Sort();
            SplitAndMergeChunks(chunks);
            _chunks = chunks.ToArray();
            Count = (ulong)chunks.Count;

            ValidateChunks();
        }

        public void InitFromMemoryList()
        {
            _memoryList = new MinidumpMemoryList(_dumpStream);
            uint count = _memoryList.Count;

            MINIDUMP_MEMORY_DESCRIPTOR tempMD;
            List<MinidumpMemoryChunk> chunks = new List<MinidumpMemoryChunk>();
            for (ulong i = 0; i < count; i++)
            {
                MinidumpMemoryChunk chunk = new MinidumpMemoryChunk();
                tempMD = _memoryList.GetElement((uint)i);
                chunk.Size = tempMD.Memory.DataSize;
                chunk.TargetStartAddress = tempMD.StartOfMemoryRange;
                chunk.TargetEndAddress = tempMD.StartOfMemoryRange + tempMD.Memory.DataSize;
                chunk.RVA = tempMD.Memory.Rva.Value;
                chunks.Add(chunk);
            }

            chunks.Sort();
            SplitAndMergeChunks(chunks);
            _chunks = chunks.ToArray();
            Count = (ulong)chunks.Count;

            ValidateChunks();
        }

        public ulong Count { get; private set; }

        private void SplitAndMergeChunks(List<MinidumpMemoryChunk> chunks)
        {
            for (int i = 1; i < chunks.Count; i++)
            {
                MinidumpMemoryChunk prevChunk = chunks[i - 1];
                MinidumpMemoryChunk curChunk = chunks[i];

                // we already sorted
                Debug.Assert(prevChunk.TargetStartAddress <= curChunk.TargetStartAddress);

                // there is some overlap
                if (prevChunk.TargetEndAddress > curChunk.TargetStartAddress)
                {
                    // the previous chunk completely covers this chunk rendering it useless
                    if (prevChunk.TargetEndAddress >= curChunk.TargetEndAddress)
                    {
                        chunks.RemoveAt(i);
                        i--;
                    }
                    // previous chunk partially covers this one so we will remove the front
                    // of this chunk and resort it if needed
                    else
                    {
                        ulong overlap = prevChunk.TargetEndAddress - curChunk.TargetStartAddress;
                        curChunk.TargetStartAddress += overlap;
                        curChunk.RVA += overlap;
                        curChunk.Size -= overlap;

                        // now that we changes the start address it might not be sorted anymore
                        // find the correct index
                        int newIndex = i;
                        for (; newIndex < chunks.Count - 1; newIndex++)
                        {
                            if (curChunk.TargetStartAddress <= chunks[newIndex + 1].TargetStartAddress)
                                break;
                        }

                        if (newIndex != i)
                        {
                            chunks.RemoveAt(i);
                            chunks.Insert(newIndex - 1, curChunk);
                            i--;
                        }
                    }
                }
            }
        }

        private void ValidateChunks()
        {
            for (ulong i = 0; i < Count; i++)
            {
                if (_chunks[i].Size != _chunks[i].TargetEndAddress - _chunks[i].TargetStartAddress ||
                    _chunks[i].TargetStartAddress > _chunks[i].TargetEndAddress)
                {
                    throw new ClrDiagnosticsException(
                        "Unexpected inconsistency error in dump memory chunk " + i
                        + " with target base address " + _chunks[i].TargetStartAddress + ".",
                        ClrDiagnosticsExceptionKind.CrashDumpError);
                }

                // If there's a next to compare to, and it's a MinidumpWithFullMemory, then we expect
                // that the RVAs & addresses will all be sorted in the dump.
                // MinidumpWithFullMemory stores things in a Memory64ListStream.
                if (i < Count - 1 && _listType == MINIDUMP_STREAM_TYPE.Memory64ListStream &&
                    (_chunks[i].RVA >= _chunks[i + 1].RVA ||
                    _chunks[i].TargetEndAddress > _chunks[i + 1].TargetStartAddress))
                {
                    throw new ClrDiagnosticsException(
                        "Unexpected relative addresses inconsistency between dump memory chunks "
                        + i + " and " + (i + 1) + ".",
                        ClrDiagnosticsExceptionKind.CrashDumpError);
                }

                // Because we sorted and split/merged entries we can expect them to be increasing and non-overlapping
                if (i < Count - 1 && _chunks[i].TargetEndAddress > _chunks[i + 1].TargetStartAddress)
                {
                    throw new ClrDiagnosticsException("Unexpected overlap between memory chunks", ClrDiagnosticsExceptionKind.CrashDumpError);
                }
            }
        }
    }
}