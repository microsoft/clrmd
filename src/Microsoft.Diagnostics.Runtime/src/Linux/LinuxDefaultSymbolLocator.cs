// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    /// <summary>
    /// A symbol locator that search binaries based on files loaded in the live Linux target.
    /// </summary>
    internal class LinuxDefaultSymbolLocator : DefaultSymbolLocator
    {
        private IEnumerable<string> _modules;
        
        public LinuxDefaultSymbolLocator(IEnumerable<string> modules) : base()
        {
            _modules = modules;
        }

        public override string FindBinary(string fileName, int buildTimeStamp, int imageSize, bool checkProperties = true)
        {
            string name = Path.GetFileName(fileName);
            foreach(var m in _modules)
            {
                if (name == Path.GetFileName(m))
                {
                    return m;
                }
                string path = Path.Combine(Path.GetDirectoryName(m), name);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return base.FindBinary(fileName, buildTimeStamp, imageSize, checkProperties);
        }
    }
}
