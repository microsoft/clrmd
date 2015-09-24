using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

class GCHandles
{
    public static void Main()
    {
        GCHandle.Alloc("normal", GCHandleType.Normal);

        GCHandle.Alloc("pinned", GCHandleType.Pinned);

        string weak = "weak";
        GCHandle.Alloc(weak, GCHandleType.Weak);

        string weakLong = "weakLong";
        GCHandle.Alloc(weak, GCHandleType.WeakTrackResurrection);

        throw new Exception();

        GC.KeepAlive(weak);
        GC.KeepAlive(weakLong);
    }
}