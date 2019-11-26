using System;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    [Flags]
    internal enum DebugAttach : uint
    {
        Default = 0,
        NonInvasive = 1,
    }
}
