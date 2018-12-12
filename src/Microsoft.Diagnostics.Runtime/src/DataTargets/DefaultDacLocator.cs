// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    internal class DefaultDacLocator : IDacLocator
    {
        private readonly DataTarget _dataTarget;

        public DefaultDacLocator(DataTarget dataTarget)
        {
            _dataTarget = dataTarget ?? throw new ArgumentNullException(nameof(dataTarget));
        }

        public string FindDac(ClrInfo clrInfo)
        {
            if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));

            string moduleDirectory = Path.GetDirectoryName(clrInfo.ModuleInfo.FileName);
            if (moduleDirectory == null)
                throw new ClrDiagnosticsException("Incorrect clr module file name");

            string dacLocation = Path.Combine(moduleDirectory, clrInfo.DacFileName);

            //TODO: revisit the logic
            if (clrInfo.IsLinux)
            {
                if (!File.Exists(dacLocation))
                    dacLocation = Path.GetFileName(dacLocation);
            }

            if (File.Exists(dacLocation) && DataTarget.PlatformFunctions.IsEqualFileVersion(dacLocation, clrInfo.Version))
                return dacLocation;

            return _dataTarget.SymbolLocator.FindBinary(
                clrInfo.DacRequestFileName,
                clrInfo.ModuleInfo.TimeStamp,
                clrInfo.ModuleInfo.FileSize,
                false);
        }
    }
}