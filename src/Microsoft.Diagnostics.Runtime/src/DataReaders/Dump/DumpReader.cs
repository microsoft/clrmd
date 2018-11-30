// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
//  
//  This provides a minidump reader for managed code.
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// This provides a managed wrapper over the unmanaged dump-reading APIs in DbgHelp.dll.
// 
// ** This has several advantages:
// - type-safe wrappers
// - marshal minidump data-structures into the proper managed types (System.String,
// System.DateTime, System.Version, System.OperatingSystem, etc)
// 
// This does not validate aganst corrupted dumps. 
// 
// ** This is not a complete set of wrappers. 
// Other potentially interesting things to expose from the dump file:
// - the header. (Get flags, Version)
// - Exception stream (find last exception thrown)
// - 
// 
// ** Potential Performance improvements
// This was first prototyped in unmanaged C++, and was significantly faster there. 
// This is  not optimized for performance at all. It currently does not use unsafe C# and so has
// no pointers to structures and so has high costs from Marshal.PtrToStructure(typeof(T)) instead of
// just using T*. 
// This could probably be speed up signficantly (approaching the speed of the C++ prototype) by using unsafe C#. 
// 
// More data could be cached. A full dump may be 80 MB+, so caching extra data for faster indexing
// and lookup, especially for reading the memory stream.
// However, the app consuming the DumpReader is probably doing a lot of caching on its own, so
// extra caching in the dump reader may make microbenchmarks go faster, but just increase the
// working set and complexity of real clients.
// 
//     

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Read contents of a minidump.
    /// If we have a 32-bit dump, then there's an addressing collision possible.
    /// OS debugging code sign extends 32 bit wide addresses into 64 bit wide addresses.
    /// The CLR does not sign extend, thus you cannot round-trip target addresses exposed by this class.
    /// Currently we read these addresses once and don't hand them back, so it's not an issue.
    /// </summary>
    internal class DumpReader : IDisposable
    {
        protected internal static class DumpNative
        {
            /// <summary>
            /// Type of stream within the minidump.
            /// </summary>
            public enum MINIDUMP_STREAM_TYPE
            {
                UnusedStream = 0,
                ReservedStream0 = 1,
                ReservedStream1 = 2,
                ThreadListStream = 3,
                ModuleListStream = 4,
                MemoryListStream = 5,
                ExceptionStream = 6,
                SystemInfoStream = 7,
                ThreadExListStream = 8,
                Memory64ListStream = 9,
                CommentStreamA = 10,
                CommentStreamW = 11,
                HandleDataStream = 12,
                FunctionTableStream = 13,
                UnloadedModuleListStream = 14,
                MiscInfoStream = 15,
                MemoryInfoListStream = 16,
                ThreadInfoListStream = 17,
                LastReservedStream = 0xffff
            }

            /// <summary>
            /// Remove the OS sign-extension from a target address.
            /// </summary>
            private static ulong ZeroExtendAddress(ulong addr)
            {
                // Since we only support debugging targets of the same bitness, we can presume that
                // the target dump process's bitness matches ours and strip the high bits.
                if (IntPtr.Size == 4)
                    return addr &= 0x00000000ffffffff;

                return addr;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct MINIDUMP_HEADER
            {
                public readonly uint Singature;
                public readonly uint Version;
                public readonly uint NumberOfStreams;
                public readonly uint StreamDirectoryRva;
                public readonly uint CheckSum;
                public readonly uint TimeDateStamp;
                public readonly ulong Flags;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct MINIDUMP_DIRECTORY
            {
                public readonly MINIDUMP_STREAM_TYPE StreamType;
                public readonly uint DataSize;
                public readonly uint Rva;
            }

            private const uint MINIDUMP_SIGNATURE = 0x504d444d;
            private const uint MINIDUMP_VERSION = 0xa793;
            private const uint MiniDumpWithFullMemoryInfo = 0x0002;

            public static bool IsMiniDump(IntPtr pbase)
            {
                var header = (MINIDUMP_HEADER)Marshal.PtrToStructure(pbase, typeof(MINIDUMP_HEADER));

                return (header.Flags & MiniDumpWithFullMemoryInfo) == 0;
            }

            public static bool MiniDumpReadDumpStream(IntPtr pBase, MINIDUMP_STREAM_TYPE type, out IntPtr streamPointer, out uint cbStreamSize)
            {
                var header = (MINIDUMP_HEADER)Marshal.PtrToStructure(pBase, typeof(MINIDUMP_HEADER));

                streamPointer = IntPtr.Zero;
                cbStreamSize = 0;

                // todo: throw dump format exception here:
                if (header.Singature != MINIDUMP_SIGNATURE || (header.Version & 0xffff) != MINIDUMP_VERSION)
                    return false;

                var sizeOfDirectory = Marshal.SizeOf(typeof(MINIDUMP_DIRECTORY));
                var dirs = pBase.ToInt64() + (int)header.StreamDirectoryRva;
                for (var i = 0; i < (int)header.NumberOfStreams; ++i)
                {
                    var dir = (MINIDUMP_DIRECTORY)Marshal.PtrToStructure(new IntPtr(dirs + i * sizeOfDirectory), typeof(MINIDUMP_DIRECTORY));
                    if (dir.StreamType != type)
                        continue;

                    streamPointer = new IntPtr(pBase.ToInt64() + (int)dir.Rva);
                    cbStreamSize = dir.DataSize;
                    return true;
                }

                return false;
            }

            // RVAs are offsets into the minidump.
            [StructLayout(LayoutKind.Sequential)]
            public struct RVA
            {
                public uint Value;

                public bool IsNull => Value == 0;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RVA64
            {
                public ulong Value;
            }

            /// <summary>
            /// Describes a data stream within the minidump
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_LOCATION_DESCRIPTOR
            {
                /// <summary>
                /// Size of the stream in bytes.
                /// </summary>
                public uint DataSize;

                /// <summary>
                /// Offset (in bytes) from the start of the minidump to the data stream.
                /// </summary>
                public RVA Rva;

                /// <summary>
                /// True iff the data is missing.
                /// </summary>
                public bool IsNull => DataSize == 0 || Rva.IsNull;
            }

            /// <summary>
            /// Describes a data stream within the minidump
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_LOCATION_DESCRIPTOR64
            {
                /// <summary>
                /// Size of the stream in bytes.
                /// </summary>
                public ulong DataSize;

                /// <summary>
                /// Offset (in bytes) from the start of the minidump to the data stream.
                /// </summary>
                public RVA64 Rva;
            }

            /// <summary>
            /// Describes a range of memory in the target.
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_DESCRIPTOR
            {
                public const int SizeOf = 16;

                /// <summary>
                /// Starting Target address of the memory range.
                /// </summary>
                private readonly ulong _startofmemoryrange;
                public ulong StartOfMemoryRange => ZeroExtendAddress(_startofmemoryrange);

                /// <summary>
                /// Location in minidump containing the memory corresponding to StartOfMemoryRage
                /// </summary>
                public MINIDUMP_LOCATION_DESCRIPTOR Memory;
            }

            /// <summary>
            /// Describes a range of memory in the target.
            /// </summary>
            /// <remarks>
            /// This is used for full-memory minidumps where
            /// all of the raw memory is laid out sequentially at the
            /// end of the dump.  There is no need for individual RVAs
            /// as the RVA is the base RVA plus the sum of the preceeding
            /// data blocks.
            /// </remarks>
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_DESCRIPTOR64
            {
                public const int SizeOf = 16;

                /// <summary>
                /// Starting Target address of the memory range.
                /// </summary>
                private readonly ulong _startofmemoryrange;
                public ulong StartOfMemoryRange => ZeroExtendAddress(_startofmemoryrange);

                /// <summary>
                /// Size of memory in bytes.
                /// </summary>
                public ulong DataSize;
            }

            // From ntxcapi_x.h, for example
            public const uint EXCEPTION_MAXIMUM_PARAMETERS = 15;

            /// <summary>
            /// The struct that holds an EXCEPTION_RECORD
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            public class MINIDUMP_EXCEPTION
            {
                public uint ExceptionCode;
                public uint ExceptionFlags;
                public ulong ExceptionRecord;

                private ulong _exceptionaddress;
                public ulong ExceptionAddress
                {
                    get => ZeroExtendAddress(_exceptionaddress);
                    set => _exceptionaddress = value;
                }

                public uint NumberParameters;
                public uint __unusedAlignment;
                public ulong[] ExceptionInformation;

                public MINIDUMP_EXCEPTION()
                {
                    ExceptionInformation = new ulong[EXCEPTION_MAXIMUM_PARAMETERS];
                }
            }

            /// <summary>
            /// The struct that holds contents of a dump's MINIDUMP_STREAM_TYPE.ExceptionStream
            /// which is a MINIDUMP_EXCEPTION_STREAM.
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            public class MINIDUMP_EXCEPTION_STREAM
            {
                public uint ThreadId;
                public uint __alignment;
                public MINIDUMP_EXCEPTION ExceptionRecord;
                public MINIDUMP_LOCATION_DESCRIPTOR ThreadContext;

                public MINIDUMP_EXCEPTION_STREAM(DumpPointer dump)
                {
                    uint offset = 0;
                    ThreadId = dump.PtrToStructureAdjustOffset<uint>(ref offset);
                    __alignment = dump.PtrToStructureAdjustOffset<uint>(ref offset);

                    ExceptionRecord = new MINIDUMP_EXCEPTION();

                    ExceptionRecord.ExceptionCode = dump.PtrToStructureAdjustOffset<uint>(ref offset);
                    ExceptionRecord.ExceptionFlags = dump.PtrToStructureAdjustOffset<uint>(ref offset);
                    ExceptionRecord.ExceptionRecord = dump.PtrToStructureAdjustOffset<ulong>(ref offset);
                    ExceptionRecord.ExceptionAddress = dump.PtrToStructureAdjustOffset<ulong>(ref offset);
                    ExceptionRecord.NumberParameters = dump.PtrToStructureAdjustOffset<uint>(ref offset);
                    ExceptionRecord.__unusedAlignment = dump.PtrToStructureAdjustOffset<uint>(ref offset);

                    if (ExceptionRecord.ExceptionInformation.Length != EXCEPTION_MAXIMUM_PARAMETERS)
                    {
                        throw new ClrDiagnosticsException(
                            "Crash dump error: Expected to find " + EXCEPTION_MAXIMUM_PARAMETERS +
                            " exception params, but found " +
                            ExceptionRecord.ExceptionInformation.Length + " instead.",
                            ClrDiagnosticsException.HR.CrashDumpError);
                    }

                    for (var i = 0; i < EXCEPTION_MAXIMUM_PARAMETERS; i++)
                    {
                        ExceptionRecord.ExceptionInformation[i] = dump.PtrToStructureAdjustOffset<ulong>(ref offset);
                    }

                    ThreadContext.DataSize = dump.PtrToStructureAdjustOffset<uint>(ref offset);
                    ThreadContext.Rva.Value = dump.PtrToStructureAdjustOffset<uint>(ref offset);
                }
            }

            /// <summary>
            /// Describes system information about the system the dump was taken on.
            /// This is returned by the MINIDUMP_STREAM_TYPE.SystemInfoStream stream.
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            public class MINIDUMP_SYSTEM_INFO
            {
                // There are existing managed types that represent some of these fields.
                // Provide both the raw imports, and the managed wrappers to make these easier to
                // consume from managed code.

                // These 3 fields are the same as in the SYSTEM_INFO structure from GetSystemInfo().
                // As of .NET 2.0, there is no existing managed object that represents these.
                public ProcessorArchitecture ProcessorArchitecture;
                public ushort ProcessorLevel; // only used for display purposes
                public ushort ProcessorRevision;

                public byte NumberOfProcessors;
                public byte ProductType;

                // These next 4 fields plus CSDVersionRva are the same as the OSVERSIONINFO structure from GetVersionEx().
                // This can be represented as a System.Version.
                public uint MajorVersion;
                public uint MinorVersion;
                public uint BuildNumber;

                // This enum is the same value as System.PlatformId.
                public int PlatformId;

                // RVA to a CSDVersion string in the string table.
                // This would be a string like "Service Pack 1".
                public RVA CSDVersionRva;

                // Remaining fields are not imported.

                //
                // Helper methods
                //

                public Version Version
                {
                    // System.Version is a managed abstraction on top of version numbers.
                    get
                    {
                        var v = new Version((int)MajorVersion, (int)MinorVersion, (int)BuildNumber);
                        return v;
                    }
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct VS_FIXEDFILEINFO
            {
                public uint dwSignature; /* e.g. 0xfeef04bd */
                public uint dwStrucVersion; /* e.g. 0x00000042 = "0.42" */
                public uint dwFileVersionMS; /* e.g. 0x00030075 = "3.75" */
                public uint dwFileVersionLS; /* e.g. 0x00000031 = "0.31" */
                public uint dwProductVersionMS; /* e.g. 0x00030010 = "3.10" */
                public uint dwProductVersionLS; /* e.g. 0x00000031 = "0.31" */
                public uint dwFileFlagsMask; /* = 0x3F for version "0.42" */
                public uint dwFileFlags; /* e.g. VFF_DEBUG | VFF_PRERELEASE */
                public uint dwFileOS; /* e.g. VOS_DOS_WINDOWS16 */
                public uint dwFileType; /* e.g. VFT_DRIVER */
                public uint dwFileSubtype; /* e.g. VFT2_DRV_KEYBOARD */

                // Timestamps would be useful, but they're generally missing (0).
                public uint dwFileDateMS; /* e.g. 0 */
                public uint dwFileDateLS; /* e.g. 0 */
            }

            // Default Pack of 8 makes this struct 4 bytes too long
            // and so retrieving the last one will fail.
            [StructLayout(LayoutKind.Sequential, Pack = 4)]
            public sealed class MINIDUMP_MODULE
            {
                /// <summary>
                /// Address that module is loaded within target.
                /// </summary>
                private ulong _baseofimage;
                public ulong BaseOfImage => ZeroExtendAddress(_baseofimage);

                /// <summary>
                /// Size of image within memory copied from IMAGE_OPTIONAL_HEADER.SizeOfImage.
                /// Note that this is usually different than the file size.
                /// </summary>
                public uint SizeOfImage;

                /// <summary>
                /// Checksum, copied from IMAGE_OPTIONAL_HEADER.CheckSum. May be 0 if not optional
                /// header is not available.
                /// </summary>
                public uint CheckSum;

                /// <summary>
                /// TimeStamp in Unix 32-bit time_t format. Copied from IMAGE_FILE_HEADER.TimeDateStamp
                /// </summary>
                public uint TimeDateStamp;

                /// <summary>
                /// RVA within minidump of the string containing the full path of the module.
                /// </summary>
                public RVA ModuleNameRva;

                internal VS_FIXEDFILEINFO VersionInfo;

                private MINIDUMP_LOCATION_DESCRIPTOR _cvRecord;

                private MINIDUMP_LOCATION_DESCRIPTOR _miscRecord;

                private ulong _reserved0;
                private ulong _reserved1;

                /// <summary>
                /// Gets TimeDateStamp as a DateTime. This is based off a 32-bit value and will overflow in 2038.
                /// This is not the same as the timestamps on the file.
                /// </summary>
                public DateTime Timestamp
                {
                    get
                    {
                        // TimeDateStamp is a unix time_t structure (32-bit value).
                        // UNIX timestamps are in seconds since January 1, 1970 UTC. It is a 32-bit number
                        // Win32 FileTimes represents the number of 100-nanosecond intervals since January 1, 1601 UTC.
                        // We can create a System.DateTime from a FileTime.
                        // 
                        // See explanation here: http://blogs.msdn.com/oldnewthing/archive/2003/09/05/54806.aspx
                        // and here http://support.microsoft.com/default.aspx?scid=KB;en-us;q167296
                        var win32FileTime = 10000000 * (long)TimeDateStamp + 116444736000000000;
                        return DateTime.FromFileTimeUtc(win32FileTime);
                    }
                }
            }

            // Gotten from MiniDumpReadDumpStream via streamPointer
            // This is a var-args structure defined as:
            //   ULONG32 NumberOfModules;  
            //   MINIDUMP_MODULE Modules[];
            public class MINIDUMP_MODULE_LIST : MinidumpArray<MINIDUMP_MODULE>
            {
                internal MINIDUMP_MODULE_LIST(DumpPointer streamPointer)
                    : base(streamPointer, MINIDUMP_STREAM_TYPE.ModuleListStream)
                {
                }
            }

            /// <summary>
            /// Raw MINIDUMP_THREAD structure imported from DbgHelp.h
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            public class MINIDUMP_THREAD
            {
                public uint ThreadId;

                // 0 if thread is not suspended.
                public uint SuspendCount;

                public uint PriorityClass;
                public uint Priority;

                // Target Address of Teb (Thread Environment block)
                private ulong _teb;
                public ulong Teb => ZeroExtendAddress(_teb);

                /// <summary>
                /// Describes the memory location of the thread's raw stack.
                /// </summary>
                public MINIDUMP_MEMORY_DESCRIPTOR Stack;

                public MINIDUMP_LOCATION_DESCRIPTOR ThreadContext;

                public virtual bool HasBackingStore()
                {
                    return false;
                }

                public virtual MINIDUMP_MEMORY_DESCRIPTOR BackingStore
                {
                    get => throw new MissingMemberException("MINIDUMP_THREAD has no backing store!");
                    set => throw new MissingMemberException("MINIDUMP_THREAD has no backing store!");
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            public sealed class MINIDUMP_THREAD_EX : MINIDUMP_THREAD
            {
                public override bool HasBackingStore()
                {
                    return true;
                }

                public override MINIDUMP_MEMORY_DESCRIPTOR BackingStore { get; set; }
            }

            // Minidumps have a common variable length list structure for modules and threads implemented
            // as an array.
            // MINIDUMP_MODULE_LIST, MINIDUMP_THREAD_LIST, and MINIDUMP_THREAD_EX_LIST are the three streams
            // which use this implementation.
            // Others are similar in idea, such as MINIDUMP_THREAD_INFO_LIST, but are not the
            // same implementation and will not work with this class.  Thus, although this class
            // is generic, it's currently tightly bound to the implementation of those three streams.
            // This is a var-args structure defined as:
            //   ULONG32 NumberOfNodesInList;
            //   T ListNodes[];
            public class MinidumpArray<T>
            {
                protected MinidumpArray(DumpPointer streamPointer, MINIDUMP_STREAM_TYPE streamType)
                {
                    if (streamType != MINIDUMP_STREAM_TYPE.ModuleListStream &&
                        streamType != MINIDUMP_STREAM_TYPE.ThreadListStream &&
                        streamType != MINIDUMP_STREAM_TYPE.ThreadExListStream)
                    {
                        throw new ClrDiagnosticsException("MinidumpArray does not support this stream type.", ClrDiagnosticsException.HR.CrashDumpError);
                    }

                    _streamPointer = streamPointer;
                }

                private DumpPointer _streamPointer;

                public uint Count => _streamPointer.ReadUInt32();

                public T GetElement(uint idx)
                {
                    if (idx > Count)
                    {
                        // Since the callers here are internal, a request out of range means a
                        // corrupted dump file.
                        throw new ClrDiagnosticsException("Dump error: index " + idx + "is out of range.", ClrDiagnosticsException.HR.CrashDumpError);
                    }

                    // Although the Marshal.SizeOf(...) is not necessarily correct, it is nonetheless
                    // how we're going to pull the bytes back from the dump in PtrToStructure
                    // and so if it's wrong we have to fix it up anyhow.  This would have to be an incorrect
                    //  code change on our side anyhow; these are public native structs whose size is fixed.
                    // MINIDUMP_MODULE    : 0n108 bytes
                    // MINIDUMP_THREAD    : 0n48 bytes
                    // MINIDUMP_THREAD_EX : 0n64 bytes
                    const uint OffsetOfArray = 4;
                    var offset = OffsetOfArray + idx * (uint)Marshal.SizeOf(typeof(T));

                    var element = _streamPointer.PtrToStructure<T>(+offset);
                    return element;
                }
            }

            public interface IMinidumpThreadList
            {
                uint Count();
                MINIDUMP_THREAD GetElement(uint idx);
            }

            /// <summary>
            /// List of Threads in the minidump.
            /// </summary>
            public class MINIDUMP_THREAD_LIST<T> : MinidumpArray<T>, IMinidumpThreadList
                where T : MINIDUMP_THREAD
            {
                internal MINIDUMP_THREAD_LIST(DumpPointer streamPointer, MINIDUMP_STREAM_TYPE streamType)
                    : base(streamPointer, streamType)
                {
                    if (streamType != MINIDUMP_STREAM_TYPE.ThreadListStream &&
                        streamType != MINIDUMP_STREAM_TYPE.ThreadExListStream)
                        throw new ClrDiagnosticsException("Only ThreadListStream and ThreadExListStream are supported.", ClrDiagnosticsException.HR.CrashDumpError);
                }

                // IMinidumpThreadList
                public new MINIDUMP_THREAD GetElement(uint idx)
                {
                    var t = base.GetElement(idx);
                    return t;
                }

                public new uint Count()
                {
                    return base.Count;
                }
            }

            public class MinidumpMemoryChunk : IComparable<MinidumpMemoryChunk>
            {
                public ulong Size;
                public ulong TargetStartAddress;
                // TargetEndAddress is the first byte beyond the end of this chunk.
                public ulong TargetEndAddress;
                public ulong RVA;

                public int CompareTo(MinidumpMemoryChunk other)
                {
                    return TargetStartAddress.CompareTo(other.TargetStartAddress);
                }
            }

            // Class to represent chunks of memory from the target.
            // To add support for mapping files in and pretending they were part
            // of the dump, say for a MinidumpNormal when you can find the module
            // image on disk, you'd fall back on the image contents when 
            // ReadPartialMemory failed checking the chunks from the dump.
            // Practically speaking, that fallback could be in the
            // implementation of ICorDebugDataTarget.ReadVirtual.
            // Keep in mind this list presumes there are no overlapping chunks.
            public class MinidumpMemoryChunks
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
                    var targetChunk = new MinidumpMemoryChunk {TargetStartAddress = address};
                    var index = Array.BinarySearch(_chunks, targetChunk);
                    if (index >= 0)
                    {
                        Debug.Assert(_chunks[index].TargetStartAddress == address);
                        return index; // exact match will contain the address
                    }

                    if (~index != 0)
                    {
                        var possibleIndex = Math.Min(_chunks.Length, ~index) - 1;
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
                        throw new ClrDiagnosticsException("Type must be either MemoryListStream or Memory64ListStream", ClrDiagnosticsException.HR.CrashDumpError);
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

                    var currentRVA = _memory64List.BaseRva;
                    var count = _memory64List.Count;

                    // Initialize all chunks.
                    MINIDUMP_MEMORY_DESCRIPTOR64 tempMD;
                    var chunks = new List<MinidumpMemoryChunk>();
                    for (ulong i = 0; i < count; i++)
                    {
                        tempMD = _memory64List.GetElement((uint)i);
                        var chunk = new MinidumpMemoryChunk
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
                    var count = _memoryList.Count;

                    MINIDUMP_MEMORY_DESCRIPTOR tempMD;
                    var chunks = new List<MinidumpMemoryChunk>();
                    for (ulong i = 0; i < count; i++)
                    {
                        var chunk = new MinidumpMemoryChunk();
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
                    for (var i = 1; i < chunks.Count; i++)
                    {
                        var prevChunk = chunks[i - 1];
                        var curChunk = chunks[i];

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
                                var overlap = prevChunk.TargetEndAddress - curChunk.TargetStartAddress;
                                curChunk.TargetStartAddress += overlap;
                                curChunk.RVA += overlap;
                                curChunk.Size -= overlap;

                                // now that we changes the start address it might not be sorted anymore
                                // find the correct index
                                var newIndex = i;
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
                                ClrDiagnosticsException.HR.CrashDumpError);
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
                                ClrDiagnosticsException.HR.CrashDumpError);
                        }

                        // Because we sorted and split/merged entries we can expect them to be increasing and non-overlapping
                        if (i < Count - 1 && _chunks[i].TargetEndAddress > _chunks[i + 1].TargetStartAddress)
                        {
                            throw new ClrDiagnosticsException("Unexpected overlap between memory chunks", ClrDiagnosticsException.HR.CrashDumpError);
                        }
                    }
                }
            }

            // Usually about 300-500 elements long.
            // This does not have the right layout to use MinidumpArray
            public class MinidumpMemory64List
            {
                // Declaration of unmanaged structure is
                //   public ulong NumberOfMemoryRanges; // offset 0
                //   public RVA64 BaseRVA; // offset 8
                //   MINIDUMP_MEMORY_DESCRIPTOR64[]; // var-length embedded array
                public MinidumpMemory64List(DumpPointer streamPointer)
                {
                    _streamPointer = streamPointer;
                }

                private DumpPointer _streamPointer;

                public ulong Count
                {
                    get
                    {
                        var count = _streamPointer.ReadInt64();
                        return (ulong)count;
                    }
                }
                public RVA64 BaseRva
                {
                    get
                    {
                        var rva = _streamPointer.PtrToStructure<RVA64>(8);
                        return rva;
                    }
                }

                public MINIDUMP_MEMORY_DESCRIPTOR64 GetElement(uint idx)
                {
                    // Embededded array starts at offset 16.
                    var offset = 16 + idx * MINIDUMP_MEMORY_DESCRIPTOR64.SizeOf;
                    return _streamPointer.PtrToStructure<MINIDUMP_MEMORY_DESCRIPTOR64>(offset);
                }
            }

            public class MinidumpMemoryList
            {
                // Declaration of unmanaged structure is
                //   public ulong NumberOfMemoryRanges; // offset 0
                //   MINIDUMP_MEMORY_DESCRIPTOR[]; // var-length embedded array
                public MinidumpMemoryList(DumpPointer streamPointer)
                {
                    _streamPointer = streamPointer;
                }

                private DumpPointer _streamPointer;

                public uint Count
                {
                    get
                    {
                        long count = _streamPointer.ReadInt32();
                        return (uint)count;
                    }
                }

                public MINIDUMP_MEMORY_DESCRIPTOR GetElement(uint idx)
                {
                    // Embededded array starts at offset 4.
                    var offset = 4 + idx * MINIDUMP_MEMORY_DESCRIPTOR.SizeOf;
                    return _streamPointer.PtrToStructure<MINIDUMP_MEMORY_DESCRIPTOR>(offset);
                }
            }

            // If the dump doesn't have memory contents, we can try to load the file
            // off disk and report as if memory contents were present.
            // Run through loader to simplify getting the in-memory layout correct, rather than using a FileStream
            // and playing around with trying to mimic the loader.
            public class LoadedFileMemoryLookups
            {
                private readonly Dictionary<string, SafeLoadLibraryHandle> _files;

                public LoadedFileMemoryLookups()
                {
                    _files = new Dictionary<string, SafeLoadLibraryHandle>();
                }

                public unsafe void GetBytes(string fileName, ulong offset, IntPtr destination, uint bytesRequested, ref uint bytesWritten)
                {
                    bytesWritten = 0;
                    IntPtr file;
                    // Did we already attempt to load this file?
                    // Only makes one attempt to load a file.
                    if (!_files.ContainsKey(fileName))
                    {
                        //TODO: real code here to get the relocations right without loading would be nice, but
                        // that's a significant amount of code - especially if you intend to compensate for linker bugs.
                        // The easiest way to accomplish this would be to build on top of dbgeng.dll which already
                        // does all this for you.  Then you can also use dbghelp & get all your module and symbol
                        // loading for free, with full integration with symbol servers.
                        //
                        // In the meantime, this  doesn't actually exec any code from the module
                        // we load.  Mdbg should be done loading modules for itself, so if we happen to load some
                        // module in common with mdbg we'll be fine because this call will be second.
                        // Lifetime issues could be important if we load some module here and do not release it back
                        // to the OS before mdbg loads it subsequently to execute it.
                        // Also, note that rebasing will not be correct, so raw assembly addresses will be relative
                        // to the base address of the module in mdbg's process, not the base address in the dump.
                        file = WindowsFunctions.NativeMethods.LoadLibraryEx(fileName, 0, WindowsFunctions.NativeMethods.LoadLibraryFlags.DontResolveDllReferences);
                        _files[fileName] = new SafeLoadLibraryHandle(file);
                        //TODO: Attempted file load order is NOT guaranteed, so the uncertainty will make output order non-deterministic.
                        // Find/create an appropriate global verbosity setting.
                        /*
                        if (file.Equals(IntPtr.Zero))
                        {
                            String warning = "DataTarget: failed to load \"" + fileName + "\"";
                            CommandBase.Write(MDbgOutputConstants.StdOutput, warning, 0, warning.Length);
                        }
                        else 
                        {
                            CommandBase.WriteOutput("DataTarget: loaded \"" + fileName + "\"");
                        }
                        */
                    }
                    else
                    {
                        file = _files[fileName].BaseAddress;
                    }

                    // Did we actually succeed loading this file?
                    if (!file.Equals(IntPtr.Zero))
                    {
                        file = new IntPtr((byte*)file.ToPointer() + offset);
                        InternalGetBytes(file, destination, bytesRequested, ref bytesWritten);
                    }
                }

                private unsafe void InternalGetBytes(IntPtr src, IntPtr dest, uint bytesRequested, ref uint bytesWritten)
                {
                    // Do the raw copy.
                    var pSrc = (byte*)src.ToPointer();
                    var pDest = (byte*)dest.ToPointer();
                    for (bytesWritten = 0; bytesWritten < bytesRequested; bytesWritten++)
                    {
                        pDest[bytesWritten] = pSrc[bytesWritten];
                    }
                }
            }
        }

        // Get a DumpPointer from a MINIDUMP_LOCATION_DESCRIPTOR
        protected internal DumpPointer TranslateDescriptor(DumpNative.MINIDUMP_LOCATION_DESCRIPTOR location)
        {
            // A Location has both an RVA and Size. If we just TranslateRVA, then that would be a
            // DumpPointer associated with a larger size (to the end of the dump-file). 
            var p = TranslateRVA(location.Rva);
            p.Shrink(location.DataSize);
            return p;
        }

        /// <summary>
        /// Translates from an RVA to Dump Pointer.
        /// </summary>
        /// <param name="rva">RVA within the dump</param>
        /// <returns>DumpPointer representing RVA.</returns>
        protected internal DumpPointer TranslateRVA(ulong rva)
        {
            return _base.Adjust(rva);
        }

        /// <summary>
        /// Translates from an RVA to Dump Pointer.
        /// </summary>
        /// <param name="rva">RVA within the dump</param>
        /// <returns>DumpPointer representing RVA.</returns>
        protected internal DumpPointer TranslateRVA(DumpNative.RVA rva)
        {
            return _base.Adjust(rva.Value);
        }

        /// <summary>
        /// Translates from an RVA to Dump Pointer.
        /// </summary>
        /// <param name="rva">RVA within the dump</param>
        /// <returns>DumpPointer representing RVA.</returns>
        protected internal DumpPointer TranslateRVA(DumpNative.RVA64 rva)
        {
            return _base.Adjust(rva.Value);
        }

        /// <summary>
        /// Gets a MINIDUMP_STRING at the given RVA as an System.String.
        /// </summary>
        /// <param name="rva">RVA of MINIDUMP_STRING</param>
        /// <returns>System.String representing contents of MINIDUMP_STRING at the given RVA</returns>
        protected internal string GetString(DumpNative.RVA rva)
        {
            var p = TranslateRVA(rva);
            return GetString(p);
        }

        /// <summary>
        /// Gets a MINIDUMP_STRING at the given DumpPointer as an System.String.
        /// </summary>
        /// <param name="ptr">DumpPointer to a MINIDUMP_STRING</param>
        /// <returns>
        /// System.String representing contents of MINIDUMP_STRING at the given location
        /// in the dump
        /// </returns>
        protected internal string GetString(DumpPointer ptr)
        {
            EnsureValid();

            // Minidump string is defined as:
            // typedef struct _MINIDUMP_STRING {
            //   ULONG32 Length;         // Length in bytes of the string
            //    WCHAR   Buffer [0];     // Variable size buffer
            // } MINIDUMP_STRING, *PMINIDUMP_STRING;
            var lengthBytes = ptr.ReadInt32();

            ptr = ptr.Adjust(4); // move past the Length field

            var lengthChars = lengthBytes / 2;
            var s = ptr.ReadAsUnicodeString(lengthChars);
            return s;
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData data)
        {
            uint min = 0, max = (uint)_memoryChunks.Count - 1;

            while (min <= max)
            {
                var mid = (max + min) / 2;

                var targetStartAddress = _memoryChunks.StartAddress(mid);

                if (addr < targetStartAddress)
                {
                    max = mid - 1;
                }
                else
                {
                    var targetEndAddress = _memoryChunks.EndAddress(mid);
                    if (targetEndAddress < addr)
                    {
                        min = mid + 1;
                    }
                    else
                    {
                        data = new VirtualQueryData(targetStartAddress, _memoryChunks.Size(mid));
                        return true;
                    }
                }
            }

            data = new VirtualQueryData();
            return false;
        }

        public IEnumerable<VirtualQueryData> EnumerateMemoryRanges(ulong startAddress, ulong endAddress)
        {
            for (ulong i = 0; i < _memoryChunks.Count; i++)
            {
                var targetStartAddress = _memoryChunks.StartAddress(i);
                var targetEndAddress = _memoryChunks.EndAddress(i);

                if (targetEndAddress < startAddress)
                    continue;
                if (endAddress < targetStartAddress)
                    continue;

                var size = _memoryChunks.Size(i);
                yield return new VirtualQueryData(targetStartAddress, size);
            }
        }

        /// <summary>
        /// Read memory from the dump file and return results in newly allocated buffer
        /// </summary>
        /// <param name="targetAddress">target address in dump to read length bytes from</param>
        /// <param name="length">number of bytes to read</param>
        /// <returns>newly allocated byte array containing dump memory</returns>
        /// <remarks>All memory requested must be readable or it throws.</remarks>
        public byte[] ReadMemory(ulong targetAddress, int length)
        {
            var buffer = new byte[length];
            ReadMemory(targetAddress, buffer, length);
            return buffer;
        }

        /// <summary>
        /// Read memory from the dump file and copy into the buffer
        /// </summary>
        /// <param name="targetAddress">target address in dump to read buffer.Length bytets from</param>
        /// <param name="buffer">destination buffer to copy target memory to.</param>
        /// <param name="cbRequestSize">count of bytes to read</param>
        /// <remarks>All memory requested must be readable or it throws.</remarks>
        public void ReadMemory(ulong targetAddress, byte[] buffer, int cbRequestSize)
        {
            var h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                ReadMemory(targetAddress, h.AddrOfPinnedObject(), (uint)cbRequestSize);
            }
            finally
            {
                h.Free();
            }
        }

        /// <summary>
        /// Read memory from target and copy it to the local buffer pointed to by
        /// destinationBuffer. Throw if any portion of the requested memory is unavailable.
        /// </summary>
        /// <param name="targetRequestStart">
        /// target address in dump file to copy
        /// destinationBufferSizeInBytes bytes from.
        /// </param>
        /// <param name="destinationBuffer">pointer to copy the memory to.</param>
        /// <param name="destinationBufferSizeInBytes">size of the destinationBuffer in bytes.</param>
        public void ReadMemory(ulong targetRequestStart, IntPtr destinationBuffer, uint destinationBufferSizeInBytes)
        {
            var bytesRead = ReadPartialMemory(targetRequestStart, destinationBuffer, destinationBufferSizeInBytes);
            if (bytesRead != destinationBufferSizeInBytes)
            {
                throw new ClrDiagnosticsException(
                    string.Format(
                        CultureInfo.CurrentUICulture,
                        "Memory missing at {0}. Could only read {1} bytes of {2} total bytes requested.",
                        targetRequestStart.ToString("x"),
                        bytesRead,
                        destinationBufferSizeInBytes),
                    ClrDiagnosticsException.HR.CrashDumpError);
            }
        }

        /*

        /// <summary>
        /// Read memory from the dump file and copy into the buffer
        /// </summary>
        /// <param name="targetAddress">target address in dump to read buffer.Length bytets from</param>
        /// <param name="buffer">destination buffer to copy target memory to.</param>
        /// <remarks>All memory requested must be readable or it throws.</remarks>
        public uint ReadPartialMemory(ulong targetAddress, byte[] buffer)
        {
            GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                uint cbRequestSize = (uint)buffer.Length;
                return ReadPartialMemory(targetAddress, h.AddrOfPinnedObject(), cbRequestSize);
            }
            finally
            {
                h.Free();
            }
        }
        
         */
        /// <summary>
        /// Read memory from target and copy it to the local buffer pointed to by destinationBuffer.
        /// </summary>
        /// <param name="targetRequestStart">
        /// target address in dump file to copy
        /// destinationBufferSizeInBytes bytes from.
        /// </param>
        /// <param name="destinationBuffer">pointer to copy the memory to.</param>
        /// <param name="destinationBufferSizeInBytes">size of the destinationBuffer in bytes.</param>
        /// <returns>Number of contiguous bytes successfuly copied into the destination buffer.</returns>
        public virtual uint ReadPartialMemory(ulong targetRequestStart, IntPtr destinationBuffer, uint destinationBufferSizeInBytes)
        {
            var bytesRead = ReadPartialMemoryInternal(
                targetRequestStart,
                destinationBuffer,
                destinationBufferSizeInBytes,
                0);
            return bytesRead;
        }

        internal ulong ReadPointerUnsafe(ulong addr)
        {
            var chunkIndex = _memoryChunks.GetChunkContainingAddress(addr);
            if (chunkIndex == -1)
                return 0;

            var chunk = TranslateRVA(_memoryChunks.RVA((uint)chunkIndex));
            var offset = addr - _memoryChunks.StartAddress((uint)chunkIndex);

            if (IntPtr.Size == 4)
                return chunk.Adjust(offset).GetDword();

            return chunk.Adjust(offset).GetUlong();
        }

        internal uint ReadDwordUnsafe(ulong addr)
        {
            var chunkIndex = _memoryChunks.GetChunkContainingAddress(addr);
            if (chunkIndex == -1)
                return 0;

            var chunk = TranslateRVA(_memoryChunks.RVA((uint)chunkIndex));
            var offset = addr - _memoryChunks.StartAddress((uint)chunkIndex);
            return chunk.Adjust(offset).GetDword();
        }

        public virtual int ReadPartialMemory(ulong targetRequestStart, byte[] destinationBuffer, int bytesRequested)
        {
            EnsureValid();

            if (bytesRequested <= 0)
                return 0;

            if (bytesRequested > destinationBuffer.Length)
                bytesRequested = destinationBuffer.Length;

            var bytesRead = 0;
            do
            {
                var chunkIndex = _memoryChunks.GetChunkContainingAddress(targetRequestStart + (uint)bytesRead);
                if (chunkIndex == -1)
                    break;

                var pointerCurrentChunk = TranslateRVA(_memoryChunks.RVA((uint)chunkIndex));
                var startAddr = targetRequestStart + (uint)bytesRead - _memoryChunks.StartAddress((uint)chunkIndex);
                var bytesAvailable = _memoryChunks.Size((uint)chunkIndex) - startAddr;

                Debug.Assert(bytesRequested >= bytesRead);
                var bytesToCopy = bytesRequested - bytesRead;
                if (bytesAvailable < (uint)bytesToCopy)
                    bytesToCopy = (int)bytesAvailable;

                Debug.Assert(bytesToCopy > 0);
                if (bytesToCopy == 0)
                    break;

                pointerCurrentChunk.Adjust(startAddr).Copy(destinationBuffer, bytesRead, bytesToCopy);
                bytesRead += bytesToCopy;
            } while (bytesRead < bytesRequested);

            return bytesRead;
        }

#pragma warning disable 0420
        private volatile bool _disposing;
        private volatile int _lock;

        private bool AcquireReadLock()
        {
            var result = 0;
            var value = 0;
            do
            {
                value = _lock;
                if (_disposing || value < 0)
                    return false;

                result = Interlocked.CompareExchange(ref _lock, value + 1, value);
            } while (result != value);

            return true;
        }

        private void ReleaseReadLock()
        {
            Interlocked.Decrement(ref _lock);
        }

        private bool AcquireWriteLock()
        {
            var result = 0;
            result = Interlocked.CompareExchange(ref _lock, -1, 0);
            while (result != 0)
            {
                Thread.Sleep(50);
                result = Interlocked.CompareExchange(ref _lock, -1, 0);
            }

            return true;
        }

        private void ReleaseWriteLock()
        {
            Interlocked.Increment(ref _lock);
        }

        // Since a MemoryListStream makes no guarantees that there aren't duplicate, overlapping, or wholly contained
        // memory regions, we need to handle that.  For the purposes of this code, we presume all memory regions
        // in the dump that cover a given VA have the correct (duplicate) contents.
        protected uint ReadPartialMemoryInternal(
            ulong targetRequestStart,
            IntPtr destinationBuffer,
            uint destinationBufferSizeInBytes,
            uint startIndex)
        {
            EnsureValid();

            if (destinationBufferSizeInBytes == 0)
                return 0;

            uint bytesRead = 0;
            do
            {
                var chunkIndex = _memoryChunks.GetChunkContainingAddress(targetRequestStart + bytesRead);
                if (chunkIndex == -1)
                    break;

                var pointerCurrentChunk = TranslateRVA(_memoryChunks.RVA((uint)chunkIndex));
                var idxStart = (uint)(targetRequestStart + bytesRead - _memoryChunks.StartAddress((uint)chunkIndex));
                var bytesAvailable = (uint)_memoryChunks.Size((uint)chunkIndex) - idxStart;
                var bytesNeeded = destinationBufferSizeInBytes - bytesRead;
                var bytesToCopy = Math.Min(bytesAvailable, bytesNeeded);

                Debug.Assert(bytesToCopy > 0);
                if (bytesToCopy == 0)
                    break;

                var dest = new IntPtr(destinationBuffer.ToInt64() + bytesRead);
                var destSize = destinationBufferSizeInBytes - bytesRead;
                pointerCurrentChunk.Adjust(idxStart).Copy(dest, destSize, bytesToCopy);
                bytesRead += bytesToCopy;
            } while (bytesRead < destinationBufferSizeInBytes);

            return bytesRead;
        }

        // Caching the chunks avoids the cost of Marshal.PtrToStructure on every single element in the memory list.
        // Empirically, this cache provides huge performance improvements for read memory.
        // This cache could be completey removed if we used unsafe C# and just had direct pointers
        // into the mapped dump file.
        protected DumpNative.MinidumpMemoryChunks _memoryChunks;
        // The backup lookup method for memory that's not in the dump is to try and load the memory
        // from the same file on disk.
        protected DumpNative.LoadedFileMemoryLookups _mappedFileMemory;

        /// <summary>
        /// ToString override.
        /// </summary>
        /// <returns>string description of the DumpReader.</returns>
        public override string ToString()
        {
            if (_file == null)
            {
                return "Empty";
            }

            return _file.Name;
        }

        public bool IsMinidump { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">filename to open dump file</param>
        public DumpReader(string path)
        {
            _file = File.OpenRead(path);
            var length = _file.Length;

            // The dump file may be many megabytes large, so we don't want to
            // read it all at once. Instead, doing a mapping.
            _fileMapping = CreateFileMapping(_file.SafeFileHandle, IntPtr.Zero, PageProtection.Readonly, 0, 0, null);

            if (_fileMapping.IsInvalid)
            {
                var error = Marshal.GetHRForLastWin32Error();
                Marshal.ThrowExceptionForHR(error, new IntPtr(-1));
            }

            _view = MapViewOfFile(_fileMapping, WindowsFunctions.NativeMethods.FILE_MAP_READ, 0, 0, IntPtr.Zero);
            if (_view.IsInvalid)
            {
                var error = Marshal.GetHRForLastWin32Error();
                Marshal.ThrowExceptionForHR(error, new IntPtr(-1));
            }

            _base = DumpPointer.DangerousMakeDumpPointer(_view.BaseAddress, (uint)length);

            //
            // Cache stuff
            //

            DumpPointer pStream;

            // System info.            
            pStream = GetStream(DumpNative.MINIDUMP_STREAM_TYPE.SystemInfoStream);
            _info = pStream.PtrToStructure<DumpNative.MINIDUMP_SYSTEM_INFO>();

            // Memory64ListStream is present in MinidumpWithFullMemory.
            if (TryGetStream(DumpNative.MINIDUMP_STREAM_TYPE.Memory64ListStream, out pStream))
            {
                _memoryChunks = new DumpNative.MinidumpMemoryChunks(pStream, DumpNative.MINIDUMP_STREAM_TYPE.Memory64ListStream);
            }
            else
            {
                // MiniDumpNormal doesn't have a Memory64ListStream, it has a MemoryListStream.
                pStream = GetStream(DumpNative.MINIDUMP_STREAM_TYPE.MemoryListStream);
                _memoryChunks = new DumpNative.MinidumpMemoryChunks(pStream, DumpNative.MINIDUMP_STREAM_TYPE.MemoryListStream);
            }

            _mappedFileMemory = new DumpNative.LoadedFileMemoryLookups();
            IsMinidump = DumpNative.IsMiniDump(_view.BaseAddress);
        }

        [Flags]
        private enum PageProtection : uint
        {
            NoAccess = 0x01,
            Readonly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            Guard = 0x100,
            NoCache = 0x200,
            WriteCombine = 0x400
        }

        // Call CloseHandle to clean up.
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeWin32Handle CreateFileMapping(
            SafeFileHandle hFile,
            IntPtr lpFileMappingAttributes,
            PageProtection flProtect,
            uint dwMaximumSizeHigh,
            uint dwMaximumSizeLow,
            string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeMapViewHandle MapViewOfFile(
            SafeWin32Handle hFileMappingObject,
            uint
                dwDesiredAccess,
            uint dwFileOffsetHigh,
            uint dwFileOffsetLow,
            IntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll")]
        internal static extern void RtlMoveMemory(IntPtr destination, IntPtr source, IntPtr numberBytes);

        /// <summary>
        /// Dispose method.
        /// </summary>
        public void Dispose()
        {
            // Clear any cached objects.
            _disposing = true;
            AcquireWriteLock();

            _info = null;
            _memoryChunks = null;
            _mappedFileMemory = null;

            // All resources are backed by safe-handles, so we don't need a finalizer.
            if (_fileMapping != null)
                _fileMapping.Close();

            if (_view != null)
                _view.Close();

            if (_file != null)
                _file.Dispose();
        }

        // Helper to ensure the object is not yet disposed.
        private void EnsureValid()
        {
            if (_file == null)
            {
                throw new ObjectDisposedException("DumpReader");
            }
        }

        private readonly FileStream _file;
        private readonly SafeWin32Handle _fileMapping;
        private readonly SafeMapViewHandle _view;

        // DumpPointer (raw pointer that's aware of remaining buffer size) for start of minidump. 
        // This is useful for computing RVAs.
        private DumpPointer _base;

        // Cached info
        private DumpNative.MINIDUMP_SYSTEM_INFO _info;

        /// <summary>
        /// Get a DumpPointer for the given stream. That can then be used to further decode the stream.
        /// </summary>
        /// <param name="type">type of stream to lookup</param>
        /// <returns>DumpPointer refering into the stream. </returns>
        private DumpPointer GetStream(DumpNative.MINIDUMP_STREAM_TYPE type)
        {
            if (!TryGetStream(type, out var stream))
                throw new ClrDiagnosticsException("Dump does not contain a " + type + " stream.", ClrDiagnosticsException.HR.CrashDumpError);

            return stream;
        }

        /// <summary>
        /// Get a DumpPointer for the given stream. That can then be used to further decode the stream.
        /// </summary>
        /// <param name="type">type of stream to lookup</param>
        /// <param name="stream">DumpPointer refering into the stream. </param>
        /// <returns>True if stream was succesfully retrived</returns>
        private bool TryGetStream(DumpNative.MINIDUMP_STREAM_TYPE type, out DumpPointer stream)
        {
            EnsureValid();

            var fOk = DumpNative.MiniDumpReadDumpStream(_view.BaseAddress, type, out var pStream, out var cbStreamSize);

            if (!fOk || IntPtr.Zero == pStream || cbStreamSize < 1)
            {
                stream = default;
                return false;
            }

            stream = DumpPointer.DangerousMakeDumpPointer(pStream, cbStreamSize);
            return true;
        }

        /// <summary>
        /// Version numbers of OS that this dump was taken on.
        /// </summary>
        public Version Version => _info.Version;

        /// <summary>
        /// The processor architecture that this dump was taken on.
        /// </summary>
        public ProcessorArchitecture ProcessorArchitecture
        {
            get
            {
                EnsureValid();
                return _info.ProcessorArchitecture;
            }
        }

        /// <summary>
        /// Get the thread for the given thread Id.
        /// </summary>
        /// <param name="threadId">thread Id to lookup.</param>
        /// <returns>
        /// a DumpThread object representing a thread in the dump whose thread id matches
        /// the requested id.
        /// </returns>
        public DumpThread GetThread(int threadId)
        {
            EnsureValid();
            var raw = GetRawThread(threadId);
            if (raw == null)
                return null;

            return new DumpThread(this, raw);
        }

        // Helper to get the thread list in the dump.
        private DumpNative.IMinidumpThreadList GetThreadList()
        {
            EnsureValid();

            DumpPointer pStream;

            DumpNative.MINIDUMP_STREAM_TYPE streamType;
            DumpNative.IMinidumpThreadList list;
            try
            {
                // On x86 and X64, we have the ThreadListStream.  On IA64, we have the ThreadExListStream.
                streamType = DumpNative.MINIDUMP_STREAM_TYPE.ThreadListStream;
                pStream = GetStream(streamType);
                list = new DumpNative.MINIDUMP_THREAD_LIST<DumpNative.MINIDUMP_THREAD>(pStream, streamType);
            }
            catch (ClrDiagnosticsException)
            {
                streamType = DumpNative.MINIDUMP_STREAM_TYPE.ThreadExListStream;
                pStream = GetStream(streamType);
                list = new DumpNative.MINIDUMP_THREAD_LIST<DumpNative.MINIDUMP_THREAD_EX>(pStream, streamType);
            }

            return list;
        }

        /// <summary>
        /// Enumerate all the native threads in the dump
        /// </summary>
        /// <returns>an enumerate of DumpThread objects</returns>
        public IEnumerable<DumpThread> EnumerateThreads()
        {
            var list = GetThreadList();
            var num = list.Count();

            for (uint i = 0; i < num; i++)
            {
                var rawThread = list.GetElement(i);
                yield return new DumpThread(this, rawThread);
            }
        }

        // Internal helper to get the raw Minidump thread object.
        // Throws if thread is not found.
        private DumpNative.MINIDUMP_THREAD GetRawThread(int threadId)
        {
            var list = GetThreadList();
            var num = list.Count();

            for (uint i = 0; i < num; i++)
            {
                var thread = list.GetElement(i);
                if (threadId == thread.ThreadId)
                {
                    return thread;
                }
            }

            return null;
        }

        /*
        /// <summary>
        /// Retrieve a thread context at the given location
        /// </summary>
        /// <param name="threadId">OS thread ID of the thread</param>
        /// <returns>a native context object representing the thread context</returns>
        internal NativeContext GetThreadContext(DumpReader.NativeMethods.MINIDUMP_LOCATION_DESCRIPTOR loc)
        {
            NativeContext context = ContextAllocator.GenerateContext();
            GetThreadContext(loc, context);
            return context;
        }

        /// <summary>
        /// Retrieve a thread context at the given location
        /// </summary>
        /// <param name="threadId">OS thread ID of the thread</param>
        /// <returns>a native context object representing the thread context</returns>
        internal void GetThreadContext(DumpReader.NativeMethods.MINIDUMP_LOCATION_DESCRIPTOR loc, NativeContext context)
        {
            using (IContextDirectAccessor w = context.OpenForDirectAccess())
            {
                GetThreadContext(loc, w.RawBuffer, w.Size);
            }
        }
        */
        internal void GetThreadContext(DumpNative.MINIDUMP_LOCATION_DESCRIPTOR loc, IntPtr buffer, int sizeBufferBytes)
        {
            if (loc.IsNull)
            {
                throw new ClrDiagnosticsException("Context not present", ClrDiagnosticsException.HR.CrashDumpError);
            }

            var pContext = TranslateDescriptor(loc);
            var sizeContext = (int)loc.DataSize;

            if (sizeBufferBytes < sizeContext)
            {
                // Context size doesn't match
                throw new ClrDiagnosticsException(
                    "Context size mismatch. Expected = 0x" + sizeBufferBytes.ToString("x") + ", Size in dump = 0x" + sizeContext.ToString("x"),
                    ClrDiagnosticsException.HR.CrashDumpError);
            }

            // Now copy from dump into buffer. 
            pContext.Copy(buffer, (uint)sizeContext);
        }

        // Internal helper to get the list of modules
        private DumpNative.MINIDUMP_MODULE_LIST GetModuleList()
        {
            EnsureValid();
            var pStream = GetStream(DumpNative.MINIDUMP_STREAM_TYPE.ModuleListStream);
            var list = new DumpNative.MINIDUMP_MODULE_LIST(pStream);

            return list;
        }

        private DumpNative.MINIDUMP_EXCEPTION_STREAM GetExceptionStream()
        {
            var pStream = GetStream(DumpNative.MINIDUMP_STREAM_TYPE.ExceptionStream);
            return new DumpNative.MINIDUMP_EXCEPTION_STREAM(pStream);
        }

        /// <summary>
        /// Check on whether there's an exception stream in the dump
        /// </summary>
        /// <returns> true iff there is a MINIDUMP_EXCEPTION_STREAM in the dump. </returns>
        public bool IsExceptionStream()
        {
            var ret = true;
            try
            {
                GetExceptionStream();
            }
            catch (ClrDiagnosticsException)
            {
                ret = false;
            }

            return ret;
        }

        /// <summary>
        /// Return the TID from the exception stream.
        /// </summary>
        /// <returns> The TID from the exception stream. </returns>
        public uint ExceptionStreamThreadId()
        {
            var es = GetExceptionStream();
            return es.ThreadId;
        }

        //todo
        /*
        public NativeContext ExceptionStreamThreadContext()
        {
            NativeMethods.MINIDUMP_EXCEPTION_STREAM es = GetExceptionStream();
            return GetThreadContext(es.ThreadContext);
        }
         */

        /// <summary>
        /// Lookup the first module in the target with a matching.
        /// </summary>
        /// <param name="nameModule">The name can either be a matching full name, or just shortname</param>
        /// <returns>The first DumpModule that has a matching name. </returns>
        public DumpModule LookupModule(string nameModule)
        {
            var list = GetModuleList();
            var num = list.Count;

            for (uint i = 0; i < num; i++)
            {
                var module = list.GetElement(i);
                var rva = module.ModuleNameRva;

                var ptr = TranslateRVA(rva);

                var name = GetString(ptr);
                if (nameModule == name ||
                    name.EndsWith(nameModule))
                {
                    return new DumpModule(this, module);
                }
            }

            return null;
        }

        /// <summary>
        /// Return the module containing the target address, or null if no match.
        /// </summary>
        /// <param name="targetAddress">address in target</param>
        /// <returns>
        /// Null if no match. Else a DumpModule such that the target address is in between the range specified
        /// by the DumpModule's .BaseAddress and .Size property
        /// </returns>
        /// <remarks>
        /// This can be useful for symbol lookups or for using module images to
        /// supplement memory read requests for minidumps.
        /// </remarks>
        public DumpModule TryLookupModuleByAddress(ulong targetAddress)
        {
            // This is an optimized lookup path, which avoids using IEnumerable or creating
            // unnecessary DumpModule objects.
            var list = GetModuleList();

            var num = list.Count;

            for (uint i = 0; i < num; i++)
            {
                var module = list.GetElement(i);
                var targetStart = module.BaseOfImage;
                var targetEnd = targetStart + module.SizeOfImage;
                if (targetStart <= targetAddress && targetEnd > targetAddress)
                {
                    return new DumpModule(this, module);
                }
            }

            return null;
        }

        /// <summary>
        /// Enumerate all the modules in the dump.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<DumpModule> EnumerateModules()
        {
            var list = GetModuleList();

            var num = list.Count;

            for (uint i = 0; i < num; i++)
            {
                var module = list.GetElement(i);
                yield return new DumpModule(this, module);
            }
        }

        /*
        public class DumpMemoryRead : IMemoryRead
        {
            public long Address { get; set; }
            public int BytesRequested { get; set; }
            public int BytesRead { get; set; }
            public DumpModule Module { get; set; }
            public FileSearchResult FileSearch { get; set; }
            public override string ToString()
            {
                return ToString("");
            }
            public string ToString(string format)
            {
                StringBuilder sb = new StringBuilder();
                if (BytesRead == 0)
                    sb.Append("Failed ");
                else if (BytesRead < BytesRequested)
                    sb.Append("Partial");
                else
                    sb.Append("Success");
                sb.Append(string.Format(" - 0x{0,-16:x}: 0x{1,-8:x} of 0x{2,-8:x} bytes read", Address, BytesRead, BytesRequested));
                if (format == "detailed")
                {
                    sb.AppendLine();
                    string source = "Dump memory";
                    if (Module != null)
                        source = string.Format("Image {0} (0x{1:x} - 0x{2:x})", Path.GetFileName(Module.FullName),
                            Module.BaseAddress, Module.BaseAddress + Module.Size);
                    sb.AppendLine("Source: " + source);
                    if (FileSearch != null && FileSearch.Path == null)
                    {
                        sb.AppendLine("Image search failed:");
                        sb.AppendLine(FileSearch.ToString());
                    }
                }
                return sb.ToString();
            }
        }
        */
    }
}