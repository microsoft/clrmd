using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
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

        int IDebugClient.CreateProcessAndAttach(string commandLine, string? directory, IEnumerable<KeyValuePair<string,string>> environment, DEBUG_ATTACH flags, in DEBUG_CREATE_PROCESS_OPTIONS options)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in environment)
            {
                sb.Append(entry.Key);
                sb.Append('=');

                string value = entry.Value;
                if (value.Length > 0 && value.Contains(' ') && value[0] != '"' && value[^1] != '"')
                    value = '"' + value + '"';

                sb.Append(value);
                sb.Append('\0');
            }

            sb.Append('\0');

            string env = sb.ToString();

            fixed (char* cmdPtr = commandLine)
            fixed (char* directoryPtr = directory)
            fixed (DEBUG_CREATE_PROCESS_OPTIONS* optionsPtr = &options)
            fixed (char* envPtr = env)
            {
                return vtable->CreateProcessAndAttach2Wide(self, 0, cmdPtr, optionsPtr, sizeof(DEBUG_CREATE_PROCESS_OPTIONS), directoryPtr, envPtr, 0, flags);
            }
        }

        int IDebugClient.AttachProcess(int processId, DEBUG_ATTACH flags)
        {
            throw new NotImplementedException();
        }

        int IDebugClient.DetachProcesses()
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            return vtable->DetachProcesses(self);
        }

        int IDebugClient.EndSession(DEBUG_END flags)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            return vtable->EndSession(self, flags);
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

        IDebugOutputCallbacks? IDebugClient.GetOutputCallbacks()
        {
            throw new NotImplementedException();
        }

        void IDebugClient.SetOutputCallbacks(IDebugOutputCallbacks? callbacks)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            if (callbacks is null)
            {
                vtable->SetOutputCallbacksWide(self, 0);
            }
            else
            {
                nint pUnk = DebugOutputCallbacksCOM.Instance.GetOrCreateComInterfaceForObject(callbacks, CreateComInterfaceFlags.None);
                Marshal.QueryInterface(pUnk, ref DebugOutputCallbacksCOM.IID_IOutputCallbacks2, out nint pOutputCallbacks);

                vtable->SetOutputCallbacksWide(self, pOutputCallbacks);

                Marshal.Release(pOutputCallbacks);
                Marshal.Release(pUnk);
            }
        }

        IDebugEventCallbacks? IDebugClient.GetEventCallbacks()
        {
            throw new NotImplementedException();
        }

        void IDebugClient.SetEventCallbacks(IDebugEventCallbacks? callbacks)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            if (callbacks == null)
            {
                vtable->SetEventCallbacksWide(self, 0);
            }
            else
            {
                nint pUnk = DebugEventCallbacksCOM.Instance.GetOrCreateComInterfaceForObject(callbacks, CreateComInterfaceFlags.None);
                Marshal.QueryInterface(pUnk, ref DebugEventCallbacksCOM.IID_IDebugEventCallbacksWide, out nint eventCallbacks);

                vtable->SetEventCallbacksWide(self, eventCallbacks);

                Marshal.Release(eventCallbacks);
                Marshal.Release(pUnk);
            }
        }

        private static void GetVTable(object ths, out nint self, out IDebugClientVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugClient;
            vtable = *(IDebugClientVtable**)self;
        }
    }
}
