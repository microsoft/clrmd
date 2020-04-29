// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime.Implementation;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    /// <summary>
    /// A symbol locator that search binaries based on files loaded in the live Linux target.
    /// </summary>
    internal class LinuxDefaultSymbolLocator : IBinaryLocator
    {
        private readonly SymbolServerLocator? _locator;
        private readonly IEnumerable<string> _modules;

        public LinuxDefaultSymbolLocator(IDataReader reader)
        {
            _modules = reader.EnumerateModules().Where(module => !string.IsNullOrEmpty(module.FileName)).Select(module => module.FileName!);
            string? sympath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
            if (!string.IsNullOrWhiteSpace(sympath))
                _locator = new SymbolServerLocator(sympath);
        }

        public string? FindBinary(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            string? localBinary = FindLocalBinary(fileName);
            return localBinary ?? _locator?.FindBinary(fileName, buildTimeStamp, imageSize, checkProperties);
        }

        public string? FindBinary(string fileName, ImmutableArray<byte> buildId, bool checkProperties = true) => null;

        public Task<string?> FindBinaryAsync(string fileName, ImmutableArray<byte> buildId, bool checkProperties = true) => Task.FromResult<string?>(null);

        public Task<string?> FindBinaryAsync(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            string? localBinary = FindLocalBinary(fileName);
            if (localBinary != null)
                return Task.FromResult(localBinary)!;

            return _locator?.FindBinaryAsync(fileName, buildTimeStamp, imageSize, checkProperties) ?? Task.FromResult<string?>(null);
        }

        private string? FindLocalBinary(string fileName)
        {
            string name = Path.GetFileName(fileName);
            foreach (var m in _modules)
            {
                string path = Path.Combine(Path.GetDirectoryName(m)!, name);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }
    }
}
