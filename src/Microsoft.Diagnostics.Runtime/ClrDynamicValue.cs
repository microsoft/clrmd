using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime
{
    class ClrDynamicValue : DynamicObject
    {
        private ClrValue _value;

        public ClrDynamicValue(ClrValue value)
        {
            _value = value;
        }


        //
        // Summary:
        //     Returns the enumeration of all dynamic member names.
        //
        // Returns:
        //     A sequence that contains dynamic member names.
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            // TODO:  This should return a list of member names this object supports.  For now,
            // returning '_value.Type.Fields.Select(f=>f.Name)' is probably correct.
            
            throw new NotImplementedException();
        }


        //
        // Summary:
        //     Provides implementation for binary operations. Classes derived from the System.Dynamic.DynamicObject
        //     class can override this method to specify dynamic behavior for operations such
        //     as addition and multiplication.
        //
        // Parameters:
        //   binder:
        //     Provides information about the binary operation. The binder.Operation property
        //     returns an System.Linq.Expressions.ExpressionType object. For example, for the
        //     sum = first + second statement, where first and second are derived from the DynamicObject
        //     class, binder.Operation returns ExpressionType.Add.
        //
        //   arg:
        //     The right operand for the binary operation. For example, for the sum = first
        //     + second statement, where first and second are derived from the DynamicObject
        //     class, arg is equal to second.
        //
        //   result:
        //     The result of the binary operation.
        //
        // Returns:
        //     true if the operation is successful; otherwise, false. If this method returns
        //     false, the run-time binder of the language determines the behavior. (In most
        //     cases, a language-specific run-time exception is thrown.)
        public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object result)
        {
            // TODO:  Comparison operators should be supported.  Such as: ==, !=, <, <=, >, >=, etc.
            //        I don't expect arithmetic operators (+, -, <<, etc) to be implemented here.
            //        This will likely be a big case statement.  Below is a rough sketch of how I think it should look.
            //        I've given the "ulong" case, most other ElementTypes need to be implemented.

            //        For 'arg', we basically accept a few inputs: A 'ClrObject', a 'ClrValue', a primitive (int, bool, etc), and null.
            
            // Design: In C#, what happens when you try to write the code:  "int i = 28; ulong j = 28; bool b = i == j;"?  (Compiler error.)
            //         What *should* happen if _value is a ulong (_value.ElementType == UInt64) and arg is an 'int'?  Should we do the conversion
            //         and do the comparison or throw an error?  Keep in mind that users of the "dynamic" keyword in C# expect more leniency in
            //         what is allowed, because they will get runtime errors instead of compiler errors...which is generally harder to deal with.

            switch (_value.ElementType)
            {
                case ClrElementType.Int32:
                    int value = _value.AsInt32();
                    int compareTo = 0;
                    
                    // Handle the 4 possible arg cases that we support.
                    if (arg is ClrValue)
                    {
                        compareTo = ((ClrValue)arg).AsInt32();
                    }
                    else if (arg is ClrObject)
                    {
                        ClrValue clrValue = ((ClrObject)arg).Unbox();
                        compareTo = clrValue.AsInt32();
                    }
                    else if (arg == null)
                    {
                        // Asking to compare an integer to null is incorrect.  Here we will 
                    }
                    else
                    {
                        // Whatever is left, we'll do the conversion.
                        compareTo = ((IConvertible)arg).ToInt32(null);
                    }

                    // Handle the operations that 'int' supports:  Just comparison for now.
                    switch (binder.Operation)
                    {
                        case ExpressionType.Equal:
                            result = value == compareTo;
                            return true;

                        case ExpressionType.GreaterThanOrEqual:
                            result = value >= compareTo;
                            return true;

                        case ExpressionType.LessThanOrEqual:
                            result = value <= compareTo;
                            return true;

                        case ExpressionType.LessThan:
                            result = value < compareTo;
                            return true;

                        case ExpressionType.GreaterThan:
                            result = value > compareTo;
                            return true;

                        default:
                            // This is basically our "not implemented" result, meaning we got an operation we didn't expect.
                            result = null;
                            return false;
                    }

                // NOTE:  For 'ElementType == ClrElementType.Object' you do the exact same thing that you would for "ulong",
                //        but instead of calling 'AsUInt64', you simply use: 'ulong value = _value.Address;'

                // TODO:  Most of the ClrElementType enum needs to be handled.  Ask questions if you run into trouble.

                default:
                    result = null;
                    return false;
            }




            throw new NotImplementedException();
        }

        //
        // Summary:
        //     Provides the implementation for operations that get member values. Classes derived
        //     from the System.Dynamic.DynamicObject class can override this method to specify
        //     dynamic behavior for operations such as getting a value for a property.
        //
        // Parameters:
        //   binder:
        //     Provides information about the object that called the dynamic operation. The
        //     binder.Name property provides the name of the member on which the dynamic operation
        //     is performed. For example, for the Console.WriteLine(sampleObject.SampleProperty)
        //     statement, where sampleObject is an instance of the class derived from the System.Dynamic.DynamicObject
        //     class, binder.Name returns "SampleProperty". The binder.IgnoreCase property specifies
        //     whether the member name is case-sensitive.
        //
        //   result:
        //     The result of the get operation. For example, if the method is called for a property,
        //     you can assign the property value to result.
        //
        // Returns:
        //     true if the operation is successful; otherwise, false. If this method returns
        //     false, the run-time binder of the language determines the behavior. (In most
        //     cases, a run-time exception is thrown.)
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            // TODO:  This is the most basic implementation.  DynamicTests.FieldValueTest tests this function.

            // TODO:  Here is a very general.
            // DESIGN:  Should this function assign result = a ClrDynamicValue when we are assigning a primitive value?
            //          Here I've taken the other choice as a starting point.


            if (binder.Name == "Length" && _value.IsArray)
            {
                result = _value.Length;
                return true;
            }


            // TODO:  This should be a switch which handles val.ElementType cases.
            ClrValue val = _value.GetField(binder.Name);

            switch (val.ElementType)
            {
                case ClrElementType.Int32:
                    result = val.AsInt32();
                    return true;

                case ClrElementType.Object:
                    result = new ClrDynamicValue(val);
                    return true;

                default:
                    // Not implemented:
                    result = null;
                    return false;
            }
        }

        //
        // Summary:
        //     Provides implementation for type conversion operations. Classes derived from
        //     the System.Dynamic.DynamicObject class can override this method to specify dynamic
        //     behavior for operations that convert an object from one type to another.
        //
        // Parameters:
        //   binder:
        //     Provides information about the conversion operation. The binder.Type property
        //     provides the type to which the object must be converted. For example, for the
        //     statement (String)sampleObject in C# (CType(sampleObject, Type) in Visual Basic),
        //     where sampleObject is an instance of the class derived from the System.Dynamic.DynamicObject
        //     class, binder.Type returns the System.String type. The binder.Explicit property
        //     provides information about the kind of conversion that occurs. It returns true
        //     for explicit conversion and false for implicit conversion.
        //
        //   result:
        //     The result of the type conversion operation.
        //
        // Returns:
        //     true if the operation is successful; otherwise, false. If this method returns
        //     false, the run-time binder of the language determines the behavior. (In most
        //     cases, a language-specific run-time exception is thrown.)
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            // TODO:  This needs to be implemented such that each primitive type is converted.
            //        See DynamicTests.DynamicConversionTest.

            // TODO:  Here is a basic implementation, but it needs to handle other cases as well, such as ensuring we
            //        don't convert things which shouldn't be converted.  Again, see DynamicTests.DynamicConversionTest
            //        for design notes.

            IConvertible tmp = (IConvertible)_value.Value;
            result = tmp.ToType(binder.Type, null);
            return true;
        }

       
        //
        // Summary:
        //     Provides the implementation for operations that get a value by index. Classes
        //     derived from the System.Dynamic.DynamicObject class can override this method
        //     to specify dynamic behavior for indexing operations.
        //
        // Parameters:
        //   binder:
        //     Provides information about the operation.
        //
        //   indexes:
        //     The indexes that are used in the operation. For example, for the sampleObject[3]
        //     operation in C# (sampleObject(3) in Visual Basic), where sampleObject is derived
        //     from the DynamicObject class, indexes[0] is equal to 3.
        //
        //   result:
        //     The result of the index operation.
        //
        // Returns:
        //     true if the operation is successful; otherwise, false. If this method returns
        //     false, the run-time binder of the language determines the behavior. (In most
        //     cases, a run-time exception is thrown.)
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            // TODO:  This is the basic (implementation).  Like TryGetMemeber though, this method should
            //        return a primitive type (ints, floats, etc) when 'value' (below) is of the given type.

            // DESIGN:  Should this throw InvalidOperationException, or just return
            //          false and let the binder figure out the exception?
            if (!_value.IsArray)
                throw new InvalidOperationException();

            if (indexes.Length != 1)
                throw new IndexOutOfRangeException();

            // DESIGN:  This will throw a cast exception if the user does "foo['c']" (or anything other
            //          than an int).  Is this the right exception?  Should we handle uints/longs/floats?  
            int index = (int)indexes[0];

            // TODO:  Based on value.ElementType, convert to its real type with "AsInt32", "AsFloat", etc.
            ClrValue value = _value[index];
            switch (value.ElementType)
            {
                case ClrElementType.Int32:
                    result = value.AsInt32();
                    return true;

                case ClrElementType.String:
                    result = value.AsString();
                    return true;

                default:
                    result = null;
                    return false;
            }
        }
        
        //
        // Summary:
        //     Provides implementation for unary operations. Classes derived from the System.Dynamic.DynamicObject
        //     class can override this method to specify dynamic behavior for operations such
        //     as negation, increment, or decrement.
        //
        // Parameters:
        //   binder:
        //     Provides information about the unary operation. The binder.Operation property
        //     returns an System.Linq.Expressions.ExpressionType object. For example, for the
        //     negativeNumber = -number statement, where number is derived from the DynamicObject
        //     class, binder.Operation returns "Negate".
        //
        //   result:
        //     The result of the unary operation.
        //
        // Returns:
        //     true if the operation is successful; otherwise, false. If this method returns
        //     false, the run-time binder of the language determines the behavior. (In most
        //     cases, a language-specific run-time exception is thrown.)
        public override bool TryUnaryOperation(UnaryOperationBinder binder, out object result)
        {
            // TODO: This is similar to TryBinaryOperation, but we only need to implement the ! operator for
            //       booleans, and throw a sensible exception for everything else.  Be sure to implement a test
            //       for this operation.

            throw new NotImplementedException();
        }
    }
}
