using System;

namespace Microsoft.Diagnostics.Runtime {
    public struct iovec
    {
        public IntPtr iov_base; /* Starting address */
        public UIntPtr iov_len; /* Number of bytes to transfer */
    }
}