// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal class FileLoader
    {
        private readonly Dictionary<string, PEImage> _pefileCache = new Dictionary<string, PEImage>(StringComparer.OrdinalIgnoreCase);

        public FileLoader()
        {
        }

        public PEImage LoadPEImage(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            if (_pefileCache.TryGetValue(fileName, out PEImage result))
                return result;

            Stream stream = File.OpenRead(fileName);
            result = new PEImage(stream);

            if (!result.IsValid)
                result = null;

            _pefileCache[fileName] = result;
            return result;
        }
    }
}