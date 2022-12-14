namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    internal sealed class ExtensionContext : IDisposable
    {
        private static ExtensionContext? s_current;

        private nint IUnknown { get; }

        private IDisposable? _dbgeng;
        private IDebugClient? _client;
        private IDebugControl? _control;
        private IDebugAdvanced? _advanced;
        private IDebugSystemObjects? _systemObjects;
        private IDebugSymbols? _symbols;
        private IDebugDataSpaces? _dataSpaces;

        public object DbgEng => _dbgeng ??= IDebugClient.Create(IUnknown);
        public IDebugClient DebugClient => _client ??= (IDebugClient)DbgEng;
        public IDebugControl DebugControl => _control ??= (IDebugControl)DbgEng;
        public IDebugAdvanced DebugAdvanced => _advanced ??= (IDebugAdvanced)DbgEng;
        public IDebugSystemObjects DebugSystemObjects => _systemObjects ??= (IDebugSystemObjects)DbgEng;
        public IDebugDataSpaces DebugDataSpaces => _dataSpaces ??= (IDebugDataSpaces)DbgEng;
        public IDebugSymbols DebugSymbols => _symbols ??= (IDebugSymbols)DbgEng;

        public static ExtensionContext Create(nint pUnknown)
        {
            if (s_current is null)
                return s_current = new ExtensionContext(pUnknown);

            if (s_current.IUnknown == pUnknown)
                return s_current;

            ExtensionContext current = s_current;
            s_current = current;
            current.Dispose();
            return s_current = new ExtensionContext(pUnknown);
        }

        private ExtensionContext(nint pUnknown)
        {
            IUnknown = pUnknown;
        }

        public void Dispose()
        {
            _dbgeng?.Dispose();
        }
    }
}
