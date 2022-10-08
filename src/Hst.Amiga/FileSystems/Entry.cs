namespace Hst.Amiga.FileSystems
{
    using System;

    public class Entry
    {
        public string Name { get; set; }
        public EntryType Type { get; set; }
        public long Size { get; set; }
        public DateTime CreationDate { get; set; }
        public ProtectionBits ProtectionBits { get; set; }
    }
}