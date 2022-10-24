namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.Collections.Generic;

    public class FindEntryResult
    {
        public string Name { get; set; }
        public uint Sector { get; set; }
        public IEnumerable<string> PartsNotFound { get; set; }
    }
}