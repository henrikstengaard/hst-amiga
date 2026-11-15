namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    internal interface IBlock
    {
        uint Offset { get; set; }
        byte[] BlockBytes { get; set; }
    }
}