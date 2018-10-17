using System;

public class Foo
{
    int i = 42;
    string s = "string";
    bool b = true;
    float f = 4.2f;
    double d = 8.4;
    object o = new object();

    public string FooString = "Foo string";

    public void Bar() { }
    public void Baz() { }
    public int Baz(int i) { return i; }
    public T5 GenericBar<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5> a) { return a(default(T1), default(T2), default(T3), default(T4)); }
}
