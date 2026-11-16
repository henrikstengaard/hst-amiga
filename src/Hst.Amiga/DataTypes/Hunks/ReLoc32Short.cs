namespace Hst.Amiga.DataTypes.Hunks
{
    using System.Collections.Generic;

    /// <summary>
    /// Reloc32 short hunk with 16-bit hunk numbers and offsets. Also used for drel32 hunks in executables.
    /// </summary>
    public class ReLoc32Short : IReLocHunk
    {
        public ReLoc32Short(uint identifier = HunkIdentifiers.ReLoc32Short)
        {
            Identifier = identifier;
        }

        public uint Identifier { get; }

        /// <summary>
        /// Offsets grouped by the hunk number they point into.
        /// </summary>
        public IEnumerable<ReLoc32Entry> Entries { get; set; }
    }
}
