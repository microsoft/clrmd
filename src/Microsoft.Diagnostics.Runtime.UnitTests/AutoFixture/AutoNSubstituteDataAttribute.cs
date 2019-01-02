// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;

namespace Microsoft.Diagnostics.Runtime.UnitTests
{
    public class AutoNSubstituteDataAttribute : AutoDataAttribute
    {
        public AutoNSubstituteDataAttribute() :
          base(() => new Fixture().Customize(
              new CompositeCustomization(
                  new ClrMDEntitiesCustomization(),
                  new AutoNSubstituteCustomization())))
        {
        }

    }
}
