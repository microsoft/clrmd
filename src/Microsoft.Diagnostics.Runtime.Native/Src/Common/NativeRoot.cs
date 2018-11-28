using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Native
{
    class NativeRoot
    {
        public ulong Address { get; internal set; }
        public ulong Object { get; internal set; }
        public GCRootKind Kind { get; internal set; }
        public bool IsInterior { get; internal set; }
        public bool IsPinned { get; internal set; }
    }
}
