// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a method on a class.
    /// </summary>
    public abstract class ClrMethod :
#nullable disable // to enable use with both T and T? for reference types due to IEquatable<T> being invariant
        IEquatable<ClrMethod>
#nullable restore
    {
        /// <summary>
        /// Gets the first MethodDesc in EnumerateMethodDescs().  For single
        /// AppDomain programs this is the only MethodDesc.  MethodDescs
        /// are unique to an Method/AppDomain pair, so when there are multiple domains
        /// there will be multiple MethodDescs for a method.
        /// </summary>
        public abstract ulong MethodDesc { get; }

        /// <summary>
        /// Gets the name of the method.  For example, "void System.Foo.Bar(object o, int i)" would return "Bar".
        /// </summary>
        public abstract string? Name { get; }

        /// <summary>
        /// Gets the full signature of the function.  For example, "void System.Foo.Bar(object o, int i)"
        /// would return "System.Foo.Bar(System.Object, System.Int32)"
        /// </summary>
        public abstract string? Signature { get; }

        /// <summary>
        /// Gets the instruction pointer in the target process for the start of the method's assembly.
        /// </summary>
        public abstract ulong NativeCode { get; }

        /// <summary>
        /// Gets the ILOffset of the given address within this method.
        /// </summary>
        /// <param name="addr">The absolute address of the code (not a relative offset).</param>
        /// <returns>The IL offset of the given address.</returns>
        public abstract int GetILOffset(ulong addr);

        /// <summary>
        /// Gets the location in memory of the IL for this method.
        /// </summary>
        public abstract ILInfo? IL { get; }

        /// <summary>
        /// Gets the regions of memory that
        /// </summary>
        public abstract HotColdRegions HotColdInfo { get; }

        /// <summary>
        /// Gets the way this method was compiled.
        /// </summary>
        public abstract MethodCompilationType CompilationType { get; }

        /// <summary>
        /// Gets the IL to native offset mapping.
        /// </summary>
        public abstract ImmutableArray<ILToNativeMap> ILOffsetMap { get; }

        /// <summary>
        /// Gets the metadata token of the current method.
        /// </summary>
        public abstract int MetadataToken { get; }

        /// <summary>
        /// Gets the enclosing type of this method.
        /// </summary>
        public abstract ClrType Type { get; }

        // Visibility:
        /// <summary>
        /// Gets a value indicating whether this method is public.
        /// </summary>
        public abstract bool IsPublic { get; }

        /// <summary>
        /// Gets a value indicating whether this method is private.
        /// </summary>
        public abstract bool IsPrivate { get; }

        /// <summary>
        /// Gets a value indicating whether this method is internal.
        /// </summary>
        public abstract bool IsInternal { get; }

        /// <summary>
        /// Gets a value indicating whether this method is protected.
        /// </summary>
        public abstract bool IsProtected { get; }

        // Attributes:
        /// <summary>
        /// Gets a value indicating whether this method is static.
        /// </summary>
        public abstract bool IsStatic { get; }

        /// <summary>
        /// Gets a value indicating whether this method is final.
        /// </summary>
        public abstract bool IsFinal { get; }

        /// <summary>
        /// Gets a value indicating whether this method is a P/Invoke.
        /// </summary>
        public abstract bool IsPInvoke { get; }

        /// <summary>
        /// Gets a value indicating whether this method is a special method.
        /// </summary>
        public abstract bool IsSpecialName { get; }

        /// <summary>
        /// Gets a value indicating whether this method is a runtime special method.
        /// </summary>
        public abstract bool IsRTSpecialName { get; }

        /// <summary>
        /// Gets a value indicating whether this method is virtual.
        /// </summary>
        public abstract bool IsVirtual { get; }

        /// <summary>
        /// Gets a value indicating whether this method is abstract.
        /// </summary>
        public abstract bool IsAbstract { get; }

        /// <summary>
        /// Gets a value indicating whether this method is an instance constructor.
        /// </summary>
        public virtual bool IsConstructor => Name == ".ctor";

        /// <summary>
        /// Gets a value indicating whether this method is a static constructor.
        /// </summary>
        public virtual bool IsClassConstructor => Name == ".cctor";

        public override bool Equals(object? obj) => Equals(obj as ClrMethod);

        public bool Equals(ClrMethod? other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other is null)
                return false;

            if (MethodDesc == other.MethodDesc)
                return true;

            // MethodDesc shouldn't be 0, but we should check the other way equality mechanism anyway.
            return MethodDesc == 0 && Type == other.Type && MetadataToken == other.MetadataToken;
        }

        public override int GetHashCode() => MethodDesc.GetHashCode();

        public static bool operator ==(ClrMethod? left, ClrMethod? right)
        {
            if (right is null)
                return left is null;

            return right.Equals(left);
        }

        public static bool operator !=(ClrMethod? left, ClrMethod? right) => !(left == right);
    }
}