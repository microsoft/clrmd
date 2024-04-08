// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
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
        IEnumerable<DEBUG_STACK_FRAME_EX> EnumerateStackTrace(int maxFrames = 4096);
        bool GetLastEvent(out DEBUG_LAST_EVENT_INFO_EXCEPTION ex, out uint tid, [NotNullWhen(true)] out string? description);
        int GetNumberProcessors(out uint numberProcessors);
        int GetCurrentTimeDate(out DateTime? timeDate);
        int GetCurrentSystemUpTime(out TimeSpan upTime);
    }
}