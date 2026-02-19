// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        protected ObjectConnection(TestTarget testTarget, Type type, bool singleFile = false) : this(testTarget, type.Name, singleFile)
        {
        }

        protected ObjectConnection(TestTarget testTarget, string typeName, bool singleFile = false)
        {
            SingleFile = singleFile;
            if (singleFile)
                DataTarget = testTarget.LoadFullDump(singleFile: true);
            else
                DataTarget = testTarget.LoadFullDump();
            Runtime = DataTarget.ClrVersions.Single().CreateRuntime();

            TestDataClrObject = FindFirstInstanceOfType(Runtime.Heap, typeName);
            TestTarget = testTarget;
        }

        /// <summary>
        /// Whether this connection is using a single-file dump.
        /// </summary>
        public bool SingleFile { get; }

        /// <summary>
        /// Test object with various field types.
        /// </summary>
        public ClrObject TestDataClrObject { get; }

        public ClrRuntime Runtime { get; }

        public DataTarget DataTarget { get; }
        public TestTarget TestTarget { get; }

        public T Prototype => new();

        private ClrObject FindFirstInstanceOfType(ClrHeap heap, string typeName)
        {
            ClrObject obj = heap.EnumerateObjects().FirstOrDefault(o => o.Type.Name == typeName);

            if (obj.IsNull)
                throw new InvalidOperationException($"Could not find {typeName} in {TestTarget.Source} source.");

            return obj;
        }

        void IDisposable.Dispose() => DataTarget?.Dispose();
    }
}
