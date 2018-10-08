using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

class GCHandles
{
    public static void Main()
    {
        GCHandle.Alloc("normal", GCHandleType.Normal);

        GCHandle.Alloc("pinned", GCHandleType.Pinned);

        string weak = "weak";
        GCHandle.Alloc(weak, GCHandleType.Weak);

        NativeOverlapped nativeOverlapped;

        unsafe
        {
            nativeOverlapped = *new System.Threading.Overlapped().UnsafePack((u, bytes, overlap) => { }, "state");
        }

        string weakLong = "weakLong";
        GCHandle.Alloc(weak, GCHandleType.WeakTrackResurrection);

        throw new Exception();

        GC.KeepAlive(nativeOverlapped);
        GC.KeepAlive(weak);
        GC.KeepAlive(weakLong);
    }
}