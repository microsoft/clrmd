using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.DbgEng
{
    public interface IDebugClient
    {
        public static ComWrappers DbgEngComWrappers { get; } = new DbgEngCom();

        static object Create(string dbgengDirectory)
        {
            if (!Directory.Exists(dbgengDirectory))
                throw new DirectoryNotFoundException(dbgengDirectory);

            string dbgengPath = Path.Combine(dbgengDirectory, DbgEngCom.DbgEngDll);
            if (!File.Exists(dbgengPath))
                throw new FileNotFoundException($"Did not find {DbgEngCom.DbgEngDll} in '{dbgengPath}'.", dbgengPath);

            // We will opportunistically load these dlls, but it's not fatal if missing
            NativeMethods.LoadLibrary(Path.Combine(dbgengDirectory, "dbgcore.dll"));
            NativeMethods.LoadLibrary(Path.Combine(dbgengDirectory, "dbghelp.dll"));

            nint dbgengBase = NativeMethods.LoadLibrary(dbgengPath);
            if (dbgengBase == 0)
                throw new FileLoadException($"Could not load '{dbgengPath}'.");

            int hr = NativeMethods.DebugCreate(in DbgEngWrapper.IID_IDebugClient5, out nint pUnknown);

            object obj = DbgEngComWrappers.GetOrCreateObjectForComInstance(pUnknown, CreateObjectFlags.UniqueInstance);
            if (hr > 0)
                Marshal.Release(pUnknown);

            return obj;
        }

        public static object Create(nint pDebugClient) => DbgEngComWrappers.GetOrCreateObjectForComInstance(pDebugClient, CreateObjectFlags.UniqueInstance);

        int CreateProcessAndAttach(string commandLine, string? directory, DEBUG_ATTACH flags, in DEBUG_CREATE_PROCESS_OPTIONS options);
        int AttachProcess(int processId, DEBUG_ATTACH flags);
        void DetachProcesses();

        void EndSession(DEBUG_END flags);

        int OpenDumpFile(string filename);
        void WriteDumpFile(string filename, DEBUG_DUMP qualifier, DEBUG_FORMAT format, string? comment);

        IDebugOutputCallbacks GetOutputCallbacks();
        void SetOutputCallbacks(IDebugOutputCallbacks callbacks);

        IDebugEventCallbacks GetEventCallbacks();
        void SetEventCallbacks(IDebugEventCallbacks callbacks);
    }
}
