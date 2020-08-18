// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
    }
}
