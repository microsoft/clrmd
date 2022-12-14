using Microsoft.Diagnostics.Runtime.Utilities.DbgEng;
using System.Text;

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

        protected DbgEngCommand(nint pUnknown, bool redirectConsoleOutput = true)
        {
            _context = ExtensionContext.Create(pUnknown);
            if (redirectConsoleOutput)
                _redirectedOutput = new DbgEngOutputStream(DebugClient, DebugControl);
        }

        public void Dispose()
        {
            DisposeImpl();
            _redirectedOutput?.Dispose();
        }

        public (int HResult, string Output) RunCommandWithOutput(string command, DEBUG_OUTPUT mask = DEBUG_OUTPUT.NORMAL)
        {
            StringBuilder result = new();
            int hr;
            using (DbgEngOutputHolder outputHolder = new(DebugClient, mask))
            {
                outputHolder.OutputReceived += (text, flags) => result.Append(text);
                hr = DebugControl.Execute(DEBUG_OUTCTL.THIS_CLIENT, "!address", DEBUG_EXECUTE.DEFAULT);
            }

            return (hr, result.ToString());
        }

        protected virtual void DisposeImpl()
        {
        }
    }
}
