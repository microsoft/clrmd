// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng.Structs
{
    public enum MINIDUMP_STREAM_TYPE : uint
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
        HandleOperationListStream = 18,
        TokenStream = 19,
        JavaScriptDataStream = 20,
        SystemMemoryInfoStream = 21,
        ProcessVmCountersStream = 22,
        IptTraceStream = 23,
        ThreadNamesStream = 24,
        ceStreamNull = 0x8000,
        ceStreamSystemInfo = 0x8001,
        ceStreamException = 0x8002,
        ceStreamModuleList = 0x8003,
        ceStreamProcessList = 0x8004,
        ceStreamThreadList = 0x8005,
        ceStreamThreadContextList = 0x8006,
        ceStreamThreadCallStackList = 0x8007,
        ceStreamMemoryVirtualList = 0x8008,
        ceStreamMemoryPhysicalList = 0x8009,
        ceStreamBucketParameters = 0x800A,
        ceStreamProcessModuleMap = 0x800B,
        ceStreamDiagnosisList = 0x800C,
        LastReservedStream = 0xffff
    }
}