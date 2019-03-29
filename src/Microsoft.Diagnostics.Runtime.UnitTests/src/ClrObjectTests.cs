// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AutoFixture.Xunit2;
using FluentAssertions;
using NSubstitute;
using Xunit;

#pragma warning disable xUnit1026

namespace Microsoft.Diagnostics.Runtime.UnitTests
{
    public class ClrObjectTests
    {
        [Theory, AutoNSubstituteData]
        public void Equals_WhenSameAddress_ReturnsTrue([Frozen] ClrHeap heap, [Frozen] ClrType type, [Frozen] ulong address, ClrObject first, ClrObject second)
        {
            // Act
            var areSame = first == second;

            // Assert
            areSame.Should().BeTrue();
        }

        [Theory, AutoNSubstituteData]
        public void Equals_WhenDifferentAddress_ReturnsFalse([Frozen] ClrHeap heap, [Frozen] ClrType type, ClrObject first, ClrObject second)
        {           
            // Act
            var areSame = first == second;

            // Assert
            areSame.Should().BeFalse();
        }

        [Fact]
        public void IsNull_WhenNoAddress_ReturnsTrue()
        {
            // Arrange
            var clrObj = new ClrObject(0, type: null);

            // Act
            var isNull = clrObj.IsNull;

            // Assert
            isNull.Should().BeTrue();
        }

        [Theory, AutoNSubstituteData]
        public void IsNull_WhenHasAddress_ReturnsFalse([Frozen] ulong objectAddress, ClrObject clrObject)
        {
            // Act
            var isNull = clrObject.IsNull;

            // Assert
            isNull.Should().BeFalse();
        }

        [Theory, AutoNSubstituteData]
        public void ulongImplicitCast_WhenCalled_ReturnsAddress([Frozen] ulong objectAddress, ClrObject clrObject)
        {
            // Act
            ulong ulongImplicitClrObject = clrObject;

            // Assert
            ulongImplicitClrObject.Should().Be(objectAddress);
        }

        [Theory, AutoNSubstituteData]
        public void GetObjectField_WhenTypeHasFieldWithName_FindsField([Frozen]ClrHeap heap, [Frozen]ClrType objectType, ClrObject source, ClrInstanceField clrField, ulong fieldAddress, ClrObject target)
        {
            // Arrange
            clrField.IsObjectReference.Returns(true);
            clrField.GetAddress(source.Address).Returns(fieldAddress);
          
            heap.ReadPointer(fieldAddress, out var whatever)
                .Returns(call =>
            {
                call[1] = target.Address;
                return true;
            });

            // Act
            var fieldFoundByName = source.GetObjectField(clrField.Name);

            // Assert
            fieldFoundByName.Address.Should().Be(target);
        }

        [Theory, AutoNSubstituteData]
        public void GetObjectField_WhenNullObject_ThrowsNullReference(string fieldName)
        {
            // Arrange
            var nullObject = new ClrObject(0, type: null);

            // Act
            Action locateFieldFromNullObject = () => nullObject.GetObjectField(fieldName);

            // Assert
            locateFieldFromNullObject.Should().Throw<NullReferenceException>();
        }

        [Theory, AutoNSubstituteData]
        public void GetObjectField_WhenFieldDoesNotExistInType_ThrowsArgumentException(ClrObject clrObject, string unexistingField)
        { 
            // Act
            Action locateUnexistingField = () => clrObject.GetObjectField(unexistingField);

            // Assert
            locateUnexistingField.Should().Throw<ArgumentException>();
        }

        [Theory, AutoNSubstituteData]
        public void GetObjectField_WhenTypeHasValueTypeField_ThrowsArgumentException([Frozen]ClrType objectType, ClrObject clrObject, ClrInstanceField valueField)
        {
            // Arrange   
            valueField.IsObjectReference.Returns(false);

            // Act
            Action locateObjectFromValueTypeField = () => clrObject.GetObjectField(valueField.Name);

            // Assert
            locateObjectFromValueTypeField.Should().Throw<ArgumentException>();
        }

        [Theory, AutoNSubstituteData]
        public void GetObjectField_WhenHeapWasUnableToReadPointer_ThrowsMemoryReadException([Frozen] ClrHeap heap, [Frozen]ClrType objectType, ClrObject clrObject, ClrInstanceField clrField, ulong corruptedFieldPointer)
        {
            // Arrange
            clrField.IsObjectReference.Returns(true);
            clrField.GetAddress(clrObject.Address).Returns(corruptedFieldPointer);

            heap.ReadPointer(corruptedFieldPointer, out var whatever).Returns(false);
  
            // Act
            Action locateObjectFromValueTypeField = () => clrObject.GetObjectField(clrField.Name);
           
            // Assert
            locateObjectFromValueTypeField.Should().Throw<MemoryReadException>();
        }

        [Theory, AutoNSubstituteData]
        public void GetValueClassField_WhenFieldFound_ReturnsField([Frozen] ClrHeap heap, ClrValueClass target, [Frozen]ClrType clrObjectType, ClrObject clrObject, ClrInstanceField clrObjValueField)
        {
            // Arrange
            clrObjValueField.IsValueClass.Returns(true);
            clrObjValueField.Type.Returns(target.Type);

            clrObjValueField
                .GetAddress(clrObject.Address)
                .Returns(target.Address);

            // Act
            var structRefFieldTarget = clrObject.GetValueClassField(clrObjValueField.Name);

            // Assert
            structRefFieldTarget.Equals(target).Should().BeTrue();
        }        
    }
}
