using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.DbgEng
{
    [DynamicInterfaceCastableImplementation]
    internal unsafe interface IDebugClientWrapper : IDebugClient
    {
        int IDebugClient.CreateProcessAndAttach(string commandLine, string? directory, DEBUG_ATTACH flags, in DEBUG_CREATE_PROCESS_OPTIONS options)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);

            fixed (char* cmdPtr = commandLine)
            fixed (char* directoryPtr = directory)
            fixed (DEBUG_CREATE_PROCESS_OPTIONS* optionsPtr = &options)
            {
                return vtable->CreateProcessAndAttach2Wide(self, 0, cmdPtr, optionsPtr, sizeof(DEBUG_CREATE_PROCESS_OPTIONS), directoryPtr, null, 0, flags);
            }
        }

        int IDebugClient.AttachProcess(int processId, DEBUG_ATTACH flags)
        {
            throw new NotImplementedException();
        }

        void IDebugClient.DetachProcesses()
        {
            throw new NotImplementedException();
        }

        void IDebugClient.EndSession(DEBUG_END flags)
        {
            throw new NotImplementedException();
        }

        int IDebugClient.OpenDumpFile(string filename)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);

            fixed (char* filenamePtr = filename)
                return vtable->OpenDumpFileWide(self, filenamePtr, 0);
        }

        void IDebugClient.WriteDumpFile(string filename, DEBUG_DUMP qualifier, DEBUG_FORMAT format, string? comment)
        {
            throw new NotImplementedException();
        }

        IDebugOutputCallbacks IDebugClient.GetOutputCallbacks()
        {
            throw new NotImplementedException();
        }

        void IDebugClient.SetOutputCallbacks(IDebugOutputCallbacks callbacks)
        {
            throw new NotImplementedException();
        }

        IDebugEventCallbacks IDebugClient.GetEventCallbacks()
        {
            throw new NotImplementedException();
        }

        void IDebugClient.SetEventCallbacks(IDebugEventCallbacks callbacks)
        {
            throw new NotImplementedException();
        }

        private static void GetVTable(object ths, out nint self, out IDebugClientVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugClient;
            vtable = *(IDebugClientVtable**)self;
        }
    }
}
