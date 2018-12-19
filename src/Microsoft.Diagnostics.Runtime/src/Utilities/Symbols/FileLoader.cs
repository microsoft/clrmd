// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal class FileLoader : ICLRDebuggingLibraryProvider
    {
        private readonly Dictionary<string, PEImage> _pefileCache = new Dictionary<string, PEImage>(StringComparer.OrdinalIgnoreCase);
        private readonly DataTarget _dataTarget;

        public FileLoader(DataTarget dt)
        {
            _dataTarget = dt;
        }

        public PEImage LoadPEImage(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

        if (_pefileCache.TryGetValue(fileName, out PEImage result))
            return result;
            
            Stream stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            result = new PEImage(stream);

            if (!result.IsValid)
                result = null;

            _pefileCache[fileName] = result;
            return result;
        }

        public int ProvideLibrary([In][MarshalAs(UnmanagedType.LPWStr)] string fileName, int timestamp, int sizeOfImage, out IntPtr hModule)
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