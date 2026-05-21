// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

#pragma warning disable 0414

// Value-type whose box (the backing storage for its static field) we want to
// observe in a collectible AssemblyLoadContext.
public struct CollectibleUnmanagedStruct
{
    public static CollectibleUnmanagedStruct Instance = default;
}

// Same shape, but loaded in the default (uncollectible) ALC for comparison.
public struct UncollectibleUnmanagedStruct
{
    public static UncollectibleUnmanagedStruct Instance = default;
}

internal sealed class CollectibleAssemblyLoadContext : AssemblyLoadContext
{
    public CollectibleAssemblyLoadContext() : base(isCollectible: true) { }
}

internal static class CollectibleTypesTarget
{
    // Static fields so the GC can't reclaim the ALC, its assembly, or the
    // boxed statics before the dump is captured.
    private static CollectibleAssemblyLoadContext s_alc;
    private static Assembly s_alcAssembly;

    public static void Main()
    {
        s_alc = new CollectibleAssemblyLoadContext();
        s_alcAssembly = s_alc.LoadFromAssemblyPath(typeof(CollectibleTypesTarget).Assembly.Location);

        Type collectibleInAlc = s_alcAssembly.GetType(typeof(CollectibleUnmanagedStruct).FullName)
            ?? throw new InvalidOperationException("CollectibleUnmanagedStruct not found in collectible ALC.");
        RuntimeHelpers.RunClassConstructor(collectibleInAlc.TypeHandle);

        // Also run the default-ALC cctor so the uncollectible counterpart is on the heap.
        RuntimeHelpers.RunClassConstructor(typeof(UncollectibleUnmanagedStruct).TypeHandle);

        GC.KeepAlive(s_alc);
        GC.KeepAlive(s_alcAssembly);

        // Unhandled exception triggers the test-harness dump generator.
        throw new Exception("CollectibleTypes dump trigger");
    }
}
