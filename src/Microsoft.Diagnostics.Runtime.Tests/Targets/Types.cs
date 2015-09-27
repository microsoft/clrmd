using System;

class Types
{
    static Foo s_foo = new Foo();


    public static void Main(string[] args)
    {
        Foo f = new Foo();
        Foo[] foos = new Foo[] { f };

        Inner();

        GC.KeepAlive(foos);
    }

    private static void Inner()
    {
        throw new Exception();
    }
}
