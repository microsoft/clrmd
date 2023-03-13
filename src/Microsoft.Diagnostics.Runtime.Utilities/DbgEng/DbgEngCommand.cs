// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.DbgEng;

namespace DbgEngExtension
{
    public class DbgEngCommand : IDisposable
    {
        private readonly ExtensionContext _context;
        private readonly DbgEngOutputStream? _redirectedOutput;

        protected object DbgEng => _context.DbgEng;
        protected IDebugClient DebugClient => _context.DebugClient;
        protected IDebugControl DebugControl => _context.DebugControl;
        protected IDebugAdvanced DebugAdvanced => _context.DebugAdvanced;
        protected IDebugSystemObjects DebugSystemObjects => _context.DebugSystemObjects;
        protected IDebugDataSpaces DebugDataSpaces => _context.DebugDataSpaces;
        protected IDebugSymbols DebugSymbols => _context.DebugSymbols;

        protected DataTarget DataTarget => _context.DataTarget;
        protected ClrRuntime[] Runtimes => _context.Runtimes; // Make a copy to not let folks modify the original
        protected ClrRuntimeInitFailure[] RuntimeLoadErrors => _context.RuntimeLoadErrors;

        protected DbgEngCommand(nint pUnknown, bool redirectConsoleOutput)
        {
            _context = ExtensionContext.Create(pUnknown);
            if (redirectConsoleOutput)
                _redirectedOutput = new DbgEngOutputStream(DebugClient, DebugControl);
        }

        protected DbgEngCommand(IDisposable dbgeng, bool redirectConsoleOutput)
        {
            _context = ExtensionContext.Create(dbgeng);
            if (redirectConsoleOutput)
                _redirectedOutput = new DbgEngOutputStream(DebugClient, DebugControl);
        }

        /// <summary>
        /// Initializes a DbgEng command from another command.  This is primarily used for when one DbgEng command
        /// needs to call into another command's helper APIs.
        /// </summary>
        protected DbgEngCommand(DbgEngCommand cmd)
        {
            _context = cmd._context;
        }

        public void Dispose()
        {
            DisposeImpl();
            _redirectedOutput?.Dispose();
        }

        protected (int HResult, string Output) RunCommandWithOutput(string command, DEBUG_OUTPUT mask = DEBUG_OUTPUT.NORMAL)
        {
            StringBuilder result = new();
            int hr;
            using (DbgEngOutputHolder outputHolder = new(DebugClient, mask))
            {
                outputHolder.OutputReceived += (text, flags) => result.Append(text);
                hr = DebugControl.Execute(DEBUG_OUTCTL.THIS_CLIENT, command, DEBUG_EXECUTE.DEFAULT);
            }

            return (hr, result.ToString());
        }

        protected virtual void DisposeImpl()
        {
        }
    }
}