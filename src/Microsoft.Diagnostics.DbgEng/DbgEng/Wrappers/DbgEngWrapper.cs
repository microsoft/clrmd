using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    internal sealed class DbgEngWrapper :
        IDynamicInterfaceCastable,
        IDbgInterfaceProvider,
        IDisposable
    {
        internal static Guid IID_IDebugClient5 = new("e3acb9d7-7ec2-4f0c-a0da-e81e0cbbe628");
        private static Guid IID_IDebugControl5 = new("b2ffe162-2412-429f-8d1d-5bf6dd824696");
        private static Guid IID_IDebugDataSpaces2 = new("7a5e852f-96e9-468f-ac1b-0b3addc4a049");

        private readonly nint _client;
        private readonly nint _control;
        private readonly nint _spaces;

        nint IDbgInterfaceProvider.DebugClient => _client;
        nint IDbgInterfaceProvider.DebugControl => _control;
        nint IDbgInterfaceProvider.DebugDataSpaces => _spaces;

        private bool _disposed;
        private static int _alive;

        internal DbgEngWrapper(nint pUnknown)
        {
            if (Interlocked.Exchange(ref _alive, 1) == 1)
                throw new NotSupportedException($"DbgEng doesn't properly handle multiple instances simultaneously.  This can lead to some undefined behavior.");

            int hr = Marshal.QueryInterface(pUnknown, ref IID_IDebugClient5, out _client);
            if (hr != 0)
                throw new NotSupportedException($"This IUnknown pointer does not implement IDebugClient5.");

            hr = Marshal.QueryInterface(pUnknown, ref IID_IDebugControl5, out _control);
            if (hr != 0)
                _control = 0;

            hr = Marshal.QueryInterface(pUnknown, ref IID_IDebugDataSpaces2, out _spaces);
            if (hr != 0)
                _spaces = 0;
        }

        ~DbgEngWrapper()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                Marshal.Release(_client);
                Marshal.Release(_control);
                Marshal.Release(_spaces);

                Interlocked.Exchange(ref _alive, 0);
            }

            GC.SuppressFinalize(this);
        }

        public RuntimeTypeHandle GetInterfaceImplementation(RuntimeTypeHandle interfaceType)
        {
            if (interfaceType.Equals(typeof(IDebugClient).TypeHandle))
                return typeof(IDebugClientWrapper).TypeHandle;

            else if (interfaceType.Equals(typeof(IDebugControl).TypeHandle))
                return typeof(IDebugControlWrapper).TypeHandle;

            else if (interfaceType.Equals(typeof(IDebugDataSpaces).TypeHandle))
                return typeof(IDebugDataSpacesWrapper).TypeHandle;

            return default;
        }

        public bool IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented)
        {
            if (_client != 0 && interfaceType.Equals(typeof(IDebugClient).TypeHandle))
                return true;

            else if (_control != 0 && interfaceType.Equals(typeof(IDebugControl).TypeHandle))
                return true;

            else if (_spaces != 0 && interfaceType.Equals(typeof(IDebugDataSpaces).TypeHandle))
                return true;

            return false;
        }
    }
}
