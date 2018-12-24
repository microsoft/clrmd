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
    public class ClrObjectConnection : ObjectConnection<ClrObjectConnection.PrimitiveTypeCarrier>
    {
        public ClrObjectConnection() : base(TestTargets.ClrObjects, typeof(PrimitiveTypeCarrier))
        {
        }

        public class PrimitiveTypeCarrier
        {
            public bool TrueBool = true;

            public long OneLargerMaxInt = ((long)int.MaxValue + 1);

            public DateTime Birthday = new DateTime(1992, 1, 24);

            public SamplePointerType SamplePointer = new SamplePointerType();

            public EnumType SomeEnum = EnumType.PickedValue;

            public string HelloWorldString = "Hello World";

            public Guid SampleGuid = new Guid("{EB06CEC0-5E2D-4DC4-875B-01ADCC577D13}");
        }


        public class SamplePointerType
        { }


        public enum EnumType { Zero, One, Two, PickedValue }
    }
}
