// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using AutoFixture.Xunit2;
using Microsoft.Diagnostics.Runtime.Tests.Fixtures;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class BasicArrayTests : IClassFixture<ArrayConnection>
    {
        private readonly ArrayConnection _connection;

        /// <summary>
        /// Snapshot of <see cref="ArraysHolder"/> object.
        /// </summary>
        private ClrObject _arrayHolder => _connection.TestDataClrObject;

        private ArrayConnection.ArraysHolder _prototype => _connection.Prototype;
        private ClrHeap _heap => _arrayHolder.Type.Heap;
        private IDataReader DataReader => _heap.Runtime.DataTarget.DataReader;

        public BasicArrayTests(ArrayConnection connection)
        {
            _connection = connection;
        }

        [Fact]
        public void Length_WhenPrimitiveValueTypeArray_ReturnsExpected()
        {
            // Arrange
            ClrArray intArraySnapshot = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.IntArray)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.IntArray.Length, intArraySnapshot.Length);
        }

        [Fact]
        public void Length_WhenGuidArray_ReturnsExpected()
        {
            // Arrange
            ClrArray guidArray = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.GuidArray)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.GuidArray.Length, guidArray.Length);
        }

        [Fact]
        public void Length_WhenDateTimeArray_ReturnsExpected()
        {
            // Arrange
            ClrArray dateTimeArray = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.DateTimeArray)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.DateTimeArray.Length, dateTimeArray.Length);
        }

        [Fact]
        public void Length_WhenCustomStructArray_ReturnsExpected()
        {
            // Arrange
            ClrArray customStructArray = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.StructArray)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.StructArray.Length, customStructArray.Length);
        }

        [Fact]
        public void Length_WhenStringArray_ReturnsExpected()
        {
            // Arrange
            ClrArray referenceArraySnapshot = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.StringArray)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.StringArray.Length, referenceArraySnapshot.Length);
        }

        [Fact]
        public void Length_WhenReferenceArrayWithBlanks_ReturnsExpected()
        {
            // Arrange
            ClrArray referenceArraySnapshot = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.ReferenceArrayWithBlanks)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.ReferenceArrayWithBlanks.Length, referenceArraySnapshot.Length);
        }

        [Theory, AutoData]
        public void GetArrayElementAddress_WhenGuidArray_Throws(int seed)
        {
            // Arrange
            Guid[] originalArray = _prototype.GuidArray;
            ClrObject structArray = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.GuidArray));

            int index = seed % originalArray.Length;

            // Act
            ulong elementAddress = structArray.Type.GetArrayElementAddress(structArray, index);

            // Assert
            Assert.NotEqual(default, elementAddress);
        }

        [Theory, AutoData]
        public void GetArrayElementAddress_WhenStringArray_ReturnsExpectedElement(int seed)
        {
            // Arrange
            string[] originalArray = _prototype.StringArray;
            ClrObject referenceArray = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.StringArray));

            int index = seed % originalArray.Length;

            // Act
            ulong elementAddress = referenceArray.Type.GetArrayElementAddress(referenceArray, index);

            DataReader.ReadPointer(elementAddress, out ulong actualValue);

            string actual = (string)_heap.GetObject(actualValue);

            // Assert
            Assert.Equal(originalArray[index], actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void GetArrayElementAddress_WhenReferenceArray_ReturnsExpectedElement(int setElementIndex)
        {
            // Arrange
            ClrObject referenceArray = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.ReferenceArrayWithBlanks));

            // Act
            ulong elementAddress = referenceArray.Type.GetArrayElementAddress(referenceArray, setElementIndex);

            DataReader.ReadPointer(elementAddress, out ulong actualPointer);

            ClrType pointerType = _connection.Runtime.Heap.GetObjectType(actualPointer);

            // Assert
            Assert.Equal(typeof(object).FullName, pointerType.Name);
        }

        [Theory, InlineData(1)]
        public void GetArrayElementAddress_WhenReferenceArrayHasNullElement_ReturnsNotEmptyPosition(int blankElementIndex)
        {
            // Arrange
            ClrObject referenceArray = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.ReferenceArrayWithBlanks));

            // Act
            ulong elementAddress = referenceArray.Type.GetArrayElementAddress(referenceArray, blankElementIndex);

            // Assert
            Assert.NotEqual(default, elementAddress);
        }

        [Theory, AutoData]
        public void GetArrayElementAddress_WhenCustomStructArray_GetsStructStartPos(int seed)
        {
            // Arrange
            ArrayConnection.SampleStruct[] originalArray = _prototype.StructArray;
            ClrObject structArray = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.StructArray));

            int index = seed % originalArray.Length;

            // Act
            ulong structStart = structArray.Type.GetArrayElementAddress(structArray, index);

            // Assert
            Assert.NotEqual(default, structStart);
        }

        [Theory, AutoData]
        public void GetArrayElementAddress_WhenCustomStructArray_ReturnsPointerToActualData(int seed)
        {
            // Arrange
            ArrayConnection.SampleStruct[] originalArray = _prototype.StructArray;
            ClrObject structArray = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.StructArray));

            int index = seed % originalArray.Length;
            ClrType structType = structArray.Type.ComponentType;
            ClrInstanceField textField = structType.GetFieldByName(nameof(ArrayConnection.SampleStruct.ReferenceLoad));

            // Act
            ulong structStart = structArray.Type.GetArrayElementAddress(structArray, index);

            DataReader.ReadPointer(structStart + (ulong)textField.Offset, out ulong textAddress);

            ClrObject obj = _heap.GetObject(textAddress);
            string text = obj.AsString();

            // Assert
            Assert.Equal(originalArray[index].ReferenceLoad, actual: text);
        }

        [Theory, AutoData]
        public void GetArrayElementAddress_WhenDateTimeArray_GetsStructStartPos(int seed)
        {
            // Arrange
            DateTime[] originalArray = _prototype.DateTimeArray;
            ClrObject structArray = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.DateTimeArray));

            int index = seed % originalArray.Length;

            // Act
            ulong structStart = structArray.Type.GetArrayElementAddress(structArray, index);

            // Assert
            Assert.NotEqual(default, structStart);
        }

        [Fact]
        public void GetArrayElementsValues_WhenIntArray_ReadAllValues()
        {
            // Arrange
            int[] originalArray = _prototype.IntArray;
            ClrArray intArraySnapshot = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.IntArray)).AsArray();

            // Act
            int[] ints = intArraySnapshot.ReadValues<int>(0, intArraySnapshot.Length);

            // Assert
            Assert.Equal(originalArray, ints);
        }

        [Fact]
        public void GetArrayElementsValues_WhenDateTimeArray_ReadAllValues()
        {
            // Arrange
            DateTime[] originalArray = _prototype.DateTimeArray;
            ClrArray datetimeArray = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.DateTimeArray)).AsArray();

            // Act
            DateTime[] datetimes = datetimeArray.ReadValues<DateTime>(0, datetimeArray.Length);

            // Assert
            Assert.Equal(originalArray, datetimes);
        }

        [Fact]
        public void GetArrayElementsValues_WhenDateTimeArray_ReadFirstValues()
        {
            const int fromEnd = 2;

            // Arrange
            DateTime[] originalArray = _prototype.DateTimeArray;
            ClrArray structArray = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.DateTimeArray)).AsArray();

            // Act
            DateTime[] datetimes = structArray.ReadValues<DateTime>(0, structArray.Length - fromEnd);

            // Assert
            Assert.Equal(originalArray[..^fromEnd], datetimes);
        }

        [Fact]
        public void GetArrayElementsValues_WhenDateTimeArray_ReadLastValues()
        {
            const int fromStart = 2;

            // Arrange
            DateTime[] originalArray = _prototype.DateTimeArray;
            ClrArray structArray = _arrayHolder.ReadObjectField(nameof(ArrayConnection.ArraysHolder.DateTimeArray)).AsArray();

            // Act
            DateTime[] datetimes = structArray.ReadValues<DateTime>(fromStart, structArray.Length - fromStart);

            // Assert
            Assert.Equal(originalArray[fromStart..], datetimes);
        }

        [Fact]
        public void GetStructValue_WhenPrimitiveValueTypeArray_ReturnsExpectedType()
        {
            // Arrange
            ClrArray intArraySnapshot = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.IntArray)).AsArray();

            // Act
            ClrValueType value = intArraySnapshot.GetStructValue(0);

            // Assert
            Assert.Equal("System.Int32", value.Type.Name);
        }

        [Fact]
        public void GetStructValue_WhenValueTypeArray_ReturnsExpectedValue()
        {
            // Arrange
            ClrArray structArraySnapshot = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.StructArray)).AsArray();

            // Act
            ClrValueType item = structArraySnapshot.GetStructValue(0);
            int intFieldValue = item.ReadField<int>("Number");
            UIntPtr stringFieldValue = item.ReadField<UIntPtr>("ReferenceLoad");
            ClrType fieldType = _heap.GetObjectType(stringFieldValue.ToUInt64());

            // Assert
            Assert.Equal(_prototype.StructArray[0].Number, intFieldValue);
            Assert.True(fieldType.IsString);
            Assert.Equal(_prototype.StructArray[0].ReferenceLoad, fieldType.Heap.GetObject(stringFieldValue.ToUInt64(), fieldType).AsString());
        }


        [Fact]
        public void GetStructValue_WhenCustomStructArray_ReturnsExpectedType()
        {
            // Arrange
            ClrArray customStructArray = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.StructArray)).AsArray();

            // Act
            ClrValueType value = customStructArray.GetStructValue(0);

            // Assert
            Assert.Equal("SampleStruct", value.Type.Name);
        }

        [Fact]
        public void GetStructValue_WhenNegativeIndex_Throws()
        {
            // Arrange
            ClrArray customStructArray = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.StructArray)).AsArray();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => customStructArray.GetStructValue(-1));
        }

        [Fact]
        public void GetStructValue_WhenReferenceTypeArray_Throws()
        {
            // Arrange
            ClrArray referenceArraySnapshot = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.StringArray)).AsArray();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => referenceArraySnapshot.GetStructValue(0));
        }

        [Theory]
        [InlineData(-2)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void GetObjectValue_WhenReferenceTypeArray_ReturnsExpectedValue(int offset)
        {
            // Arrange
            string[] originalArray = _prototype.StringArray;
            ClrArray referenceArraySnapshot = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.StringArray)).AsArray();
            int index = (offset < 0) ? originalArray.Length + offset : offset;

            // Act
            ClrObject value = referenceArraySnapshot.GetObjectValue(index);

            // Assert
            Assert.Equal(_prototype.StringArray[index], value.AsString());
        }

        [Fact]
        public void GetObjectValue_WhenValueTypeArray_Throws()
        {
            // Arrange
            ClrArray customStructArray = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.StructArray)).AsArray();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => customStructArray.GetObjectValue(0));
        }

        [Fact]
        public void GetObjectValue_WhenNegativeIndex_Throws()
        {
            // Arrange
            ClrArray referenceArraySnapshot = _arrayHolder
                .ReadObjectField(nameof(ArrayConnection.ArraysHolder.StringArray)).AsArray();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => referenceArraySnapshot.GetObjectValue(-1));
        }
    }
}
