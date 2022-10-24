namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using Blocks;

    public class NameToEntryBlockResult
    {
        public uint NSect { get; set; }
        public EntryBlock EntryBlock { get; set; }
        public uint? NUpdSect { get; set; }
    }
}