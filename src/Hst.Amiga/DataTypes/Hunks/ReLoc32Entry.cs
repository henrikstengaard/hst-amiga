namespace Hst.Amiga.DataTypes.Hunks
{
    using System.Collections.Generic;

    /// <summary>
    /// Reloc32 offsets pointing into a hunk.
    /// </summary>
    public class ReLoc32Entry
    {
        /// <summary>
        /// Number of the hunk the offsets point into.
        /// </summary>
        public uint HunkNumber { get; set; }

        /// <summary>
        /// Offsets in the hunk being relocated, where long words are relocated by the start address of hunk number.
        /// </summary>
        public IEnumerable<uint> Offsets { get; set; }
    }
}
