namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public interface IDataBlock
    {
        byte[] BlockBytes { get; set; }
        byte[] Data { get; set; }
    }
}