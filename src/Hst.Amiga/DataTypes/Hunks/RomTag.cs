namespace Hst.Amiga.DataTypes.Hunks
{
    /// <summary>
    /// Resident structure (romtag), see exec/resident.h.
    /// </summary>
    public class RomTag
    {
        /// <summary>
        /// Rom tag match word, RTC_MATCHWORD.
        /// </summary>
        public const ushort MatchWord = 0x4AFC;

        /// <summary>
        /// Number of hunk containing the resident structure.
        /// </summary>
        public int HunkNumber { get; set; }

        /// <summary>
        /// Offset of resident structure in hunk.
        /// </summary>
        public uint Offset { get; set; }

        public byte Flags { get; set; }
        public byte Version { get; set; }
        public byte Type { get; set; }
        public sbyte Priority { get; set; }

        /// <summary>
        /// Name (rt_Name) or null, if not resolvable.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Id string (rt_IdString) with trailing line breaks removed or null, if not resolvable.
        /// </summary>
        public string IdString { get; set; }
    }
}
