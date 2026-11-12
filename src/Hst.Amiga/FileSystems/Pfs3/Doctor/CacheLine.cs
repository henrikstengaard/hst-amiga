namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System.Collections.Generic;

    /// <summary>
    /// Cache line (struct cacheline in doctor.h).
    /// </summary>
    public class CacheLine
    {
        /// <summary>
        /// Linked list node replacing next and prev pointers. A cache line is either in LRU queue or LRU pool.
        /// </summary>
        public LinkedListNode<CacheLine> Node { get; }

        /// <summary>
        /// 1 == unused
        /// </summary>
        public uint BlockNr { get; set; }

        public bool Dirty { get; set; }
        public byte[] Data { get; set; }

        public CacheLine()
        {
            Node = new LinkedListNode<CacheLine>(this);
        }
    }
}
