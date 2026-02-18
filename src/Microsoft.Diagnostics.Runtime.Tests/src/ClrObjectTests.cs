// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;

            // Act
            bool actual = _primitiveCarrier.ReadField<bool>(nameof(prototype.TrueBool));

            // Assert
            Assert.True(actual);
        }

        [Fact]
        public void GetField_WhenLong_ReturnsExpected()
        {
            // Arrange
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;

            // Act
            long actual = _primitiveCarrier.ReadField<long>(nameof(prototype.OneLargerMaxInt));

            // Assert
            Assert.Equal(prototype.OneLargerMaxInt, actual);
        }

        [Fact]
        public void GetField_WhenEnum_ReturnsExpected()
        {
            // Arrange
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;

            // Act
            ClrObjectConnection.EnumType enumValue = _primitiveCarrier.ReadField<ClrObjectConnection.EnumType>(nameof(prototype.SomeEnum));

            // Assert
            Assert.Equal(prototype.SomeEnum, enumValue);
        }

        [Fact]
        public void GetStringField_WhenStringField_ReturnsPointerToObject()
        {
            // Arrange
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;

            // Act
            string text = _primitiveCarrier.ReadStringField(nameof(prototype.HelloWorldString));

            // Assert
            Assert.Equal(prototype.HelloWorldString, text);
        }

        [Fact]
        public void GetStringField_WhenTypeMismatch_ThrowsInvalidOperation()
        {
            // Arrange
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;

            // Act
            void readDifferentFieldTypeAsString() => _primitiveCarrier.ReadStringField(nameof(prototype.SomeEnum));

            // Assert
            Assert.Throws<InvalidOperationException>(readDifferentFieldTypeAsString);
        }

        [Fact]
        public void GetObjectField_WhenStringField_ReturnsPointerToObject()
        {
            // Arrange
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;

            // Act
            ClrObject textPointer = _primitiveCarrier.ReadObjectField(nameof(prototype.HelloWorldString));

            // Assert
            Assert.Equal(prototype.HelloWorldString, (string)textPointer);
        }

        [Fact]
        public void GetObjectField_WhenReferenceField_ReturnsPointerToObject()
        {
            // Arrange
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;

            // Act
            ClrObject referenceFieldValue = _primitiveCarrier.ReadObjectField(nameof(prototype.SamplePointer));

            // Assert
            Assert.Equal("SamplePointerType", referenceFieldValue.Type.Name);
        }

        [Fact]
        public void GetObjectField_WhenNonExistingField_ThrowsArgumentException()
        {
            // Arrange
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;

            // Act
            void readNonExistingField() => _primitiveCarrier.ReadObjectField("nonExistingField");

            // Assert
            Assert.Throws<ArgumentException>(readNonExistingField);
        }

        [Fact]
        public void GetValueTypeField_WhenDateTime_ThrowsException()
        {
            // Arrange
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;

            // Act
            ClrValueType birthday = _primitiveCarrier.ReadValueTypeField(nameof(prototype.Birthday));

            // Assert
            Assert.Equal(typeof(DateTime).FullName, birthday.Type.Name);
        }

        [Fact]
        public void GetValueTypeField_WhenGuid_ThrowsException()
        {
            // Arrange
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;

            // Act
            ClrValueType sampleGuid = _primitiveCarrier.ReadValueTypeField(nameof(prototype.SampleGuid));

            // Assert
            Assert.Equal(typeof(Guid).FullName, sampleGuid.Type.Name);
        }

        [Fact]
        public void ReadFieldByInstance_WhenBool_ReturnsExpected()
        {
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;
            ClrInstanceField field = _primitiveCarrier.Type.GetFieldByName(nameof(prototype.TrueBool));

            bool actual = _primitiveCarrier.ReadField<bool>(field);

            Assert.True(actual);
        }

        [Fact]
        public void ReadFieldByInstance_WhenLong_ReturnsExpected()
        {
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;
            ClrInstanceField field = _primitiveCarrier.Type.GetFieldByName(nameof(prototype.OneLargerMaxInt));

            long actual = _primitiveCarrier.ReadField<long>(field);

            Assert.Equal(prototype.OneLargerMaxInt, actual);
        }

        [Fact]
        public void ReadObjectFieldByInstance_WhenStringField_ReturnsExpected()
        {
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;
            ClrInstanceField field = _primitiveCarrier.Type.GetFieldByName(nameof(prototype.HelloWorldString));

            ClrObject textPointer = _primitiveCarrier.ReadObjectField(field);

            Assert.Equal(prototype.HelloWorldString, (string)textPointer);
        }

        [Fact]
        public void ReadObjectFieldByInstance_WhenReferenceField_ReturnsExpected()
        {
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;
            ClrInstanceField field = _primitiveCarrier.Type.GetFieldByName(nameof(prototype.SamplePointer));

            ClrObject referenceFieldValue = _primitiveCarrier.ReadObjectField(field);

            Assert.Equal("SamplePointerType", referenceFieldValue.Type.Name);
        }

        [Fact]
        public void ReadStringFieldByInstance_ReturnsExpected()
        {
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;
            ClrInstanceField field = _primitiveCarrier.Type.GetFieldByName(nameof(prototype.HelloWorldString));

            string text = _primitiveCarrier.ReadStringField(field);

            Assert.Equal(prototype.HelloWorldString, text);
        }

        [Fact]
        public void ReadValueTypeFieldByInstance_WhenDateTime_ReturnsExpected()
        {
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;
            ClrInstanceField field = _primitiveCarrier.Type.GetFieldByName(nameof(prototype.Birthday));

            ClrValueType birthday = _primitiveCarrier.ReadValueTypeField(field);

            Assert.Equal(typeof(DateTime).FullName, birthday.Type.Name);
        }

        [Fact]
        public void ReadValueTypeFieldByInstance_WhenGuid_ReturnsExpected()
        {
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;
            ClrInstanceField field = _primitiveCarrier.Type.GetFieldByName(nameof(prototype.SampleGuid));

            ClrValueType sampleGuid = _primitiveCarrier.ReadValueTypeField(field);

            Assert.Equal(typeof(Guid).FullName, sampleGuid.Type.Name);
        }

        [Fact]
        public void ReadObjectFieldByInstance_WhenNull_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => _primitiveCarrier.ReadObjectField((ClrInstanceField)null));
        }

        [Fact]
        public void ReadFieldByInstance_WhenNull_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => _primitiveCarrier.ReadField<int>((ClrInstanceField)null));
        }

        [Fact]
        public void ReadStringFieldByInstance_WhenNull_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => _primitiveCarrier.ReadStringField((ClrInstanceField)null));
        }

        [Fact]
        public void ReadValueTypeFieldByInstance_WhenNull_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => _primitiveCarrier.ReadValueTypeField((ClrInstanceField)null));
        }

        [Fact]
        public void ReadStringFieldByInstance_WhenTypeMismatch_ThrowsInvalidOperation()
        {
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;
            ClrInstanceField field = _primitiveCarrier.Type.GetFieldByName(nameof(prototype.SomeEnum));

            Assert.Throws<InvalidOperationException>(() => _primitiveCarrier.ReadStringField(field));
        }

        [Fact]
        public void ReadObjectFieldByInstance_WhenNotObjectRef_ThrowsArgumentException()
        {
            ClrObjectConnection.PrimitiveTypeCarrier prototype = _connection.Prototype;
            ClrInstanceField field = _primitiveCarrier.Type.GetFieldByName(nameof(prototype.TrueBool));

            Assert.Throws<ArgumentException>(() => _primitiveCarrier.ReadObjectField(field));
        }
    }
}
