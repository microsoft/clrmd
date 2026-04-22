// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Which managed runtime flavor inside a crash dump a test targets.
    /// </summary>
    public enum DumpFlavor
    {
        /// <summary>.NET (modern) runtime hosted via hostfxr/coreclr.</summary>
        Core,

        /// <summary>Desktop .NET Framework 4.x runtime hosted via mscoree.</summary>
        Framework,
    }

    /// <summary>
    /// Set of flavors a <see cref="TestTarget"/> can be built and dumped for.
    /// </summary>
    [Flags]
    public enum TargetFlavors
    {
        None = 0,
        Core = 1,
        Framework = 2,
        Any = Core | Framework,
    }

    /// <summary>
    /// A single point in the dump-variant matrix that a test exercises.
    /// A variant couples the runtime flavor inside the dump with whether the
    /// dump was captured under the HighBitHost harness (Windows x86 only).
    /// </summary>
    public readonly record struct DumpVariant(DumpFlavor Flavor, bool HighBit)
    {
        public override string ToString() => (Flavor, HighBit) switch
        {
            (DumpFlavor.Core, false) => "core",
            (DumpFlavor.Core, true) => "core-highbit",
            (DumpFlavor.Framework, false) => "framework",
            (DumpFlavor.Framework, true) => "framework-highbit",
            _ => $"{Flavor}{(HighBit ? "-highbit" : string.Empty)}",
        };
    }

    /// <summary>
    /// Produces the set of <see cref="DumpVariant"/> values a given <see cref="TestTarget"/>
    /// can be exercised against in the current host environment. Used as xUnit
    /// <c>[Theory]</c> member data so that unsupported combinations are filtered at
    /// enumeration time rather than surfacing as <c>Skip=</c> entries.
    /// </summary>
    public static class TestVariants
    {
        /// <summary>
        /// All variants the <paramref name="target"/> supports in the current environment.
        /// </summary>
        public static IEnumerable<object[]> For(TestTarget target) =>
            Enumerate(target).Select(v => new object[] { v });

        /// <summary>
        /// Only the non-HighBit variants (plain Core and Framework where applicable).
        /// Use for tests that legitimately don't need HighBit coverage.
        /// </summary>
        public static IEnumerable<object[]> NoHighBit(TestTarget target) =>
            Enumerate(target).Where(v => !v.HighBit).Select(v => new object[] { v });

        /// <summary>
        /// Only the Core variants (plain and, when supported, HighBit).
        /// </summary>
        public static IEnumerable<object[]> CoreOnly(TestTarget target) =>
            Enumerate(target).Where(v => v.Flavor == DumpFlavor.Core).Select(v => new object[] { v });

        /// <summary>
        /// Variants crossed with a single-file dimension. Single-file is a Core-only,
        /// non-HighBit concept, so the resulting sequence yields <c>(variant, false)</c>
        /// for every variant plus <c>(variant, true)</c> only for plain Core.
        /// </summary>
        public static IEnumerable<object[]> WithSingleFile(TestTarget target)
        {
            foreach (DumpVariant v in Enumerate(target))
            {
                yield return new object[] { v, false };
                if (v.Flavor == DumpFlavor.Core && !v.HighBit)
                    yield return new object[] { v, true };
            }
        }

        /// <summary>
        /// Only the HighBit variants. Empty on non-Windows or non-x86 hosts.
        /// </summary>
        public static IEnumerable<object[]> HighBitOnly(TestTarget target) =>
            Enumerate(target).Where(v => v.HighBit).Select(v => new object[] { v });

        private static IEnumerable<DumpVariant> Enumerate(TestTarget target)
        {
            if (target is null)
                throw new ArgumentNullException(nameof(target));

            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isX86 = RuntimeInformation.ProcessArchitecture == Architecture.X86;

            foreach (DumpFlavor flavor in new[] { DumpFlavor.Core, DumpFlavor.Framework })
            {
                if (!target.Supports(flavor))
                    continue;

                // Framework dumps are only produced on Windows (requires DbgEng and the CLR).
                if (flavor == DumpFlavor.Framework && !isWindows)
                    continue;

                yield return new DumpVariant(flavor, HighBit: false);

                // HighBit is a 32-bit Windows concept.
                if (isWindows && isX86 && target.Supports(flavor, highBit: true))
                    yield return new DumpVariant(flavor, HighBit: true);
            }
        }
    }
}
