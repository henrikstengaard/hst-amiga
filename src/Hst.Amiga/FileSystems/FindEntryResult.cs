namespace Hst.Amiga.FileSystems
{
    using System.Collections.Generic;

    public class FindEntryResult
    {
        public Entry Entry { get; set; }
        public IEnumerable<string> PartsNotFound { get; set; }
    }
}