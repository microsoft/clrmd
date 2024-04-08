// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        private static Guid IID_IDebugSymbols3 = new("f02fbecc-50ac-4f36-9ad9-c975e8f32ff8");
        private static Guid IID_IDebugSystemObjects4 = new("489468e6-7d0f-4af5-87ab-25207454d553");
        private static Guid IID_IDebugAdvanced2 = new("716d14c9-119b-4ba5-af1f-0890e672416a");

        private readonly nint _client;
        private readonly nint _control;
        private readonly nint _spaces;
        private readonly nint _symbols;
        private readonly nint _systemObjects;
        private readonly nint _advanced;

        nint IDbgInterfaceProvider.DebugClient => _client;
        nint IDbgInterfaceProvider.DebugControl => _control;
        nint IDbgInterfaceProvider.DebugDataSpaces => _spaces;
        nint IDbgInterfaceProvider.DebugSymbols => _symbols;
        nint IDbgInterfaceProvider.DebugSystemObjects => _systemObjects;
        nint IDbgInterfaceProvider.DebugAdvanced => _advanced;

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

            hr = Marshal.QueryInterface(pUnknown, ref IID_IDebugSymbols3, out _symbols);
            if (hr != 0)
                _symbols = 0;

            hr = Marshal.QueryInterface(pUnknown, ref IID_IDebugSystemObjects4, out _systemObjects);
            if (hr != 0)
                _systemObjects = 0;

            hr = Marshal.QueryInterface(pUnknown, ref IID_IDebugAdvanced2, out _advanced);
            if (hr != 0)
                _advanced = 0;
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

            else if (interfaceType.Equals(typeof(IDebugSymbols).TypeHandle))
                return typeof(IDebugSymbolsWrapper).TypeHandle;

            else if (interfaceType.Equals(typeof(IDebugSystemObjects).TypeHandle))
                return typeof(IDebugSystemObjectsWrapper).TypeHandle;

            else if (interfaceType.Equals(typeof(IDebugAdvanced).TypeHandle))
                return typeof(IDebugAdvancedWrapper).TypeHandle;

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

            else if (_symbols != 0 && interfaceType.Equals(typeof(IDebugSymbols).TypeHandle))
                return true;

            else if (_systemObjects != 0 && interfaceType.Equals(typeof(IDebugSystemObjects).TypeHandle))
                return true;

            else if (_advanced != 0 && interfaceType.Equals(typeof(IDebugAdvanced).TypeHandle))
                return true;

            return false;
        }
    }
}