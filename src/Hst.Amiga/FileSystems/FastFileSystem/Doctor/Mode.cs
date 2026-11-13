namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    /// <summary>
    /// Mode doctor runs in.
    /// </summary>
    public enum Mode
    {
        /// <summary>
        /// Check volume and report errors. Volume is never written to.
        /// </summary>
        Check,

        /// <summary>
        /// Check volume and repair errors found.
        /// </summary>
        Repair
    }
}
