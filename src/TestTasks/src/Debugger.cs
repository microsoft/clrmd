// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities;
using static System.Net.Mime.MediaTypeNames;

namespace Microsoft.Diagnostics.Runtime.Tests.Tasks
{
    internal enum ExceptionTypes : uint
    {
        AV = 0xC0000005,
        StackOverflow = 0xC00000FD,
        Cpp = 0xe06d7363,
        Clr = 0xe0434352,
        Break = 0x80000003
    }

    internal class DebuggerStartInfo
    {
        private readonly Dictionary<string, string> _environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void SetEnvironmentVariable(string variable, string value)
        {
            _environment[variable] = value;
        }

        public unsafe Debugger LaunchProcess(string commandLine, string workingDirectory)
        {
            IDebugClient5 client = CreateIDebugClient();
            IDebugControl control = (IDebugControl)client;

            if (string.IsNullOrEmpty(workingDirectory))
                workingDirectory = Environment.CurrentDirectory;

            string env = GetEnvironment();
            DEBUG_CREATE_PROCESS_OPTIONS options = new DEBUG_CREATE_PROCESS_OPTIONS();
            options.CreateFlags = (DEBUG_CREATE_PROCESS)1;
            int hr = client.CreateProcessAndAttach2(0, commandLine, options, (uint)sizeof(DEBUG_CREATE_PROCESS_OPTIONS), workingDirectory, env, 0, DEBUG_ATTACH.DEFAULT);

            if (hr < 0)
                throw new Exception(Debugger.GetExceptionString("IDebugClient::CreateProcessAndAttach2", hr));

            Debugger debugger = new Debugger(client, control);
            hr = client.SetEventCallbacks(debugger);
            if (hr < 0)
                throw new Exception(Debugger.GetExceptionString("IDebugClient::SetEventCallbacks", hr));

            hr = client.SetOutputCallbacks(debugger);
            if (hr < 0)
                throw new Exception(Debugger.GetExceptionString("IDebugClient::SetOutputCallbacks", hr));

            return debugger;
        }

        #region Private Helpers
        [DllImport("dbgeng.dll")]
        private static extern int DebugCreate(in Guid InterfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object Interface);

        private static IDebugClient5 CreateIDebugClient()
        {
            Guid guid = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
            int hr = DebugCreate(guid, out object obj);
            if (hr < 0)
                throw new Exception(Debugger.GetExceptionString("DebugCreate", hr));

            return (IDebugClient5)obj;
        }

        private string GetEnvironment()
        {
            if (_environment.Count == 0)
                return null;

            StringBuilder sb = new StringBuilder();
            foreach (string key in _environment.Keys)
            {
                sb.Append(key);
                sb.Append("=");


                string value = _environment[key];
                if (value.Length > 0 && value.Contains(' ') && value[0] != '"' && value[^1] != '"')
                    value = '"' + value + '"';

                sb.Append(value);
                sb.Append('\0');
            }

            sb.Append('\0');
            return sb.ToString();
        }
        #endregion
    }

    internal class Debugger : IDebugOutputCallbacks, IDebugEventCallbacks, IDisposable
    {
        #region Fields
        private DEBUG_OUTPUT _outputMask;
        private readonly StringBuilder _output = new StringBuilder();
        private bool _exited;
        private bool _processing;

        private readonly IDebugClient5 _client;
        private readonly IDebugControl _control;
        #endregion

        #region Events
        public delegate void ExceptionEventHandler(Debugger debugger, EXCEPTION_RECORD64 ex, bool firstChance);
        public event ExceptionEventHandler OnException;
        #endregion

        public IDebugClient5 Client => _client;

        public Debugger(IDebugClient5 client, IDebugControl control)
        {
            _client = client;
            _control = control;

            client.SetOutputCallbacks(this);
        }


        public DEBUG_STATUS ProcessEvents(uint timeout)
        {
            if (_processing)
                throw new InvalidOperationException("Cannot call ProcessEvents reentrantly.");

            if (_exited)
                return DEBUG_STATUS.NO_DEBUGGEE;

            _processing = true;
            int hr = _control.WaitForEvent(0, timeout);
            _processing = false;

            if (hr < 0 && (uint)hr != 0x8000000A)
                throw new Exception(GetExceptionString("IDebugControl::WaitForEvent", hr));

            return GetDebugStatus();
        }

        public void TerminateProcess()
        {
            _exited = true;
            _client.EndSession(DEBUG_END.ACTIVE_TERMINATE);
        }

        public string Execute(ulong handle, string command, string args)
        {
            DEBUG_OUTPUT mask = _outputMask;
            _output.Clear();
            _outputMask = DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.SYMBOLS
                | DEBUG_OUTPUT.ERROR | DEBUG_OUTPUT.WARNING | DEBUG_OUTPUT.DEBUGGEE;

            string result = null;
            try
            {
                int hr = _control.CallExtension(handle, command, args);
                if (hr < 0)
                    _output.Append($"Command encountered an error.  HRESULT={hr:X8}");

                result = _output.ToString();
            }
            finally
            {
                _outputMask = mask;
                _output.Clear();
            }

            return result;
        }

        public string Execute(string cmd)
        {
            DEBUG_OUTPUT mask = _outputMask;
            _output.Clear();
            _outputMask = DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.SYMBOLS | DEBUG_OUTPUT.ERROR | DEBUG_OUTPUT.WARNING | DEBUG_OUTPUT.DEBUGGEE;

            string result = null;
            try
            {
                int hr = _control.Execute(DEBUG_OUTCTL.ALL_CLIENTS, cmd, DEBUG_EXECUTE.DEFAULT);
                if (hr < 0)
                    _output.Append($"Command encountered an error.  HRESULT={hr:X8}");

                result = _output.ToString();
            }
            finally
            {
                _outputMask = mask;
                _output.Clear();
            }

            return result;
        }

        public string ExecuteScript(string script)
        {
            DEBUG_OUTPUT mask = _outputMask;
            _output.Clear();
            _outputMask = DEBUG_OUTPUT.NORMAL | DEBUG_OUTPUT.SYMBOLS | DEBUG_OUTPUT.ERROR | DEBUG_OUTPUT.WARNING | DEBUG_OUTPUT.DEBUGGEE;

            string result = null;
            try
            {
                int hr = _control.ExecuteCommandFile(DEBUG_OUTCTL.ALL_CLIENTS, script, DEBUG_EXECUTE.DEFAULT);
                if (hr < 0)
                    _output.Append($"Script encountered an error.  HRESULT={hr:X8}");

                result = _output.ToString();
            }
            finally
            {
                _outputMask = mask;
                _output.Clear();
            }

            return result;
        }

        public int WriteDumpFile(string dump, DEBUG_DUMP type)
        {
            // DEBUG_DUMP.DEFAULT emits an older "USERDU64" format dump which conflicts with our minidump reader.
            if (type == DEBUG_DUMP.DEFAULT)
                return _control.Execute(DEBUG_OUTCTL.NOT_LOGGED, $".dump /ma {dump}", DEBUG_EXECUTE.DEFAULT);

            return _control.Execute(DEBUG_OUTCTL.NOT_LOGGED, $".dump /m {dump}", DEBUG_EXECUTE.DEFAULT);
        }

        public void Dispose()
        {
            _client.SetEventCallbacks(null);
            _client.SetOutputCallbacks(null);
        }

        #region Helpers
        private void SetDebugStatus(DEBUG_STATUS status)
        {
            int hr = _control.SetExecutionStatus(status);
            if (hr < 0)
                throw new Exception(GetExceptionString("IDebugControl::SetExecutionStatus", hr));
        }

        private DEBUG_STATUS GetDebugStatus()
        {
            DEBUG_STATUS status;
            int hr = _control.GetExecutionStatus(out status);

            if (hr < 0)
                throw new Exception(GetExceptionString("IDebugControl::GetExecutionStatus", hr));

            return status;
        }

        internal static string GetExceptionString(string name, int hr)
        {
            return $"{name} failed with hresult={hr:X8}";
        }
        #endregion

        #region IDebugOutputCallbacks
        void IDebugOutputCallbacks.OnText(DEBUG_OUTPUT flags, string text, ulong args)
        {
            if (_output != null && (_outputMask & flags) != 0)
                _output.Append(text);
        }
        #endregion

        #region IDebugEventCallbacks

        public DEBUG_EVENT EventInterestMask => DEBUG_EVENT.BREAKPOINT | DEBUG_EVENT.EXCEPTION;

        DEBUG_STATUS IDebugEventCallbacks.OnBreakpoint(nint bp)
        {
            return DEBUG_STATUS.GO;
        }

        DEBUG_STATUS IDebugEventCallbacks.OnException(in EXCEPTION_RECORD64 exception, bool firstChance)
        {
            OnException?.Invoke(this, exception, firstChance);
            return DEBUG_STATUS.BREAK;
        }
        #endregion
    }

    internal class CreateProcessArgs
    {
        public ulong ImageFileHandle;
        public ulong Handle;
        public ulong BaseOffset;
        public uint ModuleSize;
        public string ModuleName;
        public string ImageName;
        public uint CheckSum;
        public uint TimeDateStamp;
        public ulong InitialThreadHandle;
        public ulong ThreadDataOffset;
        public ulong StartOffset;

        public CreateProcessArgs(ulong ImageFileHandle, ulong Handle, ulong BaseOffset, uint ModuleSize, string ModuleName, string ImageName,
                                 uint CheckSum, uint TimeDateStamp, ulong InitialThreadHandle, ulong ThreadDataOffset, ulong StartOffset)
        {
            this.ImageFileHandle = ImageFileHandle;
            this.Handle = Handle;
            this.BaseOffset = BaseOffset;
            this.ModuleSize = ModuleSize;
            this.ModuleName = ModuleName;
            this.ImageName = ImageName;
            this.CheckSum = CheckSum;
            this.TimeDateStamp = TimeDateStamp;
            this.InitialThreadHandle = InitialThreadHandle;
            this.ThreadDataOffset = ThreadDataOffset;
            this.StartOffset = StartOffset;
        }
    }
}
