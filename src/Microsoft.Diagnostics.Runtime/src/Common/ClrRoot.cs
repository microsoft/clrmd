// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a root in the target process.  A root is the base entry to the GC's mark and sweep algorithm.
    /// </summary>
    public abstract class ClrRoot
    {
        /// <summary>
        /// A GC Root also has a Kind, which says if it is a strong or weak root
        /// </summary>
        public abstract GCRootKind Kind { get; }

        /// <summary>
        /// The name of the root.
        /// </summary>
        public virtual string Name => "";

        /// <summary>
        /// The type of the object this root points to.  That is, ClrHeap.GetObjectType(ClrRoot.Object).
        /// </summary>
        public abstract ClrType Type { get; }

        /// <summary>
        /// The object on the GC heap that this root keeps alive.
        /// </summary>
        public virtual ulong Object { get; protected set; }

        /// <summary>
        /// The address of the root in the target process.
        /// </summary>
        public virtual ulong Address { get; protected set; }

        /// <summary>
        /// If the root can be identified as belonging to a particular AppDomain this is that AppDomain.
        /// It an be null if there is no AppDomain associated with the root.
        /// </summary>
        public virtual ClrAppDomain AppDomain => null;

        /// <summary>
        /// If the root has a thread associated with it, this will return that thread.
        /// </summary>
        public virtual ClrThread Thread => null;

        /// <summary>
        /// Returns true if Object is an "interior" pointer.  This means that the pointer may actually
        /// point inside an object instead of to the start of the object.
        /// </summary>
        public virtual bool IsInterior => false;

        /// <summary>
        /// Returns true if the root "pins" the object, preventing the GC from relocating it.
        /// </summary>
        public virtual bool IsPinned => false;

        /// <summary>
        /// Unfortunately some versions of the APIs we consume do not give us perfect information.  If
        /// this property is true it means we used a heuristic to find the value, and it might not
        /// actually be considered a root by the GC.
        /// </summary>
        public virtual bool IsPossibleFalsePositive => false;

        /// <summary>
        /// Returns the stack frame associated with this stack root.
        /// </summary>
        public virtual ClrStackFrame StackFrame => null;

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return $"GCRoot {Address:X8}->{Object:X8} {Name}";
        }
    }
}