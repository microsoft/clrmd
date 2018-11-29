using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum EFileAccess : uint
    {
        None = 0x00000000,
        GenericRead = 0x80000000,
        GenericWrite = 0x40000000,
        GenericExecute = 0x20000000,
        GenericAll = 0x10000000
    }
}