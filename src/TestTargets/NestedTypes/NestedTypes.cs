// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

// A type whose .cctor is never triggered by the test target. Used by the StaticFieldTests
// regression test for issue #1448 to verify that ClrMD reports IsInitialized==false (and
// GetAddress==0) for statics whose class constructor has not run, rather than reporting
// IsInitialized==true with a slot address pointing at zeroed memory.
public class UninitializedStatics
{
    public static int Int32Static = 100;
    public static string StringStatic = "uninitialized class string";
}

public class Program
{
    public static readonly int s_publicField;
    private static readonly int s_privateField;
    internal static readonly int s_internalField;
    protected static readonly int s_protectedField;

    public readonly int publicField;
    private readonly int privateField;
    internal readonly int internalField;
    protected readonly int protectedField;

    static Program()
    {
        s_privateField = s_publicField;
        s_internalField = s_privateField;
        s_protectedField = s_internalField;
    }

    Program()
    {
        privateField = publicField;
        internalField = privateField;
        protectedField = internalField;
    }

    private static void Main()
    {
        // Force the runtime to load UninitializedStatics' MethodTable (via typeof) without
        // running its .cctor, so the regression test for issue #1448 can find the type but
        // observe IsInitialized==false on its statics.
        GC.KeepAlive(typeof(UninitializedStatics));

        RuntimeHelpers.RunClassConstructor(typeof(PublicClass).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(PrivateClass).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(InternalClass).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(ProtectedClass).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(AbstractClass).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(SealedClass).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(StaticClass).TypeHandle);
        throw new Exception();
    }

    public class PublicClass
    {
    }

    private sealed class PrivateClass
    {
    }

    internal sealed class InternalClass
    {
    }

    protected class ProtectedClass
    {
    }

    abstract class AbstractClass
    {
    }

    sealed class SealedClass
    {
    }

    static class StaticClass
    {
    }

    // TODO
    /*
    protected internal sealed class ProtectedInternalClass
    {
    }

    private protected class PrivateProtectedClass
    {
    }
    */
}
