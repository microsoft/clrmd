// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
                .GetObjectField(nameof(ArrayConnection.ArraysHolder.IntArray)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.IntArray.Length, intArraySnapshot.Length);
        }

        [Fact]
        public void Length_WhenGuidArray_ReturnsExpected()
        {
            // Arrange
            ClrArray guidArray = _arrayHolder
                .GetObjectField(nameof(ArrayConnection.ArraysHolder.GuidArray)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.GuidArray.Length, guidArray.Length);
        }

        [Fact]
        public void Length_WhenDateTimeArray_ReturnsExpected()
        {
            // Arrange
            ClrArray dateTimeArray = _arrayHolder
                .GetObjectField(nameof(ArrayConnection.ArraysHolder.DateTimeArray)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.DateTimeArray.Length, dateTimeArray.Length);
        }

        [Fact]
        public void Length_WhenCustomStructArray_ReturnsExpected()
        {
            // Arrange
            ClrArray customStructArray = _arrayHolder
                .GetObjectField(nameof(ArrayConnection.ArraysHolder.StructArray)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.StructArray.Length, customStructArray.Length);
        }

        [Fact]
        public void Length_WhenStringArray_ReturnsExpected()
        {
            // Arrange
            ClrArray referenceArraySnapshot = _arrayHolder
                .GetObjectField(nameof(ArrayConnection.ArraysHolder.StringArray)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.StringArray.Length, referenceArraySnapshot.Length);
        }

        [Fact]
        public void Length_WhenReferenceArrayWithBlanks_ReturnsExpected()
        {
            // Arrange
            ClrArray referenceArraySnapshot = _arrayHolder
                .GetObjectField(nameof(ArrayConnection.ArraysHolder.ReferenceArrayWithBlanks)).AsArray();

            // Act & Assert
            Assert.Equal(_prototype.ReferenceArrayWithBlanks.Length, referenceArraySnapshot.Length);
        }

        [Theory, AutoData]
        public void GetArrayElementValue_WhenIntTypeArray_ReturnsExpectedElement(int seed)
        {
            // Arrange
            var originalArray = _prototype.IntArray;
            ClrObject intArraySnapshot = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.IntArray));

            var index = seed % originalArray.Length;

            // Act
            int actual = (int)intArraySnapshot.Type.GetArrayElementValue(intArraySnapshot, index);

            // Assert
            Assert.Equal(originalArray[index], actual);
        }

        [Theory, AutoData]
        public void GetArrayElementValue_WhenStringArray_ReturnsExpectedElement(int seed)
        {
            // Arrange
            var originalArray = _prototype.StringArray;
            ClrObject referenceArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.StringArray));

            var index = seed % originalArray.Length;

            // Act
            string actual = (string)referenceArray.Type.GetArrayElementValue(referenceArray, index);

            // Assert
            Assert.Equal(originalArray[index], actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void GetArrayElementValue_WhenReferenceArray_ReturnsExpectedElement(int setElementIndex)
        {
            // Arrange
            ClrObject referenceArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.ReferenceArrayWithBlanks));

            // Act
            ulong actualPointer = (ulong)referenceArray.Type.GetArrayElementValue(referenceArray, setElementIndex);

            ClrType pointerType = _connection.Runtime.Heap.GetObjectType(actualPointer);

            // Assert
            Assert.Equal(typeof(object).FullName, pointerType.Name);
        }

        [Theory, InlineData(1)]
        public void GetArrayElementValue_WhenReferenceArrayHasNullElement_ReturnsEmptyObject(int blankElementIndex)
        {
            // Arrange
            ClrObject referenceArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.ReferenceArrayWithBlanks));

            // Act
            ulong actualPointer = (ulong)referenceArray.Type.GetArrayElementValue(referenceArray, blankElementIndex);

            // Assert
            Assert.Equal(default, actualPointer);
        }

        [Theory, AutoData]
        public void GetArrayElementValue_WhenCustomStructArray_Throws(int seed)
        {
            // Arrange
            var originalArray = _prototype.StructArray;
            ClrObject structArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.StructArray));

            var index = seed % originalArray.Length;

            // Assert
            Assert.ThrowsAny<Exception>(() => structArray.Type.GetArrayElementValue(structArray, index));
        }

        [Theory, AutoData]
        public void GetArrayElementValue_WhenDateTimeArray_Throws(int seed)
        {
            // Arrange
            var originalArray = _prototype.DateTimeArray;
            ClrObject structArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.DateTimeArray));

            var index = seed % originalArray.Length;

            // Assert
            Assert.ThrowsAny<Exception>(() => structArray.Type.GetArrayElementValue(structArray, index));
        }

        [Theory, AutoData]
        public void GetArrayElementValue_WhenGuidArray_Throws(int seed)
        {
            // Arrange
            var originalArray = _prototype.GuidArray;
            ClrObject structArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.GuidArray));

            var index = seed % originalArray.Length;

            // Assert
            Assert.ThrowsAny<Exception>(() => structArray.Type.GetArrayElementValue(structArray, index));
        }

        [Theory, AutoData]
        public void GetArrayElementAddress_WhenGuidArray_Throws(int seed)
        {
            // Arrange
            var originalArray = _prototype.GuidArray;
            ClrObject structArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.GuidArray));

            var index = seed % originalArray.Length;

            // Act
            ulong elementAddress = structArray.Type.GetArrayElementAddress(structArray, index);

            // Assert
            Assert.NotEqual(default, elementAddress);
        }

        [Theory, AutoData]
        public void GetArrayElementAddress_WhenStringArray_ReturnsExpectedElement(int seed)
        {
            // Arrange
            var originalArray = _prototype.StringArray;
            ClrObject referenceArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.StringArray));

            var index = seed % originalArray.Length;

            // Act
            ulong elementAddress = referenceArray.Type.GetArrayElementAddress(referenceArray, index);

            DataReader.ReadPointer(elementAddress, out var actualValue);

            string actual = (string)(new ClrObject(actualValue, _heap.GetObjectType(actualValue)));

            // Assert
            Assert.Equal(originalArray[index], actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void GetArrayElementAddress_WhenReferenceArray_ReturnsExpectedElement(int setElementIndex)
        {
            // Arrange
            ClrObject referenceArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.ReferenceArrayWithBlanks));

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
            ClrObject referenceArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.ReferenceArrayWithBlanks));

            // Act
            ulong elementAddress = referenceArray.Type.GetArrayElementAddress(referenceArray, blankElementIndex);

            // Assert
            Assert.NotEqual(default, elementAddress);
        }


        [Theory, AutoData]
        public void GetArrayElementAddress_WhenCustomStructArray_GetsStructStartPos(int seed)
        {
            // Arrange
            var originalArray = _prototype.StructArray;
            ClrObject structArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.StructArray));

            var index = seed % originalArray.Length;

            // Act
            ulong structStart = structArray.Type.GetArrayElementAddress(structArray, index);

            // Assert
            Assert.NotEqual(default, structStart);
        }

        [Theory, AutoData]
        public void GetArrayElementAddress_WhenCustomStructArray_ReturnsPointerToActualData(int seed)
        {
            // Arrange
            var originalArray = _prototype.StructArray;
            ClrObject structArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.StructArray));

            var index = seed % originalArray.Length;
            var structType = structArray.Type.ComponentType;
            var textField = structType.GetFieldByName(nameof(ArrayConnection.SampleStruct.ReferenceLoad));

            // Act
            ulong structStart = structArray.Type.GetArrayElementAddress(structArray, index);

            DataReader.ReadPointer(structStart + (ulong)textField.Offset, out var textAddress);

            ClrObject obj = _heap.GetObject(textAddress);
            string text = obj.AsString();

            // Assert
            Assert.Equal(originalArray[index].ReferenceLoad, actual: text);
        }

        [Theory, AutoData]
        public void GetArrayElementAddress_WhenDateTimeArray_GetsStructStartPos(int seed)
        {
            // Arrange
            var originalArray = _prototype.DateTimeArray;
            ClrObject structArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.DateTimeArray));

            var index = seed % originalArray.Length;

            // Act
            ulong structStart = structArray.Type.GetArrayElementAddress(structArray, index);

            // Assert
            Assert.NotEqual(default, structStart);
        }

        [Fact]
        public void GetArrayElementsValues_WhenIntArray_GetAllValues()
        {
            // Arrange
            var originalArray = _prototype.IntArray;
            ClrArray intArraySnapshot = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.IntArray)).AsArray();

            // Act
            int[] ints = intArraySnapshot.GetContent<int>(intArraySnapshot.Length);

            // Assert
            Assert.Equal(originalArray, ints);
        }

        [Fact]
        public void GetArrayElementsValues_WhenDateTimeArray_GetAllValues()
        {
            // Arrange
            var originalArray = _prototype.DateTimeArray;
            ClrArray datetimeArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.DateTimeArray)).AsArray();

            // Act
            DateTime[] datetimes = datetimeArray.GetContent<DateTime>(datetimeArray.Length);

            // Assert
            Assert.Equal(originalArray, datetimes);
        }

        [Fact]
        public void GetArrayElementsValues_WhenDateTimeArray_ReadLessThanAllValues()
        {
            // Arrange
            var originalArray = _prototype.DateTimeArray;
            ClrArray structArray = _arrayHolder.GetObjectField(nameof(ArrayConnection.ArraysHolder.DateTimeArray)).AsArray();
            int readLength = originalArray.Length - 2;

            // Act
            DateTime[] datetimes = structArray.GetContent<DateTime>(readLength);

            // Assert
            Assert.Equal(originalArray.AsSpan().Slice(0, readLength).ToArray(), datetimes);
        }
    }
}
