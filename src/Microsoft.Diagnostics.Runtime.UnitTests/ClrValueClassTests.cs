// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AutoFixture.Xunit2;
using FluentAssertions;
using NSubstitute;
using Xunit;

#pragma warning disable xUnit1026

namespace Microsoft.Diagnostics.Runtime.UnitTests
{
    public class ClrValueClassTests
    {
        [Theory, AutoNSubstituteData]
        public void Equals_WhenSameAddressAndTypes_ReturnsTrue([Frozen]ulong address, [Frozen]ClrType type, ClrValueClass uno, ClrValueClass dos)
        {
            // Act
            var areSame = uno.Equals(dos);

            // Assert
            areSame.Should().BeTrue();
        }

        [Theory, AutoNSubstituteData]
        public void Equals_WhenDifferentAddress_ReturnsFalse([Frozen]ClrType type, ClrValueClass uno, ClrValueClass dos)
        {
            // Act
            var areSame = uno.Equals(dos);

            // Assert
            areSame.Should().BeFalse();
        }

        [Theory, AutoNSubstituteData]
        public void Equals_WhenDifferentTypesButSameAddress_ReturnsFalse([Frozen]ulong address, ClrValueClass uno, ClrValueClass dos)
        {
            // Act
            var areSame = uno.Equals(dos);

            // Assert
            areSame.Should().BeFalse();
        }

        [Theory, AutoNSubstituteData]
        public void GetObjectField_WhenFieldFound_ReturnsField([Frozen] ClrHeap heap, ClrObject target, [Frozen]ClrType structType, ClrValueClass someStruct, ClrInstanceField structReferenceField, ulong fieldAddress)
        {
            // Arrange
            structReferenceField.IsObjectReference.Returns(true);
            structReferenceField.GetAddress(someStruct.Address, Arg.Any<bool>()).Returns(fieldAddress);

            heap.ReadPointer(fieldAddress, out var whatever)
                .Returns(call =>
               {
                   call[1] = target.Address;
                   return true;
               });

            // Act
            var structRefFieldTarget = someStruct.GetObjectField(structReferenceField.Name);

            // Assert
            structRefFieldTarget.Should().Be(target);
        }

        [Theory, AutoNSubstituteData]
        public void GetValueClassField_WhenFieldFound_ReturnsField(ClrValueClass target, [Frozen]ClrType structType, ClrValueClass someStruct, ClrInstanceField structValueField)
        {
            // Arrange
            structValueField.IsValueClass.Returns(true);
            structValueField.Type.Returns(target.Type);
            structValueField.GetAddress(someStruct.Address, Arg.Any<bool>()).Returns(target.Address);

            // Act
            var structRefFieldTarget = someStruct.GetValueClassField(structValueField.Name);

            // Assert
            structRefFieldTarget.Equals(target).Should().BeTrue();
        }
    }
}
