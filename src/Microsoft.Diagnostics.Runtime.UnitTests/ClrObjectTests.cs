// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AutoFixture.Xunit2;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.UnitTests
{
    public class ClrObjectTests
    {
        [Theory, AutoNSubstituteData]
        public void Equals_WhenSameAddress_ReturnsTrue(ClrHeap heap, ClrType type, ulong address)
        {
            // Arrange
            heap.GetObjectType(address).Returns(type);
            type.Heap.Returns(heap);

            var uno = new ClrObject(address, type);
            var dos = new ClrObject(address, type);

            // Act
            var areSame = uno == dos;

            // Assert
            areSame.Should().BeTrue();
        }

        [Theory, AutoNSubstituteData]
        public void Equals_WhenDifferentAddress_ReturnsFalse(ClrHeap heap, ClrType type, ulong firstAddress, ulong secondAddress)
        {
            // Arrange
            heap.GetObjectType(firstAddress).Returns(type);
            heap.GetObjectType(secondAddress).Returns(type);
            type.Heap.Returns(heap);

            var first = new ClrObject(firstAddress, type);
            var second = new ClrObject(secondAddress, type);

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
        public void IsNull_WhenHasAddress_ReturnsFalse(ClrHeap heap, ClrType type, ulong address)
        {
            // Arrange
            heap.GetObjectType(address).Returns(type);
            type.Heap.Returns(heap);

            var clrObject = new ClrObject(address, type);

            // Act
            var isNull = clrObject.IsNull;

            // Assert
            isNull.Should().BeFalse();
        }

        [Theory, AutoNSubstituteData]
        public void ulongImplicitCast_WhenCalled_ReturnsAddress(ClrHeap heap, ClrType type, ulong objectAddress)
        {
            // Arrange
            heap.GetObjectType(objectAddress).Returns(type);
            type.Heap.Returns(heap);

            var clrObject = new ClrObject(objectAddress, type);

            // Act
            ulong ulongImplicitClrObject = clrObject;

            // Assert
            ulongImplicitClrObject.Should().Be(objectAddress);
        }


        [Theory, AutoNSubstituteData]
        public void GetObjectField_WhenTypeHasFieldWithName_FindsField(ClrHeap heap, ClrType objectType, ClrInstanceField clrField, ulong clrObj, ulong fieldAddress, ulong fieldValue, string fieldName)
        {
            // Arrange
            heap.GetObjectType(Arg.Is<ulong>(address => address == clrObj || address == fieldValue)).Returns(objectType);

            objectType.GetFieldByName(fieldName).Returns(clrField);
            clrField.IsObjectReference.Returns(true);
            objectType.Heap.Returns(heap);

            clrField.GetAddress(clrObj).Returns(fieldAddress);

            heap.ReadPointer(fieldAddress, out var whatever)
                .Returns
            (call =>
            {
                call[1] = fieldValue;
                return true;
            });

            // Act
            var fieldFoundByName = new ClrObject(clrObj, objectType).GetObjectField(fieldName);

            // Assert
            fieldFoundByName.Address.Should().Be(fieldValue);
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
        public void GetObjectField_WhenFieldDoesNotExistInType_ThrowsArgumentException(ClrHeap heap, ClrType type, ulong address, string unexistingField)
        {
            // Arrange
            heap.GetObjectType(address).Returns(type);
            type.Heap.Returns(heap);

            var clrObject = new ClrObject(address, type);

            // Act
            Action locateUnexistingField = () => clrObject.GetObjectField(unexistingField);

            // Assert
            locateUnexistingField.Should().Throw<ArgumentException>();
        }

        [Theory, AutoNSubstituteData]
        public void GetObjectField_WhenTypeHasValueTypeField_ThrowsArgumentException(ClrHeap heap, ClrType objectType, ClrInstanceField valueField, ulong clrObj, string valueFieldName)
        {
            // Arrange
            heap.GetObjectType(clrObj).Returns(objectType);

            objectType.GetFieldByName(valueFieldName).Returns(valueField);
            valueField.IsObjectReference.Returns(false);
            objectType.Heap.Returns(heap);

            var clrObject = new ClrObject(clrObj, objectType);

            // Act
            Action locateObjectFromValueTypeField = () => clrObject.GetObjectField(valueFieldName);

            // Assert
            locateObjectFromValueTypeField.Should().Throw<ArgumentException>();
        }


        [Theory, AutoNSubstituteData]
        public void GetObjectField_WhenHeapWasUnableToReadPointer_ThrowsMemoryReadException(ClrHeap heap, ClrType objectType, ClrInstanceField clrField, ulong clrObj, ulong corruptedFieldPointer, string fieldName)
        {
            // Arrange
            heap.GetObjectType(clrObj).Returns(objectType);

            objectType.GetFieldByName(fieldName).Returns(clrField);
            clrField.IsObjectReference.Returns(true);
            objectType.Heap.Returns(heap);

            clrField.GetAddress(clrObj).Returns(corruptedFieldPointer);

            heap.ReadPointer(corruptedFieldPointer, out var whatever).Returns(false);
  
            // Act
            Action locateObjectFromValueTypeField = () => new ClrObject(clrObj, objectType).GetObjectField(fieldName);
           
            // Assert
            locateObjectFromValueTypeField.Should().Throw<MemoryReadException>();
        }
    }
}
