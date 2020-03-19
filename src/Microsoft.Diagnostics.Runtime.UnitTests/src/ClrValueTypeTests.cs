// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AutoFixture.Xunit2;
using FluentAssertions;
using NSubstitute;
using Xunit;

#pragma warning disable xUnit1026
#pragma warning disable IDE0060

namespace Microsoft.Diagnostics.Runtime.UnitTests
{
    public class ClrValueTypeTests
    {
        [Theory, AutoNSubstituteData]
        public void Equals_WhenSameAddressAndTypes_ReturnsTrue([Frozen]ulong address, [Frozen]ClrType type, ClrValueType uno, ClrValueType dos)
        {
            // Act
            var areSame = uno.Equals(dos);

            // Assert
            areSame.Should().BeTrue();
        }

        [Theory, AutoNSubstituteData]
        public void Equals_WhenDifferentAddress_ReturnsFalse([Frozen]ClrType type, ClrValueType uno, ClrValueType dos)
        {
            // Act
            var areSame = uno.Equals(dos);

            // Assert
            areSame.Should().BeFalse();
        }

        [Theory, AutoNSubstituteData]
        public void Equals_WhenDifferentTypesButSameAddress_ReturnsFalse([Frozen]ulong address, ClrValueType uno, ClrValueType dos)
        {
            // Act
            var areSame = uno.Equals(dos);

            // Assert
            areSame.Should().BeFalse();
        }

        [Theory, AutoNSubstituteData]
        public void GetValueTypeField_WhenFieldFound_ReturnsField(ClrValueType target, [Frozen]ClrType structType, ClrValueType someStruct, ClrInstanceField structValueField)
        {
            // Arrange
            structValueField.IsValueType.Returns(true);
            structValueField.Type.Returns(target.Type);
            structValueField.GetAddress(someStruct.Address, Arg.Any<bool>()).Returns(target.Address);

            // Act
            var structRefFieldTarget = someStruct.GetValueTypeField(structValueField.Name);

            // Assert
            structRefFieldTarget.Equals(target).Should().BeTrue();
        }
    }
}
