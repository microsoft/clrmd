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
        private DataTarget? _dataTarget;
        private ClrRuntime[]? _runtimes;
        private ClrRuntimeInitFailure[]? _failures;

        public object DbgEng => _dbgeng ??= IDebugClient.Create(IUnknown);
        public IDebugClient DebugClient => _client ??= (IDebugClient)DbgEng;
        public IDebugControl DebugControl => _control ??= (IDebugControl)DbgEng;
        public IDebugAdvanced DebugAdvanced => _advanced ??= (IDebugAdvanced)DbgEng;
        public IDebugSystemObjects DebugSystemObjects => _systemObjects ??= (IDebugSystemObjects)DbgEng;
        public IDebugDataSpaces DebugDataSpaces => _dataSpaces ??= (IDebugDataSpaces)DbgEng;
        public IDebugSymbols DebugSymbols => _symbols ??= (IDebugSymbols)DbgEng;

        public DataTarget DataTarget => _dataTarget ??= DbgEngIDataReader.CreateDataTarget(DbgEng);
        public ClrRuntime[] Runtimes
        {
            get
            {
                if (_runtimes is null)
                    InitRuntimes();

                // Make a copy so it isn't modified
                return _runtimes!.ToArray();
            }
        }

        public ClrRuntimeInitFailure[] RuntimeLoadErrors
        {
            get
            {
                if (_failures is null)
                    InitRuntimes();

                // Make a copy so it isn't modified
                return _failures!.ToArray();
            }
        }

        private void InitRuntimes()
        {
            List<ClrRuntimeInitFailure> failures = new();
            List<ClrRuntime> runtimes = new();

            if (_runtimes is null)
            {
                foreach (ClrInfo clr in DataTarget.ClrVersions)
                {
                    try
                    {
                        runtimes.Add(clr.CreateRuntime());
                    }
                    catch (Exception ex)
                    {
                        failures.Add(new ClrRuntimeInitFailure(clr, ex));
                    }
                }
            }

            _runtimes = runtimes.ToArray();
            _failures = failures.ToArray();
        }

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
            // If we are the current instance, clear it
            Interlocked.CompareExchange(ref s_current, null, this);
            _dbgeng?.Dispose();

            if (_runtimes is not null)
                foreach (ClrRuntime runtime in _runtimes)
                    runtime.Dispose();

            _dataTarget?.Dispose();
        }
    }
}
