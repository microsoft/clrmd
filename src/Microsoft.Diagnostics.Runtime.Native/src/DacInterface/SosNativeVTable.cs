// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    internal readonly struct ISOSNativeVTable
    {
        public readonly IntPtr Flush;

        public readonly IntPtr GetThreadStoreData;
        private readonly IntPtr GetThreadAddress;
        public readonly IntPtr GetThreadData;
        public readonly IntPtr GetCurrentExceptionObject;

        public readonly IntPtr GetObjectData;
        public readonly IntPtr GetEETypeData;

        private readonly IntPtr GetGcHeapAnalyzeData;
        public readonly IntPtr GetGCHeapData;
        public readonly IntPtr GetGCHeapList;
        public readonly IntPtr GetGCHeapDetails;
        public readonly IntPtr GetGCHeapStaticData;
        public readonly IntPtr GetGCHeapSegmentData;
        public readonly IntPtr GetFreeEEType;

        private readonly IntPtr DumpGCInfo;
        private readonly IntPtr DumpEHInfo;

        private readonly IntPtr DumpStackObjects;
        public readonly IntPtr TraverseStackRoots;
        public readonly IntPtr TraverseStaticRoots;
        public readonly IntPtr TraverseHandleTable;
        private readonly IntPtr TraverseHandleTableFiltered;

        public readonly IntPtr GetCodeHeaderData;
        public readonly IntPtr GetModuleList;

        private readonly IntPtr GetStressLogAddress;
        private readonly IntPtr GetStressLogData;
        private readonly IntPtr EnumStressLogMessages;
        private readonly IntPtr EnumStressLogMemRanges;

        private readonly IntPtr UpdateDebugEventFilter;

        private readonly IntPtr UpdateCurrentExceptionNotificationFrame;
        private readonly IntPtr EnumGcStressStatsInfo;
    }
}