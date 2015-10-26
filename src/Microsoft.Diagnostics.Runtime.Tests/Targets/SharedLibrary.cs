public class Foo
{
    int i = 42;
    string s = "string";
    bool b = true;
    float f = 4.2f;
    double d = 8.4;
    object o = new object();
    Struct st = new Struct(1);

    public string FooString = "Foo string";
    
    public void Bar() { }
    public void Baz() { }
    public int Baz(int i) { return i; }
}

public struct Struct
{
    int i;
    string s;
    bool b;
    float f;
    double d;
    object o;
    MiddleStruct middle;

    public Struct(int p)
    {
        i = 42;
        s = "string";
        b = true;
        f = 4.2f;
        d = 8.4;
        o = new object();
        middle = new MiddleStruct(p);
    }
}


public struct MiddleStruct
{
    int i;
    string s;
    bool b;
    float f;
    double d;
    object o;
    object inner;

    public MiddleStruct(int p)
    {
        i = 42;
        s = "string";
        b = true;
        f = 4.2f;
        d = 8.4;
        o = new object();

        inner = new InnerStruct(p);
    }
}


public struct InnerStruct
{
    int i;
    string s;
    bool b;
    float f;
    double d;
    object o;

    public InnerStruct(int p)
    {
        i = 42;
        s = "string";
        b = true;
        f = 4.2f;
        d = 8.4;
        o = new object();
    }
}
