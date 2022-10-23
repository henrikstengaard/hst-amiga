namespace Hst.Amiga.FileSystems
{
    using System;

    public class Entry
    {
        public string Name { get; set; }
        public EntryType Type { get; set; }
        public long Size { get; set; }
        public DateTime Date { get; set; }
        public ProtectionBits ProtectionBits { get; set; }
        public string Comment { get; set; }
    }
}