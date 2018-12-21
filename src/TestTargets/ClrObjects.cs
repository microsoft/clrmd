using System;

public class Program
{
  public static void Main(string[] args)
  {
    var primitiveObj = new PrimitiveTypeCarrier();

    throw new Exception();

    GC.KeepAlive(primitiveObj);
  }
}

public class PrimitiveTypeCarrier
{
  bool TrueBool = true;

  long OneLargerMaxInt = ((long)int.MaxValue + 1);

  DateTime Birthday = new DateTime(1992, 1, 24);

  SamplePointerType SamplePointer = new SamplePointerType();

  EnumType SomeEnum = EnumType.PickedValue;

  string HelloWorldString = "Hello World";

  Guid SampleGuid = new Guid("{EB06CEC0-5E2D-4DC4-875B-01ADCC577D13}");
}


public class SamplePointerType
{ }


public enum EnumType { Zero, One, Two, PickedValue }