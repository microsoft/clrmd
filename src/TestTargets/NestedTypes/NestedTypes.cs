using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

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

    private class PrivateClass
    {
    }

    internal class InternalClass
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
    protected internal class ProtectedInternalClass
    {
    }

    private protected class PrivateProtectedClass
    {
    }
    */
}
