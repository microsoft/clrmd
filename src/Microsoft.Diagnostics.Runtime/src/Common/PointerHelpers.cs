using System;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class PointerHelpers
    {
        public static IntPtr AsIntPtr(this ulong address)
        {
            if (IntPtr.Size == 8)
                return new IntPtr((long)address);

            return new IntPtr((int)address);
        }
    }
}
