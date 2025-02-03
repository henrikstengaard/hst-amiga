namespace Hst.Amiga.DataTypes.Hunks
{
    public class Code : IHunk
    {
        public uint Identifier => HunkIdentifiers.Code;
        public byte[] Data { get; set; }
    }
}