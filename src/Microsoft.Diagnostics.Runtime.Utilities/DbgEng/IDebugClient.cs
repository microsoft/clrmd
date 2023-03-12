// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public interface IDebugClient
    {
        public static ComWrappers DbgEngComWrappers { get; } = new DbgEngCom();

        static IDisposable Create(string? dbgengDirectory = null)
        {
            string dbgengPath = DbgEngCom.DbgEngDll;
            if (dbgengDirectory is not null)
            {
                if (!Directory.Exists(dbgengDirectory))
                    throw new DirectoryNotFoundException(dbgengDirectory);

                dbgengPath = Path.Combine(dbgengDirectory, DbgEngCom.DbgEngDll);
                if (!File.Exists(dbgengPath))
                    throw new FileNotFoundException($"Did not find {DbgEngCom.DbgEngDll} in '{dbgengPath}'.", dbgengPath);

                // We will opportunistically load these dlls, but it's not fatal if missing
                NativeMethods.LoadLibrary(Path.Combine(dbgengDirectory, "dbgcore.dll"));
                NativeMethods.LoadLibrary(Path.Combine(dbgengDirectory, "dbghelp.dll"));
            }

            nint dbgengBase = NativeMethods.LoadLibrary(dbgengPath);
            if (dbgengBase == 0)
                throw new FileLoadException($"Could not load '{dbgengPath}'.");

            int hr = NativeMethods.DebugCreate(in DbgEngWrapper.IID_IDebugClient5, out nint pUnknown);

            object obj = DbgEngComWrappers.GetOrCreateObjectForComInstance(pUnknown, CreateObjectFlags.UniqueInstance);
            if (hr > 0)
                Marshal.Release(pUnknown);

            return (IDisposable)obj;
        }

        public static IDisposable Create(nint pDebugClient) => (IDisposable)DbgEngComWrappers.GetOrCreateObjectForComInstance(pDebugClient, CreateObjectFlags.UniqueInstance);

        int CreateProcessAndAttach(string commandLine, string? directory, DEBUG_ATTACH flags, in DEBUG_CREATE_PROCESS_OPTIONS options);
        int CreateProcessAndAttach(string commandLine, string? directory, IEnumerable<KeyValuePair<string, string>> environment, DEBUG_ATTACH flags, in DEBUG_CREATE_PROCESS_OPTIONS options);
        int AttachProcess(int processId, DEBUG_ATTACH flags);
        int DetachProcesses();

        int EndSession(DEBUG_END flags);

        int OpenDumpFile(string filename);
        void WriteDumpFile(string filename, DEBUG_DUMP qualifier, DEBUG_FORMAT format, string? comment);

        nint GetOutputCallbacks();
        int SetOutputCallbacks(IDebugOutputCallbacks? callbacks);
        int SetOutputCallbacks(nint pCallbacks);

        nint GetEventCallbacks();
        int SetEventCallbacks(IDebugEventCallbacks? callbacks);
        int SetEventCallbacks(nint pCallbacks);
    }
}