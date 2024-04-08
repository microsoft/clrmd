// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng.Structs
{
    public enum MINIDUMP_STREAM_TYPE : uint
    {
        UnusedStream = 0u,
        ReservedStream0 = 1u,
        ReservedStream1 = 2u,
        ThreadListStream = 3u,
        ModuleListStream = 4u,
        MemoryListStream = 5u,
        ExceptionStream = 6u,
        SystemInfoStream = 7u,
        ThreadExListStream = 8u,
        Memory64ListStream = 9u,
        CommentStreamA = 10u,
        CommentStreamW = 11u,
        HandleDataStream = 12u,
        FunctionTableStream = 13u,
        UnloadedModuleListStream = 14u,
        MiscInfoStream = 15u,
        MemoryInfoListStream = 16u,
        ThreadInfoListStream = 17u,
        HandleOperationListStream = 18u,
        TokenStream = 19u,
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