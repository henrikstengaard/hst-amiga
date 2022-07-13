namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public interface IBlock
    {
        uint Offset { get; set; }
        byte[] BlockBytes { get; set; }
    }
}