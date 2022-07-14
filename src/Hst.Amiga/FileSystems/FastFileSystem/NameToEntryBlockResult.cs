namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using Blocks;

    public class NameToEntryBlockResult
    {
        public int NSect { get; set; }
        public EntryBlock EntryBlock { get; set; }
        public int? NUpdSect { get; set; }
    }
}