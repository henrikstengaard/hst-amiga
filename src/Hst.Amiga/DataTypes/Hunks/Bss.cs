namespace Hst.Amiga.DataTypes.Hunks
{
    public class Bss : IHunk
    {
        public uint Identifier => HunkIdentifiers.Bss;

        /// <summary>
        /// Size of zero initialized memory in bytes.
        /// </summary>
        public uint Size { get; set; }
    }
}
