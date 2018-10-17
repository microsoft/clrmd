using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.ComWrappers
{
    internal unsafe sealed class SOSStackRefEnum : CallableCOMWrapper
    {
        private static Guid IID_ISOSStackRefEnum = new Guid("8FA642BD-9F10-4799-9AA3-512AE78C77EE");

        private readonly Next _next;

        public SOSStackRefEnum(IntPtr pUnk)
            : base(ref IID_ISOSStackRefEnum, pUnk)
        {
            ISOSStackRefEnumVTable* vtable = (ISOSStackRefEnumVTable*)_vtable;
            InitDelegate(ref _next, vtable->Next);
        }
        
        public int ReadStackReferences(StackRefData[] stackRefs)
        {
            if (stackRefs == null)
                throw new ArgumentNullException(nameof(stackRefs));

            int hr = _next(stackRefs.Length, stackRefs, out int read);
            return hr >= S_OK ? read : 0;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int Next(int count, [Out, MarshalAs(UnmanagedType.LPArray)] StackRefData[] stackRefs, out int pNeeded);
    }


#pragma warning disable CS0169
#pragma warning disable CS0649
    internal struct ISOSStackRefEnumVTable
    {
        private readonly IntPtr Skip;
        private readonly IntPtr Reset;
        private readonly IntPtr GetCount;
        public readonly IntPtr Next;
    }
}
