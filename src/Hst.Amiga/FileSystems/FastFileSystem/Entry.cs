namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using Blocks;

    public class Entry
    {
        public int Type { get; set; }
        public uint Parent { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public uint Size { get; set; }
        public uint Access { get; set; }
        public DateTime Date { get; set; }
        public uint Real { get; set; }
        public uint Sector { get; set; }
        public IEnumerable<Entry> SubDir { get; set; }
        public EntryBlock EntryBlock { get; set; }
    }
}