// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class StaticFieldTests
    {
        [FrameworkFact]
        public void StaticValueAppDomainTests()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule[] sharedModules = runtime.EnumerateModules().Where(m => Path.GetFileName(m.Name).Equals("sharedlibrary.dll", StringComparison.OrdinalIgnoreCase)).ToArray();

            Assert.Equal(2, sharedModules.Length);
            Assert.NotEqual(sharedModules[0].AppDomain, sharedModules[1].AppDomain);

            ClrType staticType1 = sharedModules[0].GetTypeByName("SharedStaticTest");
            ClrType staticType2 = sharedModules[1].GetTypeByName("SharedStaticTest");

            Assert.NotNull(staticType1);
            Assert.NotNull(staticType2);
            Assert.NotEqual(staticType1, staticType2);

            int value2 = staticType1.StaticFields.Single().Read<int>(staticType1.Module.AppDomain);
            int value42 = staticType2.StaticFields.Single().Read<int>(staticType2.Module.AppDomain);

            if (value2 > value42)
            {
                int tmp = value2;
                value2 = value42;
                value42 = tmp;
            }

            Assert.Equal(2, value2);
            Assert.Equal(42, value42);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StringEmptyTest(bool singleFile)
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrType strType = runtime.Heap.StringType;
            System.Collections.Immutable.ImmutableArray<ClrStaticField> statics = strType.StaticFields;
            ulong valueSlot = Assert.Single(statics).GetAddress(runtime.AppDomains[0]);
            Assert.NotEqual(0ul, valueSlot);

            ulong address = dt.DataReader.ReadPointer(valueSlot);

            Assert.NotEqual(0ul, address);
            ClrObject obj = runtime.Heap.GetObject(address);
            Assert.True(obj.Type.IsString);

            string strValue = obj.AsString();
            Assert.Equal("", strValue);

            ClrSegment seg = runtime.Heap.GetSegmentByAddress(valueSlot);
            Assert.NotNull(seg);

            ulong prev = runtime.Heap.FindPreviousObjectOnSegment(valueSlot);
            Assert.NotEqual(0ul, prev);

            ClrObject staticsArray = runtime.Heap.GetObject(prev);
            Assert.True(staticsArray.IsValid);
            Assert.True(staticsArray.IsArray);
        }

        /// <summary>
        /// Regression test for issue #1302: GetAddress on a thread that hasn't initialized
        /// a [ThreadStatic] field must return 0, not a bogus offset-only address.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ThreadStaticField_UninitializedThread_ReturnsZeroAddress(bool singleFile)
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("types.dll");
            ClrType type = module.GetTypeByName("Types");
            Assert.NotNull(type);

            Assert.NotEmpty(type.ThreadStaticFields);
            ClrThreadStaticField field = type.ThreadStaticFields.Single(f => f.Name == "ts_threadName");

            bool foundInitialized = false;
            bool foundUninitialized = false;

            foreach (ClrThread thread in runtime.Threads)
            {
                ulong address = field.GetAddress(thread);
                if (address == 0)
                {
                    Assert.False(field.IsInitialized(thread));
                    foundUninitialized = true;
                }
                else
                {
                    Assert.True(field.IsInitialized(thread));
                    foundInitialized = true;
                }
            }

            Assert.True(foundInitialized, "Expected at least one thread with initialized thread static");
            Assert.True(foundUninitialized, "Expected at least one thread with uninitialized thread static");
        }

        /// <summary>
        /// Verifies that thread-static field values can be read correctly from the thread
        /// that initialized them. Tests both reference (string) and primitive (int) types.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ThreadStaticField_ReadValueFromInitializedThread(bool singleFile)
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("types.dll");
            ClrType type = module.GetTypeByName("Types");
            Assert.NotNull(type);

            ClrThreadStaticField nameField = type.ThreadStaticFields.Single(f => f.Name == "ts_threadName");
            ClrThreadStaticField idField = type.ThreadStaticFields.Single(f => f.Name == "ts_threadId");

            bool foundValue = false;
            foreach (ClrThread thread in runtime.Threads)
            {
                if (!nameField.IsInitialized(thread))
                    continue;

                string value = nameField.ReadString(thread);
                Assert.Equal("MainThread", value);

                int id = idField.Read<int>(thread);
                Assert.NotEqual(0, id);

                foundValue = true;
                break;
            }

            Assert.True(foundValue, "No thread had the thread static field initialized");
        }

        /// <summary>
        /// Regression test for issue #1448: ReadStruct on a primitive-typed static must
        /// return a ClrValueType pointing at the field slot (where the primitive value is
        /// stored inline), not garbage produced by treating the inline value as a boxed
        /// pointer. Also verifies the IClrStaticField interface implementation matches the
        /// concrete ClrStaticField behavior.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StaticField_ReadStruct_OnPrimitive_ReturnsSlotAddress(bool singleFile)
        {
            using DataTarget dt = TestTargets.NestedTypes.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule(TypeTests.NestedTypesModuleName);
            ClrType program = module.GetTypeByName("Program");
            Assert.NotNull(program);

            ClrStaticField field = program.GetStaticFieldByName("s_publicField");
            Assert.NotNull(field);
            Assert.True(field.IsPrimitive);
            Assert.True(field.IsValueType);

            ClrAppDomain domain = runtime.AppDomains.Single();
            ulong slot = field.GetAddress(domain);
            Assert.NotEqual(0ul, slot);

            // ReadStruct on a primitive should point at the slot itself, not at a fake boxed
            // object derived from misinterpreting the inline value as a pointer.
            ClrValueType cvt = field.ReadStruct(domain);
            Assert.Equal(slot, cvt.Address);

            // The explicit IClrStaticField.ReadStruct must match the public ReadStruct.
            Interfaces.IClrStaticField iface = field;
            Interfaces.IClrValue ifaceCvt = iface.ReadStruct(domain);
            Assert.Equal(cvt.Address, ifaceCvt.Address);

            // Sanity: Read<int> reads the same memory the ClrValueType points at.
            int valueRead = field.Read<int>(domain);
            Assert.True(dt.DataReader.Read(cvt.Address, out int valueAtSlot));
            Assert.Equal(valueRead, valueAtSlot);
        }

        /// <summary>
        /// Regression test for issue #1448: the ClrThreadStaticField.ReadStruct primitive
        /// path mirrors ClrStaticField.ReadStruct - returns a ClrValueType pointing at the
        /// inline slot rather than dereferencing the value as a pointer.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ThreadStaticField_ReadStruct_OnPrimitive_ReturnsSlotAddress(bool singleFile)
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("types.dll");
            ClrType type = module.GetTypeByName("Types");
            Assert.NotNull(type);

            ClrThreadStaticField idField = type.ThreadStaticFields.Single(f => f.Name == "ts_threadId");
            Assert.True(idField.IsPrimitive);
            Assert.True(idField.IsValueType);

            bool foundValue = false;
            foreach (ClrThread thread in runtime.Threads)
            {
                if (!idField.IsInitialized(thread))
                    continue;

                ulong slot = idField.GetAddress(thread);
                Assert.NotEqual(0ul, slot);

                ClrValueType cvt = idField.ReadStruct(thread);
                Assert.Equal(slot, cvt.Address);

                int valueRead = idField.Read<int>(thread);
                Assert.True(dt.DataReader.Read(cvt.Address, out int valueAtSlot));
                Assert.Equal(valueRead, valueAtSlot);
                Assert.NotEqual(0, valueRead);

                foundValue = true;
                break;
            }

            Assert.True(foundValue, "No thread had the thread static field initialized");
        }
        /// <summary>
        /// Verifies that <see cref="ClrModule.EnumerateTypesWithStaticFields"/> yields every type
        /// that has at least one static field according to <c>ClrType.StaticFields</c>, and yields
        /// only such types. Cross-checked against the slow path of materializing every typedef
        /// individually so that the metadata pre-filter never drops a real result.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
#nullable enable
        public void EnumerateTypesWithStaticFields_MatchesFullScan(bool singleFile)
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump(singleFile);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("types.dll");

            HashSet<ulong> fast = module.EnumerateTypesWithStaticFields()
                                        .Select(t => t.MethodTable)
                                        .ToHashSet();

            HashSet<ulong> slow = new();
            foreach ((ulong methodTable, _) in module.EnumerateTypeDefToMethodTableMap())
            {
                if (methodTable == 0)
                    continue;

                ClrType? type = runtime.Heap.GetTypeByMethodTable(methodTable);
                if (type is null)
                    continue;

                if (type.StaticFields.Length > 0 || type.ThreadStaticFields.Length > 0)
                    slow.Add(methodTable);
            }

            Assert.NotEmpty(fast);
            Assert.Equal(slow, fast);

            // The Types class has many static fields and must be in the result.
            ClrType? typesClass = module.GetTypeByName("Types");
            Assert.NotNull(typesClass);
            Assert.Contains(typesClass!.MethodTable, fast);

            // ExplicitImpl has no static fields and must NOT be in the result.
            ClrType? explicitImpl = module.GetTypeByName("ExplicitImpl");
            Assert.NotNull(explicitImpl);
            Assert.Empty(explicitImpl!.StaticFields);
            Assert.Empty(explicitImpl.ThreadStaticFields);
            Assert.DoesNotContain(explicitImpl.MethodTable, fast);

            // ThreadStaticsOnly has only thread-static fields. It must STILL be returned by the
            // enumerator because thread statics are GC roots too.
            ClrType? threadStaticsOnly = module.GetTypeByName("ThreadStaticsOnly");
            Assert.NotNull(threadStaticsOnly);
            Assert.Empty(threadStaticsOnly!.StaticFields);
            Assert.NotEmpty(threadStaticsOnly.ThreadStaticFields);
            Assert.Contains(threadStaticsOnly.MethodTable, fast);

            // Every yielded type genuinely has at least one static OR thread-static field.
            foreach (ClrType type in module.EnumerateTypesWithStaticFields())
                Assert.True(type.StaticFields.Length + type.ThreadStaticFields.Length > 0);
        }
#nullable restore
    }
}
