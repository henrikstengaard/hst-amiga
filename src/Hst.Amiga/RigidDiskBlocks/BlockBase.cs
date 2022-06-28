namespace Hst.Amiga.RigidDiskBlocks
{
    public abstract class BlockBase
    {
        public byte[] BlockBytes { get; set; }
        public int Checksum { get; set; }
    }
}