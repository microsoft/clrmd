using System.Reflection;

namespace Microsoft.Diagnostics.DbgEng
{
    public interface IDebugControl
    {
        int PointerSize { get; }
        ImageFileMachine CpuType { get; }

        int WaitForEvent(TimeSpan timeout);
        void Write(DEBUG_OUTPUT mask, string text);
        void WriteLine(DEBUG_OUTPUT mask, string text) => Write(mask, text + '\n');
    }
}
