// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Well-known format strings shipped by the runtime. We byte-compare
    /// against these to identify GC-history messages without parsing the
    /// format string text.
    /// </summary>
    /// <remarks>
    /// The constants here mirror <c>src/coreclr/inc/gcmsg.inl</c> and the
    /// <c>TaskSwitchMsg</c> string in <c>stresslog.h</c>. They are part of
    /// the runtime's stable wire format; do not rewrap or retype the strings
    /// without coordinating with the runtime team.
    /// </remarks>
    internal static class StressLogKnownFormats
    {
        public static readonly byte[] GcStart =
            Encoding.ASCII.GetBytes("{ =========== BEGINGC %d, (requested generation = %lu, collect_classes = %lu) ==========\n");

        public static readonly byte[] GcEnd =
            Encoding.ASCII.GetBytes("========== ENDGC %d (gen = %lu, collect_classes = %lu) ===========}\n");

        public static readonly byte[] GcRoot =
            Encoding.ASCII.GetBytes("    GC Root %p RELOCATED %p -> %p  MT = %pT\n");

        public static readonly byte[] GcRootPromote =
            Encoding.ASCII.GetBytes("    IGCHeap::Promote: Promote GC Root *%p = %p MT = %pT\n");

        public static readonly byte[] GcPlugMove =
            Encoding.ASCII.GetBytes("GC_HEAP RELOCATING Objects in heap within range [%p %p) by -0x%x bytes\n");

        public static readonly byte[] TaskSwitch =
            Encoding.ASCII.GetBytes("StressLog TaskSwitch Marker\n");

        public static StressLogKnownFormat Identify(ReadOnlySpan<byte> formatBytes)
        {
            if (formatBytes.SequenceEqual(GcStart))       return StressLogKnownFormat.GcStart;
            if (formatBytes.SequenceEqual(GcEnd))         return StressLogKnownFormat.GcEnd;
            if (formatBytes.SequenceEqual(GcRoot))        return StressLogKnownFormat.GcRoot;
            if (formatBytes.SequenceEqual(GcRootPromote)) return StressLogKnownFormat.GcRootPromote;
            if (formatBytes.SequenceEqual(GcPlugMove))    return StressLogKnownFormat.GcPlugMove;
            if (formatBytes.SequenceEqual(TaskSwitch))    return StressLogKnownFormat.TaskSwitch;
            return StressLogKnownFormat.None;
        }
    }
}
