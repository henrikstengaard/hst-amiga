namespace Hst.Amiga.DataTypes.Hunks
{
    public class Data : IHunk
    {
        public uint Identifier => HunkIdentifiers.Data;
        public byte[] Bytes { get; set; }
    }
}
