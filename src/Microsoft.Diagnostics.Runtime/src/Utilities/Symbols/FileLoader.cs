using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal class FileLoader : ICorDebug.ICLRDebuggingLibraryProvider
    {
        private Dictionary<string, PEFile> _pefileCache = new Dictionary<string, PEFile>(StringComparer.OrdinalIgnoreCase);
        private DataTarget _dataTarget;

        public FileLoader(DataTarget dt)
        {
            _dataTarget = dt;
        }

        public PEFile LoadPEFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            if (_pefileCache.TryGetValue(fileName, out PEFile result))
            {
                if (!result.Disposed)
                    return result;

                _pefileCache.Remove(fileName);
            }

            try
            {
                result = new PEFile(fileName);
                _pefileCache[fileName] = result;
            }
            catch
            {
                result = null;
            }

            return result;
        }

        public int ProvideLibrary([In, MarshalAs(UnmanagedType.LPWStr)] string fileName, int timestamp, int sizeOfImage, out IntPtr hModule)
        {
            string result = _dataTarget.SymbolLocator.FindBinary(fileName, timestamp, sizeOfImage, false);
            if (result == null)
            {
                hModule = IntPtr.Zero;
                return -1;
            }

            hModule = WindowsFunctions.NativeMethods.LoadLibrary(result);
            return 0;
        }
    }
}