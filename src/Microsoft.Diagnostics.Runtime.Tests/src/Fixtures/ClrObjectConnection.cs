// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Tests.Fixtures
{
    /// <summary>
    /// Provides test data from <see cref="TestTargets.ClrObjects"/> testTarget source.
    /// </summary>
    public class ClrObjectConnection : IDisposable
    {
        public ClrObjectConnection()
        {
            DataTarget = TestTargets.ClrObjects.LoadFullDump();
            Runtime = DataTarget.ClrVersions.Single().CreateRuntime();

            PrimitiveTypeCarrier = FindFirstInstanceOfType(Runtime.Heap, "PrimitiveTypeCarrier");
        }

        /// <summary>
        /// Test object with various field types.
        /// </summary>
        public ClrObject PrimitiveTypeCarrier { get; }

        public ClrRuntime Runtime { get; }

        public DataTarget DataTarget { get; }

        private static ClrObject FindFirstInstanceOfType(ClrHeap heap, string typeName)
        {
            var type = heap.GetTypeByName(typeName);

            if (type is null) throw new InvalidOperationException($"Could not find {typeName} in {nameof(TestTargets.ClrObjects)} source.");

            return new ClrObject(heap.GetObjectsOfType(typeName).First(), type);
        }

        void IDisposable.Dispose() => DataTarget?.Dispose();
    }
}
