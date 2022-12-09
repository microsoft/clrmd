using System.Reflection;
using System.Runtime.InteropServices;

#pragma warning disable CS0169 // field is never used
#pragma warning disable CS0649 // field is never assigned
namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [DynamicInterfaceCastableImplementation]
    internal unsafe interface IDebugControlWrapper : IDebugControl
    {
        int IDebugControl.PointerSize
        {
            get
            {
                GetVTable(this, out nint self, out IDebugControlVtable* vtable);

                return vtable->IsPointer64Bit(self) == 0 ? 8 : 4;
            }
        }

        ImageFileMachine IDebugControl.CpuType
        {
            get
            {
                GetVTable(this, out nint self, out IDebugControlVtable* vtable);

                ImageFileMachine result = default;
                vtable->GetEffectiveProcessorType(self, &result);
                return result;
            }
        }

        int IDebugControl.WaitForEvent(TimeSpan timeout)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);

            uint milliseconds = timeout.TotalMilliseconds > uint.MaxValue ? uint.MaxValue : (uint)timeout.TotalMilliseconds;
            return vtable->WaitForEvent(self, 0, milliseconds);
        }

        void IDebugControl.Write(DEBUG_OUTPUT mask, string text)
        {
            GetVTable(this, out nint self, out IDebugControlVtable* vtable);

            fixed (char* textPtr = text)
                vtable->OutputWide(self, mask, textPtr);
        }

        private static void GetVTable(object ths, out nint self, out IDebugControlVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugControl;
            vtable = *(IDebugControlVtable**)self;
        }
    }
}
