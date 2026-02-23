// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Runtime.DataReaders;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class LinuxSnapshotTarget : ISnapshot, IDisposable
    {
        private readonly CoredumpReader _coreReader;
        private readonly string _filename;

        public IDataReader DataReader => _coreReader;

        public LinuxSnapshotTarget(int pid)
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

                _coreReader = new CoredumpReader(dumpPath, File.OpenRead(dumpPath), leaveOpen: false);
                _filename = dumpPath;
                dumpPath = null;
            }
            finally
            {
                if (dumpPath != null)
                    File.Delete(dumpPath);
            }
        }

        private bool _disposed;

        ~LinuxSnapshotTarget()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;

            if (disposing)
            {
                _coreReader.Dispose();
            }

            try
            {
                File.Delete(_filename);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
        }

        public void SaveSnapshot(string path, bool overwrite)
        {
            File.Copy(_filename, path, overwrite);
        }
    }
}