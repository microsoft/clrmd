// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.NETCore.Client;
using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    internal class LinuxSnapshotTarget : CustomDataTarget
    {
        private readonly int _pid;
        private readonly string _filename;

        public LinuxSnapshotTarget(IDataReader reader, int pid, string filename) : base(reader)
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
            catch
            {
            }
        }

        public override string ToString() => $"{_filename} (snapshot of pid:{_pid:x})";

        public static LinuxSnapshotTarget CreateSnapshotFromProcess(int pid)
        {
            string? dumpPath = Path.GetTempFileName();
            try
            {
                try
                {
                    DiagnosticsClient client = new DiagnosticsClient(pid);
                    client.WriteDump(DumpType.Full, dumpPath, logDumpGeneration: false);
                }
                catch (ServerErrorException sxe)
                {
                    throw new ArgumentException($"Unable to create a snapshot of process {pid:x}.", sxe);
                }

#pragma warning disable CA2000 // Dispose objects before losing scope
                LinuxSnapshotTarget result = new LinuxSnapshotTarget(new CoredumpReader(dumpPath, File.OpenRead(dumpPath)), pid, dumpPath);
                dumpPath = null;
                return result;
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            finally
            {
                if (dumpPath != null)
                    File.Delete(dumpPath);
            }
        }
    }
}
