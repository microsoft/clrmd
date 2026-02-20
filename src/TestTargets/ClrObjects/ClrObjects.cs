// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable 0162
using System;
using System.Collections.Generic;

public class Program
{
    public static void Main(string[] args)
    {
        var primitiveObj = new PrimitiveTypeCarrier();
        var genericObj = new GenericTypeCarrier();

        throw new Exception();

        GC.KeepAlive(primitiveObj);
        GC.KeepAlive(genericObj);
    }
}

public class PrimitiveTypeCarrier
{
    public bool TrueBool = true;

    public long OneLargerMaxInt = ((long)int.MaxValue + 1);

    public DateTime Birthday = new DateTime(1992, 1, 24);

    public SamplePointerType SamplePointer = new SamplePointerType();

    public EnumType SomeEnum = EnumType.PickedValue;

    public string HelloWorldString = "Hello World";

    public Guid SampleGuid = new Guid("{EB06CEC0-5E2D-4DC4-875B-01ADCC577D13}");

    public SamplePointerType NullReference = null;
}

public class SamplePointerType
{ }

public class GenericTypeCarrier
{
    public Dictionary<string, int> StringIntDictionary = new Dictionary<string, int> { { "hello", 1 }, { "world", 2 } };

    public LinkedList<string> StringLinkedList = CreateLinkedList();

    private static LinkedList<string> CreateLinkedList()
    {
        var list = new LinkedList<string>();
        list.AddLast("alpha");
        list.AddLast("beta");
        return list;
    }
}

public enum EnumType { Zero, One, Two, PickedValue }
