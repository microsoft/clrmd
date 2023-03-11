// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public interface IDebugControl
    {
        OSPlatform OSPlatform { get; }
        int PointerSize { get; }
        ImageFileMachine CpuType { get; }

        int AddEngineOptions(DEBUG_ENGOPT options);
        int WaitForEvent(TimeSpan timeout);
        void Write(DEBUG_OUTPUT mask, string text);
        void WriteLine(DEBUG_OUTPUT mask, string text) => Write(mask, text + '\n');
        int GetExecutionStatus(out DEBUG_STATUS status);
        int Execute(DEBUG_OUTCTL outputControl, string command, DEBUG_EXECUTE flags);
        void ControlledOutput(DEBUG_OUTCTL outCtl, DEBUG_OUTPUT dbgOutput, string message);
    }
}