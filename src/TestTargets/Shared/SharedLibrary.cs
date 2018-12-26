using System;

#pragma warning disable 0414

public class Foo
{
    int i = 42;
    string s = "string";
    bool b = true;
    float f = 4.2f;
    double d = 8.4;
    object o = new object();
    Struct st = new Struct();

    public string FooString = "Foo string";

    public void Bar() { }
    public void Baz() { }
    public int Baz(int i) { return i; }
    public T5 GenericBar<T1, T2, T3, T4, T5>(GenericClass<T1, T2, T3, T4, T5> a) { return a.Invoke(default(T1), default(T2), default(T3), default(T4)); }
}

public struct Struct
{
    int j;
}

public class GenericClass<T1, T2, T3, T4, T5>
{
    public T5 Invoke(T1 a, T2 b, T3 te, T4 t4) { return default(T5); }
}
