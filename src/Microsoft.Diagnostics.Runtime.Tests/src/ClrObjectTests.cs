// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Diagnostics.Runtime.Tests.Fixtures;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ClrObjectTests : IClassFixture<ClrObjectConnection>
    {
        private readonly ClrObjectConnection _connection;

        private ClrObject _primitiveCarrier => _connection.TestDataClrObject;

        public ClrObjectTests(ClrObjectConnection connection)
            => _connection = connection;

        [Fact]
        public void GetField_WhenBool_ReturnsExpected()
        {
            // Arrange 
            var prototype = _connection.Prototype;
            
            // Act
            bool actual = _primitiveCarrier.GetField<bool>(nameof(prototype.TrueBool));

            // Assert
            Assert.True(actual);
        }

        [Fact]
        public void GetField_WhenLong_ReturnsExpected()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            long actual = _primitiveCarrier.GetField<long>(nameof(prototype.OneLargerMaxInt));

            // Assert
            Assert.Equal(prototype.OneLargerMaxInt, actual);
        }

        [Fact]
        public void GetField_WhenEnum_ReturnsExpected()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            ClrObjectConnection.EnumType enumValue = _primitiveCarrier.GetField<ClrObjectConnection.EnumType>(nameof(prototype.SomeEnum));

            // Assert
            Assert.Equal(prototype.SomeEnum, enumValue);
        }

        [Fact]
        public void GetField_WhenDateTime_ThrowsException()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            void readDateTime() => _primitiveCarrier.GetField<DateTime>(nameof(prototype.Birthday));

            // Assert
            Assert.ThrowsAny<Exception>(readDateTime);
        }

        [Fact]
        public void GetField_WhenGuid_ThrowsException()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            void readGuid() => _primitiveCarrier.GetField<Guid>(nameof(prototype.SampleGuid));

            // Assert
            Assert.ThrowsAny<Exception>(readGuid);
        }

        [Fact]
        public void GetField_WhenTargetTypeMismatch_ThrowsException()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            void readingPointerAsBool() => _primitiveCarrier.GetField<bool>(nameof(prototype.SamplePointer));

            // Assert
            Assert.ThrowsAny<Exception>(readingPointerAsBool);
        }

        [Fact]
        public void GetStringField_WhenStringField_ReturnsPointerToObject()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            string text = _primitiveCarrier.GetStringField(nameof(prototype.HelloWorldString));

            // Assert
            Assert.Equal(prototype.HelloWorldString, text);
        }

        [Fact]
        public void GetStringField_WhenTypeMismatch_ThrowsInvalidOperation()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            void readDifferentFieldTypeAsString() => _primitiveCarrier.GetStringField(nameof(prototype.SomeEnum));

            // Assert
            Assert.Throws<InvalidOperationException>(readDifferentFieldTypeAsString);
        }

        [Fact]
        public void GetObjectField_WhenStringField_ReturnsPointerToObject()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            ClrObject textPointer = _primitiveCarrier.GetObjectField(nameof(prototype.HelloWorldString));

            // Assert
            Assert.Equal(prototype.HelloWorldString, (string)textPointer);
        }

        [Fact]
        public void GetObjectField_WhenReferenceField_ReturnsPointerToObject()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            ClrObject referenceFieldValue = _primitiveCarrier.GetObjectField(nameof(prototype.SamplePointer));

            // Assert
            Assert.Equal("SamplePointerType", referenceFieldValue.Type.Name);
        }

        [Fact]
        public void GetObjectField_WhenNonExistingField_ThrowsArgumentException()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            void readNonExistingField() => _primitiveCarrier.GetObjectField("nonExistingField");

            // Assert
            Assert.Throws<ArgumentException>(readNonExistingField);
        }

        [Fact]
        public void GetValueClassField_WhenDateTime_ThrowsException()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            ClrValueClass birthday = _primitiveCarrier.GetValueClassField(nameof(prototype.Birthday));

            // Assert
            Assert.Equal(typeof(DateTime).FullName, birthday.Type.Name);
        }

        [Fact]
        public void GetValueClassField_WhenGuid_ThrowsException()
        {
            // Arrange
            var prototype = _connection.Prototype;

            // Act
            ClrValueClass sampleGuid = _primitiveCarrier.GetValueClassField(nameof(prototype.SampleGuid));

            // Assert
            Assert.Equal(typeof(Guid).FullName, sampleGuid.Type.Name);
        }
    }
}
