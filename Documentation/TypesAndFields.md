# Types and Fields in CLRMD

## Introduction

An object's type in CLRMD is represented by `GCHeapType`. This class has two
sets of operations. The first is to provide data about an instance of that type.
For example, `GCHeapType` has functions for getting the length of an array,
getting the size of an object, and getting the field values for an object. The
second set is operations which tell you about the type itself such as what
fields it contains, what interfaces it implements, what methods the type has,
and so on.

In general, there is no wrapper types for an object in CLR MD, but other types
in CLR MD take an object address as a parameter to operate on.

## A short note about MethodTables

If you come from a background of using SOS or PSSCOR you might be asking, what
about MethodTables?

In the CLR runtime itself, there are several ways to represent a type. The
`MethodTable`, `EEClass`, and `TypeHandle` classes are all three different ways
that CLR represents what you would call a "Type" in managed code. SOS and PSSCOR
use the `MethodTable` in particular to represent types that are displayed to the
user (which is why you are likely most familiar with MethodTables if you have
ever worked with .Net Diagnostics).

Using a `MethodTable` for this is problematic for a few reasons. First, it does
not fully describe a type (a `TypeHandle` in CLR does). There are objects of
different types which share the same `MethodTable` (`object[]` and `string[]`,
for example). Second, a `MethodTable` is a per AppDomain representation of a
type. For example, this means there's a unique `MethodTable` for
`System.Xml.XmlAttribute` for each AppDomain in the process.

For these reason CLRMD does not natively use or expose MethodTables. That means
if you are trying to correlate output from CLR MD with output from SOS or
PSSCOR, you will need a way to get the MethodTable. Thankfully, this is very
easy, even though I don't give a direct way to the MethodTable through the API:

    CLRRuntime runtime = ...;
    ulong obj = ...; // An object address you got from somewhere.

    ulong mt;
    if (runtime.ReadPtr(obj, out mt))
    {
        // mt now contains obj's MethodTable.
    }

## Basic type information

We'll start with basic operations of a type. There are a few fairly self
explanatory functions and properties, such as `GCHeapType.Name` (the name of the
type) and `GCHeapType.GetSize`, which returns the size of an instance of that
type. Note that you must pass in an instance of an object to get its size since
there are variable-sized types in CLR. Similarly, `GCHeapType` has `IsArray`,
`IsException`, `IsEnum`, and `IsFree` which tells you if the type is an array of
some sort, is a subclass of `Exception`, an `Enum`, or free space on the heap,
respectively. (We'll cover free objects in more detail below.)

Another very basic thing that you can do with a type is enumerate the interfaces
it implements. Here is an example of doing that:

    GCHeapType type = ...;
    Console.WriteLine("Type {0} implements interfaces:");

    foreach (GCHeapInterface inter in type.Interfaces)
    {
        Console.WriteLine("    {0}", inter.Name);
    }

## Getting fields values

There are three types of fields in CLRMD: Instance fields (fields on an object's
instance), static fields (which are per-AppDomain), and thread static fields
(which are per-thread, per-AppDomain). We will use instance fields as the
starting point, but keep in mind static fields and thread static fields work
basically the same way.

At the simplest level you can request a field by name. For example, all
exception objects contains an `_HResult` field. Here is how you would get the
`_HResult` value for that object:

    if (type.IsException)
    {
        GCHeapInstanceField _hresult = type.GetFieldByName("_HResult");
        Debug.Assert(_hresult.ElementType == ClrElementType.ELEMENT_TYPE_I4);

        int value = (int)_hresult.GetFieldValue(obj);
        Console.WriteLine("Exception 0x{0:X} hresult: 0x{1:X}", obj, value);
    }

Note that `GCHeapInstanceField.GetFieldValue` returns an "object" which I've
blindly cast to an int here. That's because the underlying type of `_HResult` is
an int. Were the field a float, you would need to cast that to a float as well.
Similarly, all `System.String` objects come out as a regular "string" filled
with its contents. There are a few things you should know about using
`GetFieldValue`.

First, you may only call `GetFieldValue` if the
`GCHeapInstanceField.HasSimpleValue` returns true. In theory, you should always
check that property before calling `GetFieldValue`. However, in practice, only
fields which are value classes (C#'s "struct") cannot retrieve a value through
`GetFieldValue`. Calling `GetFieldValue` on anything where `HasSimpleValue` is
`false`, returns `null`. Second, you can tell what type `GetFieldValue` will
return (if you do not know it ahead of time) by looking at the value of
`GCHeapInstanceField.ElementType`.

This property (`GCHeapInstanceField.ElementType`) is equivalent to the
`CorElementType` of this field, as defined by the CLI spec. This tells you what
exact type will be returned in the "object" return value of `GetFieldValue`.
Here is an example of formatting the return value of `GetFieldValue` as
hexidecimal string for any pointer-types, and otherwise as a string
representation for the value for that type.

    static string GetOutput(ulong obj, GCHeapInstanceField field)
    {
        // If we don't have a simple value, return the address of the field in hex.
        if (!field.HasSimpleValue)
            return field.GetFieldAddress(obj).ToString("X");

        object value = field.GetFieldValue(obj);
        if (value == null)
            return "{error}";  // Memory corruption in the target process.

        // Decide how to format the string based on the underlying type of the field.
        switch (field.ElementType)
        {
            case ClrElementType.ELEMENT_TYPE_STRING:
                // In this case, value is the actual string itself.
                return (string)value;

            case ClrElementType.ELEMENT_TYPE_ARRAY:
            case ClrElementType.ELEMENT_TYPE_SZARRAY:
            case ClrElementType.ELEMENT_TYPE_OBJECT:
            case ClrElementType.ELEMENT_TYPE_CLASS:
            case ClrElementType.ELEMENT_TYPE_FNPTR:
            case ClrElementType.ELEMENT_TYPE_I:
            case ClrElementType.ELEMENT_TYPE_U:
                // These types are pointers.  Print as hex.
                return string.Format("{0:X}", value);

            default:
                // Everything else will look fine by simply calling ToString.
                return value.ToString();
        }
    }

## Working with embedded structs

As you've already seen, value classes (C#'s "structs") require special handling.
CLR recursively embeds structs in types, this means that if you had a class
defined as:

    struct Three
    {
        int b;
    }
    struct Two
    {
        int a;
        Three three;
        int c;
    }
    class One
    {
        int i;
        Two two;
        int j;
    }

What does the layout of an object of type "One" look like? It looks something
like this (on x86, and this layout may be completely different, numbers in hex):

    +00 int i;
    +04 int a;    <- start of struct "two"
    +08 int c;
    +0c int b;    <- start of struct "three";
    +10 int j;

As you can see, CLR has recursively embedded structs "Two" and "Three" into
class "One". This can have surprising consequences if you are attempting to
deeply inspect an object. For example, let's take the naive approach to walking
the "One" class:

    public static void WriteFields(ulong obj, GCHeapType type)
    {
        foreach (var field in type.Fields)
        {
            string output;
            if (field.HasSimpleValue)
                output = field.GetFieldValue(obj).ToString();
            else
                output = field.GetFieldAddress(obj).ToString("X");

            Console.WriteLine("  +{0,2:X2} {1} {2} = {3}", field.Offset, field.Type.Name, field.Name, output);
        }
    }

This works just fine, but you will not see fields a, b, or c! This prints out
something like:

    +00 System.Int32 i = 0
    +04 Two two = 27F2CB4
    +10 System.Int32 j = 0

As you can see, the "two" field has not been expanded. If that's what you are
looking for, great! You are done.

However, let's say you want to retrieve fields which are a part of an inlined
struct, embedded in an object. To do this, you will need to use the
`GetFieldValue` overload which accepts the "inner" parameter, signifying that
you are reading from an embedded struct. Here is a simple example of doing so,
where obj is of type "One":

        var twoField = type.GetFieldByName("two");
        var twoA = twoField.Type.GetFieldByName("a");
        var twoC = twoField.Type.GetFieldByName("c");

        // Get the address of the "two" field:
        ulong twoAddr = twoField.GetFieldAddress(obj);

        // Now get the value of a and c, note that we must pass true for the "inner" parameter.
        int a = (int)twoA.GetFieldValue(twoAddr, true);
        int c = (int)twoC.GetFieldValue(twoAddr, true);

As you can see, this gets quite complex, and requires you to understand the
layout of the object you are walking. You know that a field is embedded if
`field.ElementType == ClrElementType.ELEMENTTYPEVALUETYPE`. Here is another
example, one which recursively walks an object and prints out the values and
locations of all fields:

    public static void WriteFields(ulong obj, GCHeapType type)
    {
        WriteFieldsWorker(obj, type, null, 0, false);
    }

    static void WriteFieldsWorker(ulong obj, GCHeapType type, string baseName, int offset, bool inner)
    {
        // Keep track of nested fields.
        if (string.IsNullOrEmpty(baseName))
            baseName = "";
        else
            baseName += ".";

        foreach (var field in type.Fields)
        {
            ulong addr = field.GetFieldAddress(obj, inner);

            string output;
            if (field.HasSimpleValue)
                output = field.GetFieldValue(obj, inner).ToString();
            else
                output = addr.ToString("X");

            Console.WriteLine("  +{0,2:X2} {1} {2}{3} = {4}", field.Offset + offset, field.Type.Name, baseName, field.Name, output);

            // Recurse for structs.
            if (field.ElementType == ClrElementType.ELEMENT_TYPE_VALUETYPE)
                WriteFieldsWorker(addr, field.Type, baseName + field.Name, offset + field.Offset, true);
        }
    }

This would print out something like:

    +00 System.Int32 i = 0
    +04 Two two = 27F2CB4
    +0c Three three = 27F2CBc
    +04 System.Int32 two.a = 0
    +0c System.Int32 two.c = 0
    +08 System.Int32 two.b = 0
    +10 System.Int32 two.three.j = 0

## One last note about fields

As you can see, working with fields is unfortunately very complex in CLRMD. We
are forced into this complex design to accomodate the full expressiveness of
CLR's type system.

## Special Subtypes

There are several special "subtypes" that you should be aware of. For example,
Arrays, Enums, Exceptions, and Free objects. We will go into depth on each of
these.

## Arrays

Arrays in CLRMD are unfortunately another complex topic, again due to embedded
structs. Before we look at the complex case, let's look at an array of ints, as
this easy to work with. Note, I use a simple linq query to filter down to just
`int[]` objects (which is quite inefficient) for simplicity of the example:

    var intArrays = from o in heap.EnumerateObjects()
                    let t = heap.GetObjectType(o)
                    where t.IsArray && t.Name == "System.Int32[]"
                    select new { Address = o, Type = t };

    foreach (var item in intArrays)
    {
        GCHeapType type = item.Type;
        ulong obj = item.Address;

        int len = type.GetArrayLength(obj);

        Console.WriteLine("Object: {0:X}", obj);
        Console.WriteLine("Length: {0}", len);
        Console.WriteLine("Elements:");

        for (int i = 0; i < len; i++)
            Console.WriteLine("{0,3} - {1}", i, type.GetArrayElementValue(obj, i));
    }

At the very basic level, walking elements of an array is quite simple.
`GCHeapType.IsArray` will tell you the type is an array. Use the
`GetArrayLength` function to get the length of the array, and either
`GetArrayElementValue` or `GetArrayElementAddress` to get the address or value
of the field, respectively. Similar to fields, `GCHeapType` also provides the
`ArrayComponentType` property, telling you the component type of the array. You
can use: `type.ArrayComponentType.HasSimpleValue` or
`type.ArrayComponentType.ElementType == ClrElementType.ELEMENTTYPEVALUETYPE` to
tell if you are dealing with an array of value classes.

When working with an array of value types, they work very similarly to value
type fields. They are embedded in the array itself. So, let's look at an example
of walking all "arrays of structs" in a process and printing out the fields they
contain:

    foreach (ulong obj in heap.EnumerateObjects())
    {
        var type = heap.GetObjectType(obj);

        // Only consider types which are arrays that do not have simple values (I.E., are structs).
        if (!type.IsArray || type.ArrayComponentType.HasSimpleValue)
            continue;

        int len = type.GetArrayLength(obj);

        Console.WriteLine("Object: {0:X}", obj);
        Console.WriteLine("Type:   {0}", type.Name);
        Console.WriteLine("Length: {0}", len);
        for (int i = 0; i < len; i++)
        {
            ulong addr = type.GetArrayElementAddress(obj, i);
            foreach (var field in type.ArrayComponentType.Fields)
            {
                string output;
                if (field.HasSimpleValue)
                    output = field.GetFieldValue(addr, true).ToString();        // <- true here, as this is an embedded struct
                else
                    output = field.GetFieldAddress(addr, true).ToString("X");   // <- true here as well

                Console.WriteLine("{0}  +{1,2:X2} {2} {3} = {4}", i, field.Offset, field.Type.Name, field.Name, output);
            }
        }
    }

This would print out:

    Object: 27FAC28
    Type:   System.Collections.Generic.Dictionary.Entry<System.String,System.Resources.ResourceLocator>[]
    Length: 3
    0  +00 System.__Canon key = 41896456
    0  +08 System.__Canon value = 18446744071403970992
    0  +10 System.Int32 hashCode = 41921440
    0  +14 System.Int32 next = 0
    1  +00 System.__Canon key = 41923968
    1  +08 System.__Canon value = 122910689
    1  +10 System.Int32 hashCode = 41924480
    1  +14 System.Int32 next = 0
    2  +00 System.__Canon key = 41924008
    2  +08 System.__Canon value = 18446744070652986553
    2  +10 System.Int32 hashCode = 41924696
    2  +14 System.Int32 next = 0

Of course, these embedded structs can also have embedded structs within them!
You would need to recursively walk these structs (as we did earlier) to fully
walk the contents of these arrays.

## Enums

You can check if a type is an Enum by checking the `GCHeapType.IsEnum` property.
If a type is an Enum, you can get the list of enumeration values it contains
using `GCHeapType.GetEnumNames`. For example, let's say an enum was defined as:

    enum Numbers
    {
        Two = 2,
        Three = 3
        One = 1,
    }

`GetEnumNames` would return "One", "Two", and "Three". You can then feed these
values into `TryGetEnumValue`, which will give you the value of each defined
name. For example, `type.TryGetEnumValue("Three", out value)` would set
`value = 3`.

There is one major caveat to using enums in CLRMD. Enums in CLR can be of many
different types. Currently CLRMD only supports getting the enum value for "int"
(which is the most common type anyway). You can check what underlying type an
enum actually is using `GetEnumElementType`. The following code demonstrates
some of these concepts:

        // This walks the heap looking for enums, but keep in mind this works for field
        // values which are enums too.
        foreach (ulong obj in heap.EnumerateObjects())
        {
            var type = heap.GetObjectType(obj);
            if (!type.IsEnum)
                continue;

            // Enums do not have to be ints!  We will only handle the int case here.
            if (type.GetEnumElementType() != ClrElementType.ELEMENT_TYPE_I4)
                continue;

            int objValue = (int)type.GetValue(obj);

            bool found = false;
            foreach (var name in type.GetEnumNames())
            {
                int value;
                if (type.TryGetEnumValue(name, out value) && objValue == value)
                {
                    Console.WriteLine("{0} - {1}", value, name);
                    found = true;
                    break;
                }
            }

            if (!found)
                Console.WriteLine("{0} - {1}", objValue, "Unknown");
        }

## Exceptions

Exceptions in CLRMD are objects which derrive from `System.Exception`. CLR
itself does not actually make that distinction, almost any object can be thrown
as an exception (this is mostly to support C++/CLI which allows throwing non-
System.Exception objects). `GCHeapType.IsException` only returns true if an
object derives from `System.Exception`.

The `GCHeapType` class does not offer any properties or methods specific to
exceptions (other than `IsException`). Instead, it provides a wrapper class for
exception objects which gives you a wide variety of helpful properties for
exceptions. This includes the HRESULT of the exception, the exception message,
the type of the exception, an optional callstack and so on. Here is a very
simple example of using the `GCHeapException` object:

    foreach (ulong obj in heap.EnumerateObjects())
    {
        var type = heap.GetObjectType(obj);
        if (!type.IsException)
            continue;

        GCHeapException ex = heap.GetExceptionObject(obj);
        Console.WriteLine(ex.Type.Name);
        Console.WriteLine(ex.Message);
        Console.WriteLine(ex.HResult.ToString("X"));

        foreach (var frame in ex.StackTrace)
            Console.WriteLine(frame.ToString());
    }

Please note that we do not always have a stack trace available for every
exception. For example, if an exception object is constructed, but never throw,
it will not have an exception stack trace. In these cases,
`GCHeapException.StackTrace` will be a list of length 0 (so the above code will
simply not print a stack trace in that case).

## "Free" objects

When enumerating the heap, you will notice a lot of "Free objects". These are
denoted by the `GCHeapType.IsFree` property.

Free objects are not real objects in the strictest sense. They are actually
markers placed by the GC to denote free space on the heap. Free objects have no
fields (though they do have a size). In general, if you are trying to find heap
fragmentation, you will need to take a look at how many Free objects there are,
how big they are, and what lies between them. Otherwise, you should ignore them.

## Getting Methods

Enumerating methods on a type is currently not implemented (as of Beta 0.5).
This will be done before 1.0 is released.

## Enumerating all types

Previously, we have talked about types by starting from an object instance and
getting the type from it. However, there are also circumstances where you want
to simply enumerate all types in the runtime. For example, you may want to get
the values of all static variables in the process. To enumerate all types that
CLR currently knows about, you can call the `GCHeap.EnumerateTypes` function.
Enumerating all types in the runtime has a LOT of caveats, however. Before we
get to those, let's look at a quick example of printing out every static
variable in the runtime:

    foreach (var type in heap.EnumerateTypes())
    {
        foreach (CLRAppDomain appDomain in runtime.AppDomains)
        {
            foreach (GCHeapStaticField field in type.StaticFields)
            {
                if (field.HasSimpleValue)
                    Console.WriteLine("{0}.{1} ({2}) = {3}", type.Name, field.Name, appDomain.ID, field.GetFieldValue(appDomain));
            }
        }
    }

Similarly, this is how you would walk all ThreadStatic variables in your process:

    foreach (var type in heap.EnumerateTypes())
    {
        foreach (CLRAppDomain appDomain in runtime.AppDomains)
        {
            foreach (CLRThread thread in runtime.Threads)
            {
                foreach (GCHeapThreadStaticField field in type.ThreadStaticFields)
                {
                    if (field.HasSimpleValue)
                        Console.WriteLine("{0}.{1} ({2}, {3:X}) = {4}", type.Name, field.Name, appDomain.ID, thread.OSThreadId, field.GetFieldValue(appDomain, thread));
                }
            }
        }
    }

There are several caveats that you need to know about when using
`GCHeap.EnumerateTypes`. First, this function can be quite slow the first time
you call it. CLRMD is implemented on top of the old dac debugging interfaces,
and this is simply one place where we did not enumerate types in a smart or
performant way. You should expect a 1-3 second delay for a sizable program when
calling this function for the first time. The result is cached though, so
subsequent calls will be MUCH faster.

Second, this function only enumerates types which CLR currently knows about.
Let's say you have a class called "Foo" in your program which has no static
variables, and has never been constructed. While the class exists in the
metadata of your program, CLR has never needed it, so it never constructed it.
That means that it will NOT be one of the types enumerated to you. This again is
a limitation of the underlying API this library was implemented on top of. If
you want all of the types in your metadata, you will need to use the real
Metadata APIs, which CLRMD does not provide.

Third, some generics will be enumerated and some will not. Some generic
instantiations will be enumerated and some will not. Sometimes the runtime will
enumerate the "open form" of generics through this function. For example, we may
enumerate `Action<T>` (the open generic form), `Action<int>`, and
`Action<object>`, but not enumerate `Action<float>`. You will have to account
for this and work around it if you attempt to enumerate types in the runtime.

The thing to keep in mind here is that it was impossible for me to implement
EnumerateTypes in a sensible, consistent way due to the underlying API CLRMD is
built on. I would not have included it at all in the API surface area, except
that you need a way to do things like: Enumerate all statics in the process,
enumerate all types (that we can!) which implement `IDisposable`, and so on.
Please use caution when using this for anything "outside the box" and realize
the limitations of it.