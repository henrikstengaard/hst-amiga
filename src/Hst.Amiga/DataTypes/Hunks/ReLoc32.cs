namespace Hst.Amiga.DataTypes.Hunks
{
    using System.Collections.Generic;

    public class ReLoc32 : IHunk
    {
        public uint Identifier => HunkIdentifiers.ReLoc32;
        public IEnumerable<uint> Offsets { get; set; }
    }
}