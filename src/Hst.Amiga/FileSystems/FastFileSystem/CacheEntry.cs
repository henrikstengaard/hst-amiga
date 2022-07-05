namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;

    public class CacheEntry
    {
        public int Header;
        public int Size;
        public int Protect;
        public DateTime Date; 
        public int Type;
        public string Name;
        public string Comment;

        public int EntryLen
        {
            get
            {
                var len = 24 + (Name ?? string.Empty).Length + 1 + (Comment ?? string.Empty).Length;
                return len % 2 == 0 ? len : len + 1;
            }
        }
    }
}