namespace Hst.Amiga.Roms
{
    /// <summary>
    /// A resident module located in a Kickstart ROM image.
    /// </summary>
    public class KickstartRomModule
    {
        /// <summary>
        /// Offset of the module in the decoded ROM image.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Size of the module in bytes.
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Resident module version.
        /// </summary>
        public byte Version { get; set; }

        /// <summary>
        /// Resident module revision.
        /// </summary>
        public byte Revision { get; set; }

        /// <summary>
        /// Resident module name, if present.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Resident module identification string, if present.
        /// </summary>
        public string IdString { get; set; }
    }
}
