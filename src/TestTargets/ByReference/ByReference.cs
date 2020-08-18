using System;
using System.Threading;

internal static class Program
{
    private static readonly Barrier B = new Barrier(4 + 1);

    private static object O = new object();
    private static IntPtr I = new IntPtr();

    private static void Main()
    {
        new Thread(HeapReferenceTypeOuter).Start();
        new Thread(HeapValueTypeOuter).Start();
        new Thread(StackReferenceTypeOuter).Start();
        new Thread(StackValueTypeOuter).Start();

        B.SignalAndWait();
        Throw();
    }

    private static void HeapReferenceTypeOuter()
    {
        HeapReferenceType(ref O);
    }

    private static void HeapReferenceType(ref object o)
    {
        SignalAndSleep();
    }

    private static void HeapValueTypeOuter()
    {
        HeapValueType(ref I);
    }

    private static void HeapValueType(ref IntPtr i)
    {
        SignalAndSleep();
    }

    private static void StackReferenceTypeOuter()
    {
        object o = new object();
        StackReferenceType(ref o);
    }

    private static void StackReferenceType(ref object o)
    {
        SignalAndSleep();
    }

    private static void StackValueTypeOuter()
    {
        IntPtr i = new IntPtr();
        StackValueType(ref i);
    }

    private static void StackValueType(ref IntPtr i)
    {
        SignalAndSleep();
    }

    private static void SignalAndSleep()
    {
        B.SignalAndWait();
        Thread.Sleep(int.MaxValue);
    }

    private static void Throw()
    {
        throw new Exception();
    }
}
