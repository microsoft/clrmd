// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.Runtime.Tests.Tasks
{
    public sealed class Dump : Task
    {
        [Required]
        public string ExePath { get; set; }

        [Required]
        public string FullDumpPath { get; set; }

        public string MinidumpPath { get; set; }

        public bool IsServerGC { get; set; }

        public override bool Execute()
        {
            if (!Environment.Is64BitProcess)
            {
                Log.LogError("This task must be executed by 64-bit MSBuild to generate both 32-bit and 64-bit dump files.");
                return false;
            }

            DebuggerStartInfo info = new DebuggerStartInfo();
            if (IsServerGC)
            {
                info.SetEnvironmentVariable("COMPlus_BuildFlavor", "SVR");
            }

            using Debugger debugger = info.LaunchProcess(ExePath, null);
            debugger.SecondChanceExceptionEvent += (debugger, exception) =>
            {
                if (exception.ExceptionCode == (uint)ExceptionTypes.Clr)
                {
                    _ = debugger.WriteDumpFile(FullDumpPath, DEBUG_DUMP.DEFAULT);

                    if (MinidumpPath != null)
                    {
                        _ = debugger.WriteDumpFile(MinidumpPath, DEBUG_DUMP.SMALL);
                    }
                }
            };

            DEBUG_STATUS status;
            do
            {
                status = debugger.ProcessEvents(0xffffffff);
            } while (status != DEBUG_STATUS.NO_DEBUGGEE);

            return true;
        }
    }
}
