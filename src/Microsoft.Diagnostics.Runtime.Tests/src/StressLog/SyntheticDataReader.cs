// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// An <see cref="IDataReader"/> backed by an in-memory address-to-bytes
    /// dictionary. Reads return however many bytes are available starting at
    /// the requested address, up to the buffer length, mimicking
    /// <c>ReadProcessMemory</c> semantics for short reads.
    /// </summary>
    internal sealed class SyntheticDataReader : IDataReader
    {
        private readonly Dictionary<ulong, byte[]> _segments;

        public SyntheticDataReader(Dictionary<ulong, byte[]> segments)
            : this(segments, pointerSize: 8, architecture: Architecture.X64)
        {
        }

        public SyntheticDataReader(Dictionary<ulong, byte[]> segments, int pointerSize, Architecture architecture)
        {
            _segments = segments;
            PointerSize = pointerSize;
            Architecture = architecture;
        }

        public string DisplayName => "synthetic";
        public bool IsThreadSafe => true;
        public OSPlatform TargetPlatform => OSPlatform.Windows;
        public Architecture Architecture { get; }
        public int ProcessId => 0;
        public IEnumerable<ModuleInfo> EnumerateModules() => Array.Empty<ModuleInfo>();
        public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context) => false;
        public IEnumerable<uint> EnumerateAllThreads() => Array.Empty<uint>();
        public void FlushCachedData() { }
        public int PointerSize { get; }

        public int Read(ulong address, Span<byte> buffer)
        {
            foreach (KeyValuePair<ulong, byte[]> kv in _segments)
            {
                ulong start = kv.Key;
                ulong end = start + (ulong)kv.Value.Length;
                if (address >= start && address < end)
                {
                    int srcOffset = (int)(address - start);
                    int avail = kv.Value.Length - srcOffset;
                    int copy = Math.Min(avail, buffer.Length);
                    kv.Value.AsSpan(srcOffset, copy).CopyTo(buffer);
                    return copy;
                }
            }
            return 0;
        }

        public bool Read<T>(ulong address, out T value) where T : unmanaged
        {
            Span<byte> buf = stackalloc byte[Marshal.SizeOf<T>()];
            int got = Read(address, buf);
            if (got != buf.Length) { value = default; return false; }
            value = MemoryMarshal.Read<T>(buf);
            return true;
        }

        public T Read<T>(ulong address) where T : unmanaged
            => Read(address, out T value) ? value : default;

        public bool ReadPointer(ulong address, out ulong value)
        {
            if (PointerSize == 4)
            {
                if (Read(address, out uint v32)) { value = v32; return true; }
                value = 0; return false;
            }
            return Read(address, out value);
        }
        public ulong ReadPointer(ulong address) => ReadPointer(address, out ulong v) ? v : 0UL;
    }
}
