// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable 0414

interface IExplicitTest
{
    void ExplicitMethod();
}

class ExplicitImpl : IExplicitTest
{
    void IExplicitTest.ExplicitMethod() { }

    public void RegularMethod() { }
}

class Types
{
    [ThreadStatic]
    static int ts_threadId;

    [ThreadStatic]
    static string ts_threadName;

    static object s_one = new object();
    static object s_two = new object();
    static object s_three = new object();

    static object[] s_array = new object[] { s_one, s_two, s_three };
    static object[,] s_2dArray = new object[0, 0];
    static object[,,,,] s_5dArray = new object[2, 4, 6, 8, 10];

    static int[] s_szIntArray = new int[2];
    static Array s_mdIntArray = Array.CreateInstance(typeof(int), new[] { 2 }, new[] { 1 });
    static int[,] s_2dIntArray = new int[2, 4];

    static object[] s_szObjArray = new object[2];
    static Array s_mdObjArray = Array.CreateInstance(typeof(object), new[] { 2 }, new[] { 1 });
    static object[,] s_2dObjArray = new object[2, 4];

    static Foo s_foo = new Foo();
    static List<int> s_list = new List<int>();

    static object s_i = 42;

    delegate void TestDelegate1();
    static event TestDelegate1 TestEvent;

    delegate void TestDelegate2();

    static TestDelegate2 TestDelegate = new TestDelegate2(Inner);


    public static FileAccess s_enum = FileAccess.Read;

    private static async Task Async()
    {
        await Task.Delay(1000);
    }

    static Types()
    {
        s_szIntArray[1] = 42;
        s_mdIntArray.SetValue(42, 2);
        s_2dIntArray[1, 2] = 42;

        s_szObjArray[1] = s_szObjArray;
        s_mdObjArray.SetValue(s_mdObjArray, 2);
        s_2dObjArray[1, 2] = s_2dObjArray;

        TestEvent += Inner;
        TestEvent += new Types().InstanceMethod;
    }

    public static void Main()
    {
        new StructTestClass(); // Ensure type is constructed
        ExplicitImpl ei = new ExplicitImpl();
        ((IExplicitTest)ei).ExplicitMethod();
        ei.RegularMethod();
        Foo f = new Foo();
        Foo[] foos = new Foo[] { f };
        Task task = Async();

        // Ensure InstanceMethod is JIT'd so delegate tests can resolve it
        try { new Types().InstanceMethod(); } catch { }

        // Force a full sync block on s_foo for VerifyHeapTests.
        // GetHashCode stores the identity hash in the object header, then
        // Monitor.Enter forces promotion to a full sync block since the
        // header can't hold both the hash and the thin lock simultaneously.
        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s_foo);
        Monitor.Enter(s_foo);

        // Set thread-static values on the main thread only
        ts_threadId = Environment.CurrentManagedThreadId;
        ts_threadName = "MainThread";

        // Exercise struct interface dispatch to create unboxing stub + JIT the implementation
        IStructTest structTest = new StructWithInterface();
        int structResult = structTest.TestMethod();

        Inner();

        GC.KeepAlive(foos);
        GC.KeepAlive(structResult);
    }

    private static void Inner()
    {
        throw new Exception();
    }

    private void InstanceMethod()
    {
        TestEvent.Invoke();
    }
}
