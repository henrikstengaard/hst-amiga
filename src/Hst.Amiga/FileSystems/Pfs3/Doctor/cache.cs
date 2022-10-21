namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System.Collections.Generic;

    public class cache
    {
        public LinkedList<cacheline> LRUqueue;
        public LinkedList<cacheline> LRUpool;
        public uint linesize; // linesize in blocks
        public uint nolines;
        public cacheline[] cachelines { get; set; }
    }
}