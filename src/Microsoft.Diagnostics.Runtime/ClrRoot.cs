// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// This sets the policy for how ClrHeap walks the stack when enumerating roots.  There is a choice here because the 'Exact' stack walking
    /// gives the correct answer (without overreporting), but unfortunately is poorly implemented in CLR's debugging layer.
    /// This means it could take 10-30 minutes (!) to enumerate roots on crash dumps with 4000+ threads.
    /// </summary>
    public enum ClrRootStackwalkPolicy
    {
        /// <summary>
        /// The GCRoot class will attempt to select a policy for you based on the number of threads in the process.
        /// </summary>
        Automatic,

        /// <summary>
        /// Use real stack roots.  This is much slower than 'Fast', but provides no false-positives for more accurate
        /// results.  Note that this can actually take 10s of minutes to enumerate the roots themselves in the worst
        /// case scenario.
        /// </summary>
        Exact,

        /// <summary>
        /// Walks each pointer alighed address on all stacks and if it points to an object it treats that location
        /// as a real root.  This can over-report roots when a value is left on the stack, but the GC does not
        /// consider it a real root.
        /// </summary>
        Fast,

        /// <summary>
        /// Do not walk stack roots.
        /// </summary>
        SkipStack,
    }

    /// <summary>
    /// The type of GCRoot that a ClrRoot represnts.
    /// </summary>
    public enum GCRootKind
    {
        /// <summary>
        /// The root is a static variable.
        /// </summary>
        StaticVar,

        /// <summary>
        /// The root is a thread static.
        /// </summary>
        ThreadStaticVar,

        /// <summary>
        /// The root is a local variable (or compiler generated temporary variable).
        /// </summary>
        LocalVar,

        /// <summary>
        /// The root is a strong handle.
        /// </summary>
        Strong,

        /// <summary>
        /// The root is a weak handle.
        /// </summary>
        Weak,

        /// <summary>
        /// The root is a strong pinning handle.
        /// </summary>
        Pinning,

        /// <summary>
        /// The root comes from the finalizer queue.
        /// </summary>
        Finalizer,

        /// <summary>
        /// The root is an async IO (strong) pinning handle.
        /// </summary>
        AsyncPinning,

        /// <summary>
        /// The max value of this enum.
        /// </summary>
        Max = AsyncPinning
    }

    /// <summary>
    /// Represents a root in the target process.  A root is the base entry to the GC's mark and sweep algorithm.
    /// </summary>
    public abstract class ClrRoot
    {
        /// <summary>
        /// A GC Root also has a Kind, which says if it is a strong or weak root
        /// </summary>
        abstract public GCRootKind Kind { get; }

        /// <summary>
        /// The name of the root. 
        /// </summary>
        virtual public string Name { get { return ""; } }

        /// <summary>
        /// The type of the object this root points to.  That is, ClrHeap.GetObjectType(ClrRoot.Object).
        /// </summary>
        abstract public ClrType Type { get; }

        /// <summary>
        /// The object on the GC heap that this root keeps alive.
        /// </summary>
        virtual public ulong Object { get; protected set; }

        /// <summary>
        /// The address of the root in the target process.
        /// </summary>
        virtual public ulong Address { get; protected set; }

        /// <summary>
        /// If the root can be identified as belonging to a particular AppDomain this is that AppDomain.
        /// It an be null if there is no AppDomain associated with the root.  
        /// </summary>
        virtual public ClrAppDomain AppDomain { get { return null; } }

        /// <summary>
        /// If the root has a thread associated with it, this will return that thread.
        /// </summary>
        virtual public ClrThread Thread { get { return null; } }

        /// <summary>
        /// Returns true if Object is an "interior" pointer.  This means that the pointer may actually
        /// point inside an object instead of to the start of the object.
        /// </summary>
        virtual public bool IsInterior { get { return false; } }

        /// <summary>
        /// Returns true if the root "pins" the object, preventing the GC from relocating it.
        /// </summary>
        virtual public bool IsPinned { get { return false; } }

        /// <summary>
        /// Unfortunately some versions of the APIs we consume do not give us perfect information.  If
        /// this property is true it means we used a heuristic to find the value, and it might not
        /// actually be considered a root by the GC.
        /// </summary>
        virtual public bool IsPossibleFalsePositive { get { return false; } }


        /// <summary>
        /// Returns the stack frame associated with this stack root.
        /// </summary>
        virtual public ClrStackFrame StackFrame { get { return null; } }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return string.Format("GCRoot {0:X8}->{1:X8} {2}", Address, Object, Name);
        }
    }

}
