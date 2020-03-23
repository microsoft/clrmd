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
        public void GetFieldFrom_WhenStructureHasStructureField_ReturnsField(ClrValueType target, [Frozen]ClrType structType, ClrValueType rawStruct, ClrInstanceField structValueField)
        {
            // Arrange
            IAddressableTypedEntity entity = rawStruct;

            structValueField.IsValueType.Returns(true);
            structValueField.Type.Returns(entity.Type);
            structValueField.GetAddress(entity.Address, Arg.Any<bool>()).Returns(target.Address);

            // Act
            var structFromStruct = entity.GetFieldFrom(structValueField.Name);

            // Assert
            structFromStruct.Address.Should().Be(target.Address);
        }

        [Theory, AutoNSubstituteData]
        public void GetFieldFrom_WhenFieldFound_ReturnsField([Frozen] ClrHeap heap, ClrValueType target, [Frozen]ClrType clrObjectType, ClrObject rawClrObject, ClrInstanceField clrObjValueField)
        {
            clrObjValueField.IsValueType.Returns(true);
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
