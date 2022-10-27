namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    /// <summary>
    /// Long name file system comment block (LNFS) present in DOS\6 and DOS\7
    /// </summary>
    public class LongNameFileSystemCommentBlock
    {
        public byte[] BlockBytes { get; set; }
        public int Type { get; set; }
        public uint OwnKey { get; set; }
        public uint HeaderKey { get; set; }
        public int Checksum { get; set; }
        public string Comment { get; set; }

        public LongNameFileSystemCommentBlock()
        {
            Type = Constants.TYPE_COMMENT;
        }
    }
}