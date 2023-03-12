// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.Tests.Fixtures
{
    /// <summary>
    /// Provides test data from <see cref="TestTargets.ClrObjects"/> testTarget source.
    /// </summary>
    public class ClrObjectConnection : ObjectConnection<ClrObjectConnection.PrimitiveTypeCarrier>
    {
        public ClrObjectConnection() : base(TestTargets.ClrObjects, typeof(PrimitiveTypeCarrier))
        {
        }

        public class PrimitiveTypeCarrier
        {
            public bool TrueBool = true;

            public long OneLargerMaxInt = (long)int.MaxValue + 1;

            public DateTime Birthday = new(1992, 1, 24);

            public SamplePointerType SamplePointer = new();

            public EnumType SomeEnum = EnumType.PickedValue;

            public string HelloWorldString = "Hello World";

            public Guid SampleGuid = new("{EB06CEC0-5E2D-4DC4-875B-01ADCC577D13}");
        }

        public class SamplePointerType
        { }

        public enum EnumType { Zero, One, Two, PickedValue }
    }
}
