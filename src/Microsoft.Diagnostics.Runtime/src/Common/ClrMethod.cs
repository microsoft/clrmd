// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a method on a class.
    /// </summary>
    public abstract class ClrMethod
    {
        /// <summary>
        /// Retrieves the first MethodDesc in EnumerateMethodDescs().  For single
        /// AppDomain programs this is the only MethodDesc.  MethodDescs
        /// are unique to an Method/AppDomain pair, so when there are multiple domains
        /// there will be multiple MethodDescs for a method.
        /// </summary>
        public abstract ulong MethodDesc { get; }

        /// <summary>
        /// Enumerates all method descs for this method in the process.  MethodDescs
        /// are unique to an Method/AppDomain pair, so when there are multiple domains
        /// there will be multiple MethodDescs for a method.
        /// </summary>
        /// <returns>
        /// An enumeration of method handles in the process for this given
        /// method.
        /// </returns>
        public abstract IEnumerable<ulong> EnumerateMethodDescs();

        /// <summary>
        /// The name of the method.  For example, "void System.Foo.Bar(object o, int i)" would return "Bar".
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Returns the full signature of the function.  For example, "void System.Foo.Bar(object o, int i)"
        /// would return "System.Foo.Bar(System.Object, System.Int32)"
        /// </summary>
        public abstract string GetFullSignature();

        /// <summary>
        /// Returns the instruction pointer in the target process for the start of the method's assembly.
        /// </summary>
        public abstract ulong NativeCode { get; }

        /// <summary>
        /// Gets the ILOffset of the given address within this method.
        /// </summary>
        /// <param name="addr">The absolute address of the code (not a relative offset).</param>
        /// <returns>The IL offset of the given address.</returns>
        public abstract int GetILOffset(ulong addr);

        /// <summary>
        /// Returns the location in memory of the IL for this method.
        /// </summary>
        public abstract ILInfo IL { get; }

        /// <summary>
        /// Returns the regions of memory that
        /// </summary>
        public abstract HotColdRegions HotColdInfo { get; }

        /// <summary>
        /// Returns the way this method was compiled.
        /// </summary>
        public abstract MethodCompilationType CompilationType { get; }

        /// <summary>
        /// Returns the IL to native offset mapping.
        /// </summary>
        public abstract ILToNativeMap[] ILOffsetMap { get; }

        /// <summary>
        /// Returns the metadata token of the current method.
        /// </summary>
        public abstract uint MetadataToken { get; }

        /// <summary>
        /// Returns the enclosing type of this method.
        /// </summary>
        public abstract ClrType Type { get; }

        // Visibility:
        /// <summary>
        /// Returns if this method is public.
        /// </summary>
        public abstract bool IsPublic { get; }

        /// <summary>
        /// Returns if this method is private.
        /// </summary>
        public abstract bool IsPrivate { get; }

        /// <summary>
        /// Returns if this method is internal.
        /// </summary>
        public abstract bool IsInternal { get; }

        /// <summary>
        /// Returns if this method is protected.
        /// </summary>
        public abstract bool IsProtected { get; }

        // Attributes:
        /// <summary>
        /// Returns if this method is static.
        /// </summary>
        public abstract bool IsStatic { get; }
        /// <summary>
        /// Returns if this method is final.
        /// </summary>
        public abstract bool IsFinal { get; }
        /// <summary>
        /// Returns if this method is a PInvoke.
        /// </summary>
        public abstract bool IsPInvoke { get; }
        /// <summary>
        /// Returns if this method is a special method.
        /// </summary>
        public abstract bool IsSpecialName { get; }
        /// <summary>
        /// Returns if this method is runtime special method.
        /// </summary>
        public abstract bool IsRTSpecialName { get; }

        /// <summary>
        /// Returns if this method is virtual.
        /// </summary>
        public abstract bool IsVirtual { get; }
        /// <summary>
        /// Returns if this method is abstract.
        /// </summary>
        public abstract bool IsAbstract { get; }

        /// <summary>
        /// Returns the location of the GCInfo for this method.
        /// </summary>
        public abstract ulong GCInfo { get; }

        /// <summary>
        /// Returns whether this method is an instance constructor.
        /// </summary>
        public virtual bool IsConstructor => Name == ".ctor";

        /// <summary>
        /// Returns whether this method is a static constructor.
        /// </summary>
        public virtual bool IsClassConstructor => Name == ".cctor";
    }
}