// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities.DbgEng;

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

    internal sealed class DebuggerStartInfo
    {
        private readonly Dictionary<string, string> _environment = new(StringComparer.OrdinalIgnoreCase);
        public string DbgEngDirectory { get; set; }

        public DebuggerStartInfo()
        {
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Value is null || entry.Value is not string strValue)
                    continue;

                _environment[(string)entry.Key] = strValue;
            }
        }

        public void SetEnvironmentVariable(string variable, string value)
        {
            _environment[variable] = value;
        }

        public unsafe Debugger LaunchProcess(string commandLine, string workingDirectory)
        {
            if (string.IsNullOrEmpty(workingDirectory))
                workingDirectory = Environment.CurrentDirectory;

            return new Debugger(DbgEngDirectory, commandLine, workingDirectory, _environment);
        }
    }

    internal sealed class Debugger : IDebugOutputCallbacks, IDebugEventCallbacks, IDisposable
    {
        #region Fields
        private readonly StringBuilder _output = new();
        private bool _exited;
        private bool _processing;

        private readonly IDisposable _dbgeng;
        private readonly IDebugClient _client;
        private readonly IDebugControl _control;
        #endregion

        #region Events
        public delegate void ExceptionEventHandler(Debugger debugger, EXCEPTION_RECORD64 ex, bool firstChance);
        public event ExceptionEventHandler OnException;
        #endregion

        public IDebugClient Client => _client;

        public Debugger(string dbgEngDirectory, string commandLine, string workingDirectory, IEnumerable<KeyValuePair<string, string>> env)
        {
            _dbgeng = IDebugClient.Create(dbgEngDirectory);
            _client = (IDebugClient)_dbgeng;
            _control = (IDebugControl)_client;

            if (string.IsNullOrEmpty(workingDirectory))
                workingDirectory = Environment.CurrentDirectory;

            DEBUG_CREATE_PROCESS_OPTIONS options = default;
            options.CreateFlags = DEBUG_CREATE_PROCESS.DEBUG_PROCESS;
            int hr = _client.CreateProcessAndAttach(commandLine, workingDirectory, env, DEBUG_ATTACH.DEFAULT, options);

            if (hr < 0)
                throw new Exception(GetExceptionString("IDebugClient::CreateProcessAndAttach2", hr));

            _client.SetEventCallbacks(this);
            if (hr < 0)
                throw new Exception(GetExceptionString("IDebugClient::SetEventCallbacks", hr));

            _client.SetOutputCallbacks(this);
            if (hr < 0)
                throw new Exception(GetExceptionString("IDebugClient::SetOutputCallbacks", hr));
        }

        public DEBUG_STATUS ProcessEvents(TimeSpan timeout)
        {
            if (_processing)
                throw new InvalidOperationException("Cannot call ProcessEvents reentrantly.");

            if (_exited)
                return DEBUG_STATUS.NO_DEBUGGEE;

            _processing = true;
            int hr = _control.WaitForEvent(timeout);
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
            _dbgeng.Dispose();
        }

        private DEBUG_STATUS GetDebugStatus()
        {
            int hr = _control.GetExecutionStatus(out DEBUG_STATUS status);

            if (hr < 0)
                throw new Exception(GetExceptionString("IDebugControl::GetExecutionStatus", hr));

            return status;
        }

        internal static string GetExceptionString(string name, int hr)
        {
            return $"{name} failed with hresult={hr:X8}";
        }

        #region IDebugOutputCallbacks
        void IDebugOutputCallbacks.OnText(DEBUG_OUTPUT flags, string text, ulong args)
        {
            _output?.Append(text);
        }
        #endregion

        #region IDebugEventCallbacks

        public DEBUG_EVENT EventInterestMask => DEBUG_EVENT.BREAKPOINT | DEBUG_EVENT.EXCEPTION | DEBUG_EVENT.EXIT_PROCESS;

        DEBUG_STATUS IDebugEventCallbacks.OnBreakpoint(nint bp)
        {
            return DEBUG_STATUS.GO;
        }

        DEBUG_STATUS IDebugEventCallbacks.OnException(in EXCEPTION_RECORD64 exception, bool firstChance)
        {
            OnException?.Invoke(this, exception, firstChance);

            return DEBUG_STATUS.BREAK;
        }

        DEBUG_STATUS IDebugEventCallbacks.OnExitProcess(int exitCode)
        {
            _exited = true;
            return DEBUG_STATUS.BREAK;
        }
        #endregion
    }

    internal sealed class CreateProcessArgs
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