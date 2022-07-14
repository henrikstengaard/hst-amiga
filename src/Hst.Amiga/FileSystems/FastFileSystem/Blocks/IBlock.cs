namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public interface IBlock
    {
        uint Offset { get; set; }
        byte[] BlockBytes { get; set; }
    }
}