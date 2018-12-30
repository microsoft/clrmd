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
                type.IsValueClass.Returns(true);

                var constructor = typeof(ClrValueClass)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, binder: null, types: new Type[] { typeof(ulong), typeof(ClrType), typeof(bool) }, null);
                return (ClrValueClass)constructor.Invoke(new object[] { address, type, true });
            });
        }
    }
}
