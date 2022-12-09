using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [DynamicInterfaceCastableImplementation]
    internal unsafe interface IDebugSystemObjectsWrapper : IDebugSystemObjects
    {
        int IDebugSystemObjects.ProcessSystemId
        {
            get
            {
                GetVTable(this, out nint self, out IDebugSystemObjectsVtable* vtable);

                if (vtable->GetCurrentProcessSystemId(self, out int id) < 0)
                    return -1;

                return id;
            }
        }

        int IDebugSystemObjects.CurrentThreadId
        {
            get
            {
                GetVTable(this, out nint self, out IDebugSystemObjectsVtable* vtable);

                if (vtable->GetCurrentThreadId(self, out int id) < 0)
                    return -1;

                return id;
            }

            set
            {
                GetVTable(this, out nint self, out IDebugSystemObjectsVtable* vtable);
                vtable->SetCurrentThreadId(self, value);
            }
        }

        int IDebugSystemObjects.GetCurrentThreadTeb(out ulong teb)
        {
            GetVTable(this, out nint self, out IDebugSystemObjectsVtable* vtable);
            return vtable->GetCurrentThreadTeb(self, out teb);
        }

        int IDebugSystemObjects.GetNumberThreads(out int threadCount)
        {
            GetVTable(this, out nint self, out IDebugSystemObjectsVtable* vtable);
            return vtable->GetNumberThreads(self, out threadCount);
        }


        private static void GetVTable(object ths, out nint self, out IDebugSystemObjectsVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugSymbols;
            vtable = *(IDebugSystemObjectsVtable**)self;
        }
    }
}
