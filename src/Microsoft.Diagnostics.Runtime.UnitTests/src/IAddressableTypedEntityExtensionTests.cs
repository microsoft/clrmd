// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AutoFixture.Xunit2;
using FluentAssertions;
using NSubstitute;
using Xunit;
using System;

#pragma warning disable xUnit1026

namespace Microsoft.Diagnostics.Runtime.UnitTests
{
    public class IAddressableTypedEntityExtensionTests
    {
        [Theory, AutoNSubstituteData]
        public void GetFieldFrom_WhenNullTypePassed_ThrowsArgumentNullException(IAddressableTypedEntity entity, string fieldName)
        {
            // Arrange 
            entity.Type.Returns((ClrType)null);

            Action getFieldFromTypelessEntity = () => entity.GetFieldFrom(fieldName);

            getFieldFromTypelessEntity
                .Should().Throw<ArgumentNullException>()
                .And.ParamName.Should().Be(nameof(entity));
        }

        [Theory, AutoNSubstituteData]
        public void GetFieldFrom_WhenClrObjectHasReferenceField_ReturnsField([Frozen]ClrHeap heap, [Frozen]ClrType objectType, ClrObject rawClrObject, ClrInstanceField clrField, ulong fieldAddress, ClrObject target)
        {
            // Arrange
            IAddressableTypedEntity entity = rawClrObject;

            clrField.IsObjectReference.Returns(true);
            clrField.GetAddress(entity.Address).Returns(fieldAddress);

            heap.ReadPointer(fieldAddress, out var whatever)
                .Returns(call =>
                {
                    call[1] = target.Address;
                    return true;
                });

            // Act
            var fieldFoundByName = entity.GetFieldFrom(clrField.Name);

            // Assert
            fieldFoundByName.Address.Should().Be(target.Address);
        }

        [Theory, AutoNSubstituteData]
        public void GetFieldFrom_WhenStructureHasReferenceField_ReturnsField([Frozen] ClrHeap heap, ClrObject target, [Frozen]ClrType structType, ClrValueClass rawStruct, ClrInstanceField structReferenceField, ulong fieldAddress)
        {
            // Arrange
            IAddressableTypedEntity entity = rawStruct;

            structReferenceField.IsObjectReference.Returns(true);
            structReferenceField.GetAddress(entity.Address, Arg.Any<bool>()).Returns(fieldAddress);

            heap.ReadPointer(fieldAddress, out var whatever)
                .Returns(call =>
                {
                    call[1] = target.Address;
                    return true;
                });

            // Act
            var fieldByName = entity.GetFieldFrom(structReferenceField.Name);

            // Assert
            fieldByName.Address.Should().Be(target.Address);
        }

        [Theory, AutoNSubstituteData]
        public void GetFieldFrom_WhenStructureHasStructureField_ReturnsField(ClrValueClass target, [Frozen]ClrType structType, ClrValueClass rawStruct, ClrInstanceField structValueField)
        {
            // Arrange
            IAddressableTypedEntity entity = rawStruct;
            
            structValueField.IsValueClass.Returns(true);
            structValueField.Type.Returns(entity.Type);
            structValueField.GetAddress(entity.Address, Arg.Any<bool>()).Returns(target.Address);

            // Act
            var structFromStruct = entity.GetFieldFrom(structValueField.Name);

            // Assert
            structFromStruct.Address.Should().Be(target.Address);
        }

        [Theory, AutoNSubstituteData]
        public void GetFieldFrom_WhenFieldFound_ReturnsField([Frozen] ClrHeap heap, ClrValueClass target, [Frozen]ClrType clrObjectType, ClrObject rawClrObject, ClrInstanceField clrObjValueField)
        {
            // Arrange
            IAddressableTypedEntity entity = rawClrObject;

            clrObjValueField.IsValueClass.Returns(true);
            clrObjValueField.Type.Returns(target.Type);

            clrObjValueField
                .GetAddress(rawClrObject.Address)
                .Returns(target.Address);

            // Act
            var structRefFieldTarget = rawClrObject.GetFieldFrom(clrObjValueField.Name);

            // Assert
            structRefFieldTarget.Equals(target).Should().BeTrue();
        }
    }
}
