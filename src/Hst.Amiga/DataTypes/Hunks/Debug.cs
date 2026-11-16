namespace Hst.Amiga.DataTypes.Hunks
{
    /// <summary>
    /// Debug hunk with debug information in compiler specific format.
    /// </summary>
    public class Debug : IHunk
    {
        public uint Identifier => HunkIdentifiers.Debug;

        public byte[] Data { get; set; }
    }
}
