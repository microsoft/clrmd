// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        int IDebugClient.CreateProcessAndAttach(string commandLine, string? directory, IEnumerable<KeyValuePair<string, string>> environment, DEBUG_ATTACH flags, in DEBUG_CREATE_PROCESS_OPTIONS options)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);

            StringBuilder sb = new();
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

        nint IDebugClient.GetOutputCallbacks()
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            nint pCallbacks = 0;
            int hr = vtable->GetOutputCallbacksWide(self, &pCallbacks);
            if (hr != 0)
                return 0;

            return pCallbacks;
        }

        int IDebugClient.SetOutputCallbacks(IDebugOutputCallbacks? callbacks)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            if (callbacks is null)
            {
                return vtable->SetOutputCallbacksWide(self, 0);
            }
            else
            {
                nint pUnk = DebugOutputCallbacksCOM.Instance.GetOrCreateComInterfaceForObject(callbacks, CreateComInterfaceFlags.None);
                Marshal.QueryInterface(pUnk, ref DebugOutputCallbacksCOM.IID_IOutputCallbacks2, out nint pOutputCallbacks);

                int hr = vtable->SetOutputCallbacksWide(self, pOutputCallbacks);

                Marshal.Release(pOutputCallbacks);
                Marshal.Release(pUnk);

                return hr;
            }
        }

        int IDebugClient.SetOutputCallbacks(nint pCallbacks)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            return vtable->SetOutputCallbacksWide(self, pCallbacks);
        }

        nint IDebugClient.GetEventCallbacks()
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            nint pCallbacks = 0;
            int hr = vtable->GetEventCallbacksWide(self, &pCallbacks);
            if (hr != 0)
                return 0;

            return pCallbacks;
        }

        int IDebugClient.SetEventCallbacks(IDebugEventCallbacks? callbacks)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            if (callbacks == null)
            {
                return vtable->SetEventCallbacksWide(self, 0);
            }
            else
            {
                nint pUnk = DebugEventCallbacksCOM.Instance.GetOrCreateComInterfaceForObject(callbacks, CreateComInterfaceFlags.None);
                Marshal.QueryInterface(pUnk, ref DebugEventCallbacksCOM.IID_IDebugEventCallbacksWide, out nint eventCallbacks);

                int hr = vtable->SetEventCallbacksWide(self, eventCallbacks);

                Marshal.Release(eventCallbacks);
                Marshal.Release(pUnk);

                return hr;
            }
        }


        int IDebugClient.SetEventCallbacks(nint pCallbacks)
        {
            GetVTable(this, out nint self, out IDebugClientVtable* vtable);
            return vtable->SetEventCallbacksWide(self, pCallbacks);
        }

        private static void GetVTable(object ths, out nint self, out IDebugClientVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugClient;
            vtable = *(IDebugClientVtable**)self;
        }
    }
}