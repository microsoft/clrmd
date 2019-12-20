// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using AutoFixture;
using NSubstitute;

namespace Microsoft.Diagnostics.Runtime.UnitTests
{
    public class ClrMDEntitiesCustomization : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Register((ClrHeap heap, string typeName) =>
            {
                var clrType = Substitute.For<ClrType>();
                clrType.Heap.Returns(heap);
                clrType.Name.Returns(typeName);

                return clrType;
            });

            fixture.Register((ClrType type, ulong address) =>
            {
                var heap = type.Heap;
                heap.GetObjectType(address).Returns(type);

                return new ClrObject(address, type);
            });

            fixture.Register((ClrType owner, string fieldName) =>
            {
                ClrInstanceField clrInstanceField = Substitute.For<ClrInstanceField>();
                clrInstanceField.Name.Returns(fieldName);

                owner.GetFieldByName(fieldName).Returns(clrInstanceField);

                return clrInstanceField;
            });

            fixture.Register((ClrType type, ulong address) =>
            {
                type.IsValueType.Returns(true);

                var constructor = typeof(ClrValueType)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, binder: null, types: new Type[] { typeof(ulong), typeof(ClrType), typeof(bool) }, null);
                return (ClrValueType)constructor.Invoke(new object[] { address, type, true });
            });
        }
    }
}
