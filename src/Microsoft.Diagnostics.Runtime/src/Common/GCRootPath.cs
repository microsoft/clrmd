namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a path of objects from a root to an object.
    /// </summary>
    public struct GCRootPath
    {
        /// <summary>
        /// The location that roots the object.
        /// </summary>
        public ClrRoot Root { get; set; }

        /// <summary>
        /// The path from Root to a given target object.
        /// </summary>
        public ClrObject[] Path { get; set; }
    }
}