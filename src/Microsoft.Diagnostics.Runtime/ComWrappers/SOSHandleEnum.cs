using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.ComWrappers
{
    internal sealed unsafe class SOSHandleEnum : CallableCOMWrapper
    {
        private static Guid IID_ISOSHandleEnum = new Guid("3E269830-4A2B-4301-8EE2-D6805B29B2FA");

        private readonly Next _next;

        public SOSHandleEnum(IntPtr pUnk)
            : base(ref IID_ISOSHandleEnum, pUnk)
        {
            ISOSHandleEnumVTable* vtable = (ISOSHandleEnumVTable*)_vtable;
            InitDelegate(ref _next, vtable->Next);
        }

        public int ReadHandles(HandleData[] handles)
        {
            if (handles == null)
                throw new ArgumentNullException(nameof(handles));

            int hr = _next(handles.Length, handles, out int read);
            return hr >= S_OK ? read : 0;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int Next(int count, [Out, MarshalAs(UnmanagedType.LPArray)] HandleData[] stackRefs, out int pNeeded);
    }


#pragma warning disable CS0169
#pragma warning disable CS0649
    internal struct ISOSHandleEnumVTable
    {
        private readonly IntPtr Skip;
        private readonly IntPtr Reset;
        private readonly IntPtr GetCount;
        public readonly IntPtr Next;
    }
}
