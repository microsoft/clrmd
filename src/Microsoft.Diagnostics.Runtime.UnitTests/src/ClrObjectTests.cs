// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AutoFixture.Xunit2;
using FluentAssertions;
using NSubstitute;
using Xunit;

#pragma warning disable xUnit1026
#pragma warning disable IDE0060

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
        public void GetObjectField_WhenNullObject_ThrowsInvalidOperationException(string fieldName)
        {
            // Arrange
            var nullObject = new ClrObject(0, type: null);

            // Act
            Action locateFieldFromNullObject = () => nullObject.GetObjectField(fieldName);

            // Assert
            locateFieldFromNullObject.Should().Throw<InvalidOperationException>();
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
        public void GetValueTypeField_WhenFieldFound_ReturnsField([Frozen] ClrHeap heap, ClrValueType target, [Frozen]ClrType clrObjectType, ClrObject clrObject, ClrInstanceField clrObjValueField)
        {
            // Arrange
            clrObjValueField.IsValueType.Returns(true);
            clrObjValueField.Type.Returns(target.Type);

            clrObjValueField
                .GetAddress(clrObject.Address)
                .Returns(target.Address);

            // Act
            var structRefFieldTarget = clrObject.GetValueTypeField(clrObjValueField.Name);

            // Assert
            structRefFieldTarget.Equals(target).Should().BeTrue();
        }
    }
}
