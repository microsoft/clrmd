// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        // Force JIT compilation of generic method with reference-type parameter and
        // store the instantiation-specific MethodDesc handle for test lookup.
        string echoResult = GenericStaticMethod.Echo("test");
        GC.KeepAlive(echoResult);
        MethodInfo echoStr = typeof(GenericStaticMethod).GetMethod("Echo").MakeGenericMethod(typeof(string));
        RuntimeHelpers.PrepareMethod(echoStr.MethodHandle);
        GenericStaticMethod.EchoStringMethodHandle = echoStr.MethodHandle.Value;

        // Force JIT compilation of generic method with value-type parameter (issue #935
        // exact scenario: generic method M<T>() called with int). The MethodDesc for
        // Echo<int> should have HasNativeCode != 0 and correct CompilationType/HotSize.
        int echoIntResult = GenericStaticMethod.Echo(42);
        GC.KeepAlive(echoIntResult);
        MethodInfo echoInt = typeof(GenericStaticMethod).GetMethod("Echo").MakeGenericMethod(typeof(int));
        RuntimeHelpers.PrepareMethod(echoInt.MethodHandle);
        GenericStaticMethod.EchoIntMethodHandle = echoInt.MethodHandle.Value;

        // Force JIT of GenericClass<...>.Invoke and store the per-instantiation MethodDesc.
        // For ref-type generic types, the per-instantiation MethodDesc may have HasNativeCode=0
        // because the JIT'd code lives on the canonical (__Canon) desc, exercising the
        // slot-based fallback path in DacMethodLocator.GetMethodInfo.
        GenericClass<bool, int, float, string, object> gc = new GenericClass<bool, int, float, string, object>();
        object invokeResult = gc.Invoke(false, 0, 0f, "test");
        GC.KeepAlive(invokeResult);
        MethodInfo invokeMethod = typeof(GenericClass<bool, int, float, string, object>).GetMethod("Invoke");
        RuntimeHelpers.PrepareMethod(invokeMethod.MethodHandle,
            new RuntimeTypeHandle[] {
                typeof(bool).TypeHandle, typeof(int).TypeHandle,
                typeof(float).TypeHandle, typeof(string).TypeHandle, typeof(object).TypeHandle
            });
        GenericStaticMethod.GenericClassInvokeMethodHandle = invokeMethod.MethodHandle.Value;

        // Exercise RefGenericClass with two different reference-type instantiations.
        // On .NET Core, RefGenericClass<string> and RefGenericClass<object> share JIT'd
        // code via the canonical __Canon instantiation. JIT the code via <string> first,
        // then store the <object> MethodDesc. The <object> per-instantiation MethodDesc
        // may have HasNativeCode=0 because the code lives on the canonical desc.
        RefGenericClass<string> rgs = new RefGenericClass<string>();
        string rgsResult = rgs.GetValue("hello");
        GC.KeepAlive(rgsResult);

        RefGenericClass<object> rgo = new RefGenericClass<object>();
        object rgoResult = rgo.GetValue(new object());
        GC.KeepAlive(rgoResult);

        MethodInfo getValueObj = typeof(RefGenericClass<object>).GetMethod("GetValue");
        RuntimeHelpers.PrepareMethod(getValueObj.MethodHandle,
            new RuntimeTypeHandle[] { typeof(object).TypeHandle });
        GenericStaticMethod.RefGenericGetValueMethodHandle = getValueObj.MethodHandle.Value;

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
