// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Tests.Fixtures;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ClrObjectTests
    {
        public static IEnumerable<object[]> Variants => TestVariants.WithSingleFile(TestTargets.ClrObjects);

        // Per-test scope: load the ClrObjects dump for the given variant and locate
        // the primitive carrier object on the heap. The original fixture-based flow
        // (IClassFixture<ClrObjectConnection>) shared a single dump across the whole
        // class, which is fundamentally incompatible with a per-test variant matrix.
        private sealed class Scope : IDisposable
        {
            public DataTarget DataTarget { get; }
            public ClrRuntime Runtime { get; }
            public ClrObject PrimitiveCarrier { get; }
            public ClrObjectConnection.PrimitiveTypeCarrier Prototype { get; } = new();

            public Scope(DumpVariant variant, bool singleFile)
            {
                DataTarget = TestTargets.ClrObjects.LoadFullDump(variant, singleFile);
                Runtime = DataTarget.ClrVersions.Single().CreateRuntime();
                string typeName = nameof(ClrObjectConnection.PrimitiveTypeCarrier);
                PrimitiveCarrier = Runtime.Heap.EnumerateObjects().FirstOrDefault(o => o.Type?.Name == typeName);
                if (PrimitiveCarrier.IsNull)
                    throw new InvalidOperationException($"Could not find {typeName} on heap.");
            }

            public void Dispose() => DataTarget.Dispose();
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetField_WhenBool_ReturnsExpected(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            bool actual = s.PrimitiveCarrier.ReadField<bool>(nameof(s.Prototype.TrueBool));
            Assert.True(actual);
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetField_WhenLong_ReturnsExpected(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            long actual = s.PrimitiveCarrier.ReadField<long>(nameof(s.Prototype.OneLargerMaxInt));
            Assert.Equal(s.Prototype.OneLargerMaxInt, actual);
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetField_WhenEnum_ReturnsExpected(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrObjectConnection.EnumType enumValue = s.PrimitiveCarrier.ReadField<ClrObjectConnection.EnumType>(nameof(s.Prototype.SomeEnum));
            Assert.Equal(s.Prototype.SomeEnum, enumValue);
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetStringField_WhenStringField_ReturnsPointerToObject(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            string text = s.PrimitiveCarrier.ReadStringField(nameof(s.Prototype.HelloWorldString));
            Assert.Equal(s.Prototype.HelloWorldString, text);
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetStringField_WhenTypeMismatch_ThrowsInvalidOperation(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            Assert.Throws<InvalidOperationException>(() => s.PrimitiveCarrier.ReadStringField(nameof(s.Prototype.SomeEnum)));
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetObjectField_WhenStringField_ReturnsPointerToObject(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrObject textPointer = s.PrimitiveCarrier.ReadObjectField(nameof(s.Prototype.HelloWorldString));
            Assert.Equal(s.Prototype.HelloWorldString, (string)textPointer);
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetObjectField_WhenReferenceField_ReturnsPointerToObject(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrObject referenceFieldValue = s.PrimitiveCarrier.ReadObjectField(nameof(s.Prototype.SamplePointer));
            Assert.Equal("SamplePointerType", referenceFieldValue.Type.Name);
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetObjectField_WhenNonExistingField_ThrowsArgumentException(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            Assert.Throws<ArgumentException>(() => s.PrimitiveCarrier.ReadObjectField("nonExistingField"));
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetObjectField_WhenNull_ReturnsFieldType(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrObject nullRef = s.PrimitiveCarrier.ReadObjectField(nameof(s.Prototype.NullReference));
            Assert.True(nullRef.IsNull);
            Assert.NotNull(nullRef.Type);
            Assert.Equal("SamplePointerType", nullRef.Type.Name);
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetValueTypeField_WhenDateTime_ThrowsException(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrValueType birthday = s.PrimitiveCarrier.ReadValueTypeField(nameof(s.Prototype.Birthday));
            Assert.Equal(typeof(DateTime).FullName, birthday.Type.Name);
        }

        [Theory, MemberData(nameof(Variants))]
        public void GetValueTypeField_WhenGuid_ThrowsException(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrValueType sampleGuid = s.PrimitiveCarrier.ReadValueTypeField(nameof(s.Prototype.SampleGuid));
            Assert.Equal(typeof(Guid).FullName, sampleGuid.Type.Name);
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadFieldByInstance_WhenBool_ReturnsExpected(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrInstanceField field = s.PrimitiveCarrier.Type.GetFieldByName(nameof(s.Prototype.TrueBool));
            bool actual = s.PrimitiveCarrier.ReadField<bool>(field);
            Assert.True(actual);
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadFieldByInstance_WhenLong_ReturnsExpected(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrInstanceField field = s.PrimitiveCarrier.Type.GetFieldByName(nameof(s.Prototype.OneLargerMaxInt));
            long actual = s.PrimitiveCarrier.ReadField<long>(field);
            Assert.Equal(s.Prototype.OneLargerMaxInt, actual);
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadObjectFieldByInstance_WhenStringField_ReturnsExpected(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrInstanceField field = s.PrimitiveCarrier.Type.GetFieldByName(nameof(s.Prototype.HelloWorldString));
            ClrObject textPointer = s.PrimitiveCarrier.ReadObjectField(field);
            Assert.Equal(s.Prototype.HelloWorldString, (string)textPointer);
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadObjectFieldByInstance_WhenReferenceField_ReturnsExpected(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrInstanceField field = s.PrimitiveCarrier.Type.GetFieldByName(nameof(s.Prototype.SamplePointer));
            ClrObject referenceFieldValue = s.PrimitiveCarrier.ReadObjectField(field);
            Assert.Equal("SamplePointerType", referenceFieldValue.Type.Name);
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadStringFieldByInstance_ReturnsExpected(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrInstanceField field = s.PrimitiveCarrier.Type.GetFieldByName(nameof(s.Prototype.HelloWorldString));
            string text = s.PrimitiveCarrier.ReadStringField(field);
            Assert.Equal(s.Prototype.HelloWorldString, text);
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadValueTypeFieldByInstance_WhenDateTime_ReturnsExpected(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrInstanceField field = s.PrimitiveCarrier.Type.GetFieldByName(nameof(s.Prototype.Birthday));
            ClrValueType birthday = s.PrimitiveCarrier.ReadValueTypeField(field);
            Assert.Equal(typeof(DateTime).FullName, birthday.Type.Name);
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadValueTypeFieldByInstance_WhenGuid_ReturnsExpected(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrInstanceField field = s.PrimitiveCarrier.Type.GetFieldByName(nameof(s.Prototype.SampleGuid));
            ClrValueType sampleGuid = s.PrimitiveCarrier.ReadValueTypeField(field);
            Assert.Equal(typeof(Guid).FullName, sampleGuid.Type.Name);
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadObjectFieldByInstance_WhenNull_ThrowsArgumentNull(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            Assert.Throws<ArgumentNullException>(() => s.PrimitiveCarrier.ReadObjectField((ClrInstanceField)null));
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadFieldByInstance_WhenNull_ThrowsArgumentNull(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            Assert.Throws<ArgumentNullException>(() => s.PrimitiveCarrier.ReadField<int>((ClrInstanceField)null));
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadStringFieldByInstance_WhenNull_ThrowsArgumentNull(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            Assert.Throws<ArgumentNullException>(() => s.PrimitiveCarrier.ReadStringField((ClrInstanceField)null));
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadValueTypeFieldByInstance_WhenNull_ThrowsArgumentNull(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            Assert.Throws<ArgumentNullException>(() => s.PrimitiveCarrier.ReadValueTypeField((ClrInstanceField)null));
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadStringFieldByInstance_WhenTypeMismatch_ThrowsInvalidOperation(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrInstanceField field = s.PrimitiveCarrier.Type.GetFieldByName(nameof(s.Prototype.SomeEnum));
            Assert.Throws<InvalidOperationException>(() => s.PrimitiveCarrier.ReadStringField(field));
        }

        [Theory, MemberData(nameof(Variants))]
        public void ReadObjectFieldByInstance_WhenNotObjectRef_ThrowsArgumentException(DumpVariant variant, bool singleFile)
        {
            using Scope s = new(variant, singleFile);
            ClrInstanceField field = s.PrimitiveCarrier.Type.GetFieldByName(nameof(s.Prototype.TrueBool));
            Assert.Throws<ArgumentException>(() => s.PrimitiveCarrier.ReadObjectField(field));
        }
    }
}
