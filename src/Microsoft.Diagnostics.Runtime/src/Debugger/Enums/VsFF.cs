using System;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [Flags]
    public enum VS_FF : uint
    {
        DEBUG = 0x00000001,
        PRERELEASE = 0x00000002,
        PATCHED = 0x00000004,
        PRIVATEBUILD = 0x00000008,
        INFOINFERRED = 0x00000010,
        SPECIALBUILD = 0x00000020,
    }
}