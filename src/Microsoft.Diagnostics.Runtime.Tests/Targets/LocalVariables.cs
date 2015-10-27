using System;

#pragma warning disable 0219

class NullRefTest
{
    public NullRefInner NullValue;
    public NullRefInner SetValue = new NullRefInner();
}

class NullRefInner
{
    public int i = 42;
}

class Program
{
    public static void Main(string[] args)
    {
        object foo = new Foo();
        NullRefTest containsnullref = new NullRefTest();

        Struct s = new Struct(1);
        Outer();
        GC.KeepAlive(foo);
        GC.KeepAlive(containsnullref);
    }

    private static void Outer()
    {
        float f = 42.0f;
        double d = 43.0;
        IntPtr ptr = new IntPtr(0x42424242);
        UIntPtr uptr = new UIntPtr(0x43434343);
        Middle();
    }

    private static void Middle()
    {
        byte b = 0x42;
        sbyte sb = 0x43;
        short sh = 0x4242;
        short ush = 0x4243;
        if (ush == 0x4243)
        {
            int i = 0x42424242;
            if (i == 0x42424242)
            {
                uint ui = 0x42424243;
                Inner();
            }
        }
    }

    private static void Inner()
    {
        bool b = true;
        char c = 'c';
        if (b)
        {
            string s = "hello world";
            throw new Exception();
        }
    }
}
