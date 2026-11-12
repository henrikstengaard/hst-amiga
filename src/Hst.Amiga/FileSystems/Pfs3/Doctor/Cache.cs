namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System.Collections.Generic;

    /// <summary>
    /// Device cache (struct cache in doctor.h).
    /// </summary>
    public class Cache
    {
        public LinkedList<CacheLine> LruQueue { get; set; }
        public LinkedList<CacheLine> LruPool { get; set; }

        /// <summary>
        /// linesize in blocks
        /// </summary>
        public uint LineSize { get; set; }

        public uint NoLines { get; set; }
        public uint BlockSize { get; set; }
        public CacheLine[] CacheLines { get; set; }

        public Cache()
        {
            LruQueue = new LinkedList<CacheLine>();
            LruPool = new LinkedList<CacheLine>();
        }
    }
}
