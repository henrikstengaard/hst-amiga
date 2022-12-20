namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public class BootBlock
    {
        public byte[] DosType { get; set; }
        public uint RootBlockOffset { get; set; }

        public BootBlock()
        {
            RootBlockOffset = 880; // floppy disk root block offset
        }
    }
}