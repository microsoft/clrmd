using System;

class Foo
{
    int i;
    string s;
    bool b;
    float f;
    object o;
}

class Types
{
    public static void Main(string[] args)
    {
        Foo f = new Foo();
        Foo[] foos = new Foo[] { f };

        throw new Exception();

        GC.KeepAlive(foos);
    }
}
