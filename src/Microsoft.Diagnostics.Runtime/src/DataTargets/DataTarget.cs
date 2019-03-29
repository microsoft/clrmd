// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A crash dump or live process to read out of.
    /// </summary>
    public abstract class DataTarget : IDisposable
    {
        /// <summary>
        /// A set of helper functions that are consistently implemented across all platforms.
        /// </summary>
        public static PlatformFunctions PlatformFunctions { get; }

        static DataTarget()
        {
#if !NET45
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                PlatformFunctions = new LinuxFunctions();
            else
#endif
            PlatformFunctions = new WindowsFunctions();
        }

        /// <summary>
        /// Creates a DataTarget from a crash dump.
        /// </summary>
        /// <param name="fileName">The crash dump's filename.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget LoadCrashDump(string fileName)
        {
            DbgEngDataReader reader = new DbgEngDataReader(fileName);
            return CreateFromReader(reader, reader.DebuggerInterface);
        }

        /// <summary>
        /// Creates a DataTarget from a coredump.  Note that since we have to load a native library (libmscordaccore.so)
        /// this must be run on a Linux machine.
        /// </summary>
        /// <param name="filename">The path to a core dump.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget LoadCoreDump(string filename)
        {
            CoreDumpReader reader = new CoreDumpReader(filename);
            return CreateFromReader(reader, null);
        }

        /// <summary>
        /// Creates a DataTarget from a crash dump, specifying the dump reader to use.
        /// </summary>
        /// <param name="fileName">The crash dump's filename.</param>
        /// <param name="dumpReader">The type of dump reader to use.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget LoadCrashDump(string fileName, CrashDumpReader dumpReader)
        {
            if (dumpReader == CrashDumpReader.DbgEng)
            {
                DbgEngDataReader reader = new DbgEngDataReader(fileName);
                return CreateFromReader(reader, reader.DebuggerInterface);
            }
            else
            {
                DumpDataReader reader = new DumpDataReader(fileName);
                return CreateFromReader(reader, null);
            }
        }

        /// <summary>
        /// Create an instance of DataTarget from a user defined DataReader
        /// </summary>
        /// <param name="reader">A user defined DataReader.</param>
        /// <returns>A new DataTarget instance.</returns>
        public static DataTarget CreateFromDataReader(IDataReader reader)
        {
            return CreateFromReader(reader, null);
        }

        private static DataTarget CreateFromReader(IDataReader reader, IDebugClient client)
        {
#if _TRACING
            reader = new TraceDataReader(reader);
#endif
            return new DataTargetImpl(reader, client);
        }

        /// <summary>
        /// Creates a data target from an existing IDebugClient interface.  If you created and attached
        /// a dbgeng based debugger to a process you may pass the IDebugClient RCW object to this function
        /// to create the DataTarget.
        /// </summary>
        /// <param name="client">The dbgeng IDebugClient object.  We will query interface on this for IDebugClient.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget CreateFromDebuggerInterface(IDebugClient client)
        {
            DbgEngDataReader reader = new DbgEngDataReader(client);
            DataTargetImpl dataTarget = new DataTargetImpl(reader, reader.DebuggerInterface);

            return dataTarget;
        }

        /// <summary>
        /// Invasively attaches to a live process.
        /// </summary>
        /// <param name="pid">The process ID of the process to attach to.</param>
        /// <param name="msecTimeout">Timeout in milliseconds.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget AttachToProcess(int pid, uint msecTimeout)
        {
            return AttachToProcess(pid, msecTimeout, AttachFlag.Invasive);
        }

        /// <summary>
        /// Attaches to a live process.
        /// </summary>
        /// <param name="pid">The process ID of the process to attach to.</param>
        /// <param name="msecTimeout">Timeout in milliseconds.</param>
        /// <param name="attachFlag">The type of attach requested for the target process.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget AttachToProcess(int pid, uint msecTimeout, AttachFlag attachFlag)
        {
            IDebugClient client = null;
            IDataReader reader;
            if (attachFlag == AttachFlag.Passive)
            {
                reader = new LiveDataReader(pid, false);
            }
            else
            {
                DbgEngDataReader dbgeng = new DbgEngDataReader(pid, attachFlag, msecTimeout);
                reader = dbgeng;
                client = dbgeng.DebuggerInterface;
            }

            DataTargetImpl dataTarget = new DataTargetImpl(reader, client);
            return dataTarget;
        }

        /// <summary>
        /// Attaches to a snapshot process (see https://msdn.microsoft.com/en-us/library/dn457825(v=vs.85).aspx).
        /// </summary>
        /// <param name="pid">The process ID of the process to attach to.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget CreateSnapshotAndAttach(int pid)
        {
            IDataReader reader = new LiveDataReader(pid, true);
            DataTargetImpl dataTarget = new DataTargetImpl(reader, null);
            return dataTarget;
        }

        /// <summary>
        /// Returns the ProcessId of the DataTarget.  May return uint.MaxValue if the underlying IDataReader does
        /// not implement this functionality.
        /// </summary>
        public abstract uint ProcessId { get; }

        /// <summary>
        /// The data reader for this instance.
        /// </summary>
        public abstract IDataReader DataReader { get; }

        private SymbolLocator _symbolLocator;
        /// <summary>
        /// Instance to manage the symbol path(s)
        /// </summary>
        public SymbolLocator SymbolLocator
        {
            get
            {
                if (_symbolLocator == null)
                    _symbolLocator = new DefaultSymbolLocator();

                return _symbolLocator;
            }
            set => _symbolLocator = value;
        }

        private FileLoader _fileLoader;
        internal FileLoader FileLoader
        {
            get
            {
                if (_fileLoader == null)
                    _fileLoader = new FileLoader(this);

                return _fileLoader;
            }
        }

        /// <summary>
        /// Returns true if the target process is a minidump, or otherwise might have limited memory.  If IsMinidump
        /// returns true, a greater range of functions may fail to return data due to the data not being present in
        /// the application/crash dump you are debugging.
        /// </summary>
        public abstract bool IsMinidump { get; }

        /// <summary>
        /// Returns the architecture of the target process or crash dump.
        /// </summary>
        public abstract Architecture Architecture { get; }

        /// <summary>
        /// Returns the list of Clr versions loaded into the process.
        /// </summary>
        public abstract IList<ClrInfo> ClrVersions { get; }

        /// <summary>
        /// Returns the pointer size for the target process.
        /// </summary>
        public abstract uint PointerSize { get; }

        /// <summary>
        /// Reads memory from the target.
        /// </summary>
        /// <param name="address">The address to read from.</param>
        /// <param name="buffer">
        /// The buffer to store the data in.  Size must be greator or equal to
        /// bytesRequested.
        /// </param>
        /// <param name="bytesRequested">The amount of bytes to read from the target process.</param>
        /// <param name="bytesRead">The actual number of bytes read.</param>
        /// <returns>
        /// True if any bytes were read out of the process (including a partial read).  False
        /// if no bytes could be read from the address.
        /// </returns>
        public abstract bool ReadProcessMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead);

        /// <summary>
        /// Returns the IDebugClient interface associated with this datatarget.  (Will return null if the
        /// user attached passively.)
        /// </summary>
        public abstract IDebugClient DebuggerInterface { get; }

        /// <summary>
        /// Enumerates information about the loaded modules in the process (both managed and unmanaged).
        /// </summary>
        public abstract IEnumerable<ModuleInfo> EnumerateModules();

        /// <summary>
        /// IDisposable implementation.
        /// </summary>
        public abstract void Dispose();

        protected internal abstract void AddDacLibrary(DacLibrary dacLibrary);

        /// <summary>
        /// Creates a runtime from the given Dac file on disk.
        /// </summary>
        internal ClrRuntime CreateRuntime(ClrInfo clrInfo)
        {
            if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));

            string dac = clrInfo.LocalMatchingDac;
            if (dac != null && !File.Exists(dac))
                dac = null;

            if (dac == null)
                dac = SymbolLocator.FindBinary(clrInfo.DacInfo);

            if (!File.Exists(dac))
                throw new FileNotFoundException("Could not find matching DAC for this runtime.", clrInfo.DacInfo.FileName);

            if (IntPtr.Size != (int)DataReader.GetPointerSize())
                throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

            return ConstructRuntime(clrInfo, dac);
        }

        /// <summary>
        /// Creates a runtime from a given IXClrDataProcess interface. Used for debugger plugins.
        /// </summary>
        internal ClrRuntime CreateRuntime(ClrInfo clrInfo, object clrDataProcess)
        {
            if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));
            if (clrDataProcess == null) throw new ArgumentNullException(nameof(clrDataProcess));

            DacLibrary lib = new DacLibrary(this, DacLibrary.TryGetDacPtr(clrDataProcess));

            // Figure out what version we are on.
            if (lib.GetSOSInterfaceNoAddRef() != null)
                return new V45Runtime(clrInfo, this, lib);

            byte[] buffer = new byte[Marshal.SizeOf(typeof(V2HeapDetails))];

            int val = lib.InternalDacPrivateInterface.Request(DacRequests.GCHEAPDETAILS_STATIC_DATA, 0, null, (uint)buffer.Length, buffer);
            if ((uint)val == 0x80070057)
                return new LegacyRuntime(clrInfo, this, lib, DesktopVersion.v4, 10000);

            return new LegacyRuntime(clrInfo, this, lib, DesktopVersion.v2, 3054);
        }

        /// <summary>
        /// Creates a runtime from the given Dac file on disk.
        /// </summary>
        /// <param name="clrInfo">CLR info</param>
        /// <param name="dacFilename">A full path to the matching mscordacwks for this process.</param>
        /// <param name="ignoreMismatch">Whether or not to ignore mismatches between </param>
        /// <returns></returns>
        internal ClrRuntime CreateRuntime(ClrInfo clrInfo, string dacFilename, bool ignoreMismatch = false)
        {
            if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));
            if (string.IsNullOrEmpty(dacFilename)) throw new ArgumentNullException(nameof(dacFilename));

            if (!File.Exists(dacFilename))
                throw new FileNotFoundException(dacFilename);

            if (!ignoreMismatch)
            {
                PlatformFunctions.GetFileVersion(dacFilename, out int major, out int minor, out int revision, out int patch);
                if (major != clrInfo.Version.Major || minor != clrInfo.Version.Minor || revision != clrInfo.Version.Revision || patch != clrInfo.Version.Patch)
                    throw new InvalidOperationException($"Mismatched dac. Version: {major}.{minor}.{revision}.{patch}");
            }

            return ConstructRuntime(clrInfo, dacFilename);
        }

        private ClrRuntime ConstructRuntime(ClrInfo clrInfo, string dac)
        {
            if (IntPtr.Size != (int)DataReader.GetPointerSize())
                throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

            if (IsMinidump)
                SymbolLocator.PrefetchBinary(clrInfo.ModuleInfo.FileName, (int)clrInfo.ModuleInfo.TimeStamp, (int)clrInfo.ModuleInfo.FileSize);

            DacLibrary lib = new DacLibrary(this, dac);

            if (clrInfo.Flavor == ClrFlavor.Core)
                return new V45Runtime(clrInfo, this, lib);

            DesktopVersion ver;
            if (clrInfo.Version.Major == 2)
                ver = DesktopVersion.v2;
            else if (clrInfo.Version.Major == 4 && clrInfo.Version.Minor == 0 && clrInfo.Version.Patch < 10000)
                ver = DesktopVersion.v4;
            else
            {
                // Assume future versions will all work on the newest runtime version.
                return new V45Runtime(clrInfo, this, lib);
            }

            return new LegacyRuntime(clrInfo, this, lib, ver, clrInfo.Version.Patch);
        }
    }
}