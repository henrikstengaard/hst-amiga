namespace Hst.Amiga.DataTypes.Hunks
{
    using System.Collections.Generic;

    public class ReLoc32 : IHunk
    {
        public uint Identifier => HunkIdentifiers.ReLoc32;

        /// <summary>
        /// Hunk numbers and offsets as read, each hunk number followed by its offsets.
        /// </summary>
        public IEnumerable<uint> Offsets { get; set; }

        /// <summary>
        /// Offsets grouped by the hunk number they point into.
        /// </summary>
        public IEnumerable<ReLoc32Entry> Entries { get; set; }
    }
}
