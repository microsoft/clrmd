namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// An object's thinlock.
    /// </summary>
    public class ClrThinlock
    {
        /// <summary>
        /// The owning thread of this thinlock.
        /// </summary>
        public ClrThread? Thread { get; }

        /// <summary>
        /// The recursion count of the entries for this thinlock.
        /// </summary>
        public int Recursion { get; }

        internal ClrThinlock(ClrThread? thread, int recursion)
        {
            Thread = thread;
            Recursion = recursion;
        }
    }
}
