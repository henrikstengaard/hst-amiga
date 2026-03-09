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

        /// <summary>
        /// Link entry is the entry that is linked to, null if entry is not a link.
        /// </summary>
        public Entry LinkEntry { get; set; }
        
        /// <summary>
        /// Link path to directory or file, null if entry is not a link.
        /// </summary>
        public string LinkPath { get; set; }
    }
}