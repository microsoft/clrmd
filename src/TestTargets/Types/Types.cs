using System;
using System.Collections.Generic;
using System.IO;

class Types
{
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

    public static FileAccess s_enum = FileAccess.Read;

    static Types()
    {
        s_szIntArray[1] = 42;
        s_mdIntArray.SetValue(42, 2);
        s_2dIntArray[1, 2] = 42;

        s_szObjArray[1] = s_szObjArray;
        s_mdObjArray.SetValue(s_mdObjArray, 2);
        s_2dObjArray[1, 2] = s_2dObjArray;
    }

    public static void Main()
    {
        new StructTestClass(); // Ensure type is constructed
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
