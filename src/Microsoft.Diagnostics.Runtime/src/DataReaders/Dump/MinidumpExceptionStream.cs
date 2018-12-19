// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// The struct that holds contents of a dump's MINIDUMP_STREAM_TYPE.ExceptionStream
    /// which is a MINIDUMP_EXCEPTION_STREAM.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal class MINIDUMP_EXCEPTION_STREAM
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

            if (ExceptionRecord.ExceptionInformation.Length != DumpNative.EXCEPTION_MAXIMUM_PARAMETERS)
            {
                throw new ClrDiagnosticsException(
                    "Crash dump error: Expected to find " + DumpNative.EXCEPTION_MAXIMUM_PARAMETERS +
                    " exception params, but found " +
                    ExceptionRecord.ExceptionInformation.Length + " instead.",
                    ClrDiagnosticsExceptionKind.CrashDumpError);
            }

            for (int i = 0; i < DumpNative.EXCEPTION_MAXIMUM_PARAMETERS; i++)
            {
                ExceptionRecord.ExceptionInformation[i] = dump.PtrToStructureAdjustOffset<ulong>(ref offset);
            }

            ThreadContext.DataSize = dump.PtrToStructureAdjustOffset<uint>(ref offset);
            ThreadContext.Rva.Value = dump.PtrToStructureAdjustOffset<uint>(ref offset);
        }
    }
}