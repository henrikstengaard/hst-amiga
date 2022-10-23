namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.Collections.Generic;
    using Blocks;

    public class FindEntryResult
    {
        public string Name { get; set; }
        public EntryBlock EntryBlock { get; set; }
        public IEnumerable<string> PartsNotFound { get; set; }
    }
}