// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Tests.Fixtures
{
    /// <summary>
    /// Provides test data from <see cref="Tests.TestTarget"/> source.
    /// <para><see cref="T"/> is object prototype to compare values from snapshot with live instance - must have matching definition in snapshot and during test execution.</para>
    /// </summary>
    public abstract class ObjectConnection<T> : IDisposable
        where T : new()
    {
        protected ObjectConnection(TestTarget testTarget, Type type): this(testTarget, type.Name)
        {
        }

        protected ObjectConnection(TestTarget testTarget, string typeName)
        {
            DataTarget = testTarget.LoadFullDump();
            Runtime = DataTarget.ClrVersions.Single().CreateRuntime();

            TestDataClrObject = FindFirstInstanceOfType(Runtime.Heap, typeName);
            TestTarget = testTarget;
        }

        /// <summary>
        /// Test object with various field types.
        /// </summary>
        public ClrObject TestDataClrObject { get; }

        public ClrRuntime Runtime { get; }

        public DataTarget DataTarget { get; }
        public TestTarget TestTarget { get; }

        public T Prototype => new T();

        private ClrObject FindFirstInstanceOfType(ClrHeap heap, string typeName)
        {
            var type = heap.GetTypeByName(typeName);

            if (type is null)
                throw new InvalidOperationException($"Could not find {typeName} in {TestTarget.Source} source.");

            return new ClrObject(heap.GetObjectsOfType(typeName).First(), type);
        }

        void IDisposable.Dispose() => DataTarget?.Dispose();
    }
}
