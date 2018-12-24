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
            // Act
            bool actual = _primitiveCarrier.GetField<bool>("TrueBool");

            // Assert
            Assert.True(actual);
        }

        [Fact]
        public void GetField_WhenLong_ReturnsExpected()
        {
            // Arrange
            long expected = (long)int.MaxValue + 1;

            // Act
            long actual = _primitiveCarrier.GetField<long>("OneLargerMaxInt");

            // Assert
            Assert.Equal(actual, expected);
        }

        [Fact]
        public void GetField_WhenEnum_ReturnsExpected()
        {
            // Act
            EnumType enumValue = _primitiveCarrier.GetField<EnumType>("SomeEnum");

            // Assert
            Assert.Equal(EnumType.PickedValue, enumValue);
        }

        [Fact]
        public void GetField_WhenDateTime_ThrowsException()
        {
            // Act
            void readDateTime() => _primitiveCarrier.GetField<DateTime>("Birthday");

            // Assert
            Assert.ThrowsAny<Exception>(readDateTime);
        }

        [Fact]
        public void GetField_WhenGuid_ThrowsException()
        {
            // Act
            void readGuid() => _primitiveCarrier.GetField<Guid>("SampleGuid");

            // Assert
            Assert.ThrowsAny<Exception>(readGuid);
        }

        [Fact]
        public void GetField_WhenTargetTypeMismatch_ThrowsException()
        {
            // Act
            void readingPointerAsBool() => _primitiveCarrier.GetField<bool>("SamplePointerType");

            // Assert
            Assert.ThrowsAny<Exception>(readingPointerAsBool);
        }

        [Fact]
        public void GetStringField_WhenStringField_ReturnsPointerToObject()
        {
            // Act
            string text = _primitiveCarrier.GetStringField("HelloWorldString");

            // Assert
            Assert.Equal("Hello World", text);
        }

        [Fact]
        public void GetStringField_WhenTypeMismatch_ThrowsInvalidOperation()
        {
            // Act
            void readDifferentFieldTypeAsString() => _primitiveCarrier.GetStringField("SomeEnum");

            // Assert
            Assert.Throws<InvalidOperationException>(readDifferentFieldTypeAsString);
        }

        [Fact]
        public void GetObjectField_WhenStringField_ReturnsPointerToObject()
        {
            // Act
            ClrObject textPointer = _primitiveCarrier.GetObjectField("HelloWorldString");

            // Assert
            Assert.Equal("Hello World", (string)textPointer);
        }

        [Fact]
        public void GetObjectField_WhenReferenceField_ReturnsPointerToObject()
        {
            // Act
            ClrObject referenceFieldValue = _primitiveCarrier.GetObjectField("SamplePointer");

            // Assert
            Assert.Equal("SamplePointerType", referenceFieldValue.Type.Name);
        }

        [Fact]
        public void GetObjectField_WhenNonExistingField_ThrowsArgumentException()
        {
            // Act
            void readNonExistingField() => _primitiveCarrier.GetObjectField("nonExistingField");

            // Assert
            Assert.Throws<ArgumentException>(readNonExistingField);
        }

        [Fact]
        public void GetValueClassField_WhenDateTime_ThrowsException()
        {
            // Act
            ClrValueClass birthday = _primitiveCarrier.GetValueClassField("Birthday");

            // Assert
            Assert.Equal(typeof(DateTime).FullName, birthday.Type.Name);
        }

        [Fact]
        public void GetValueClassField_WhenGuid_ThrowsException()
        {
            // Act
            ClrValueClass sampleGuid = _primitiveCarrier.GetValueClassField("SampleGuid");

            // Assert
            Assert.Equal(typeof(Guid).FullName, sampleGuid.Type.Name);
        }

        /// <summary>
        /// Must be alligned with enum declared in source ClrObjects testTarget.
        /// </summary>
        public enum EnumType { Zero, One, Two, PickedValue }
    }
}
