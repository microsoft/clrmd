// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Runtime.MacOS;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class MacOSSnapshotTarget : CustomDataTarget
    {
        private readonly int _pid;
        private readonly string _filename;

        private MacOSSnapshotTarget(IDataReader reader, int pid, string filename) : base(reader, null)
        {
            _pid = pid;
            _filename = filename;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            try
            {
                File.Delete(_filename);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
        }

        public override string ToString() => $"{_filename} (snapshot of pid:{_pid:x})";

        public static MacOSSnapshotTarget CreateSnapshotFromProcess(int pid)
        {
            string? dumpPath = Path.GetTempFileName();
            try
            {
                try
                {
                    DiagnosticsClient client = new(pid);
                    client.WriteDump(DumpType.Full, dumpPath, logDumpGeneration: false);
                }
                catch (ServerErrorException sxe)
                {
                    throw new ArgumentException($"Unable to create a snapshot of process {pid:x}.", sxe);
                }

                MacOSSnapshotTarget result = new(new MachOCoreReader(dumpPath, File.OpenRead(dumpPath), leaveOpen: false), pid, dumpPath);
                dumpPath = null;
                return result;
            }
            finally
            {
                if (dumpPath != null)
                    File.Delete(dumpPath);
            }
        }
    }
}