namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    /// <summary>
    /// generic interface for blocks
    /// </summary>
    public interface IBlock
    {
        byte[] BlockBytes { get; set; }
        ushort id { get; set; }
        ushort not_used_1 { get; set; }
        uint datestamp { get; set; }
    }
}