// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The type of frame the ClrStackFrame represents.
    /// </summary>
    public enum ClrStackFrameType
    {
        /// <summary>
        /// Indicates this stack frame is unknown
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// Indicates this stack frame is a standard managed method.
        /// </summary>
        ManagedMethod = 0,

        /// <summary>
        /// Indicates this stack frame is a special stack marker that the Clr runtime leaves on the stack.
        /// Note that the ClrStackFrame may still have a ClrMethod associated with the marker.
        /// </summary>
        Runtime = 1
    }

    /// <summary>
    /// A frame in a managed stack trace.  Note you can call ToString on an instance of this object to get the
    /// function name (or clr!Frame name) similar to SOS's !clrstack output.
    /// </summary>
    public abstract class ClrStackFrame
    {
        /// <summary>
        /// Gets this stack frame context.
        /// </summary>
        public abstract byte[] Context { get; }

        /// <summary>
        /// Returns the thread this stack frame came from.
        /// </summary>
        public abstract ClrThread Thread { get; }

        /// <summary>
        /// The instruction pointer of this frame.
        /// </summary>
        public abstract ulong InstructionPointer { get; }

        /// <summary>
        /// The stack pointer of this frame.
        /// </summary>
        public abstract ulong StackPointer { get; }

        /// <summary>
        /// The type of frame (managed or internal).
        /// </summary>
        public abstract ClrStackFrameType Kind { get; }

        /// <summary>
        /// The string to display in a stack trace.  Similar to !clrstack output.
        /// </summary>
        public abstract string DisplayString { get; }

        /// <summary>
        /// Returns the ClrMethod which corresponds to the current stack frame.  This may be null if the
        /// current frame is actually a CLR "Internal Frame" representing a marker on the stack, and that
        /// stack marker does not have a managed method associated with it.
        /// </summary>
        public abstract ClrMethod Method { get; }

        /// <summary>
        /// Returns the module name to use for building the stack trace.
        /// </summary>
        public virtual string ModuleName
        {
            get
            {
                if (Method == null || Method.Type == null || Method.Type.Module == null)
                    return UnknownModuleName;

                string result = Method.Type.Module.Name;
                try
                {
                    return Path.GetFileNameWithoutExtension(result);
                }
                catch
                {
                    return result;
                }
            }
        }

        /// <summary>
        /// The default name used when a module name cannot be calculated.
        /// </summary>
        public static string UnknownModuleName = "UNKNOWN";
    }
}