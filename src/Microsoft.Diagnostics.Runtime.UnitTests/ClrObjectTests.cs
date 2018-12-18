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
  }
}
