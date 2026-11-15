namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;

    internal class diskcache
    {
        public reftable[] ref_;   /* reference table; one entry per slot */
        public byte[] data;            /* the data (one slot per block) */
        public ushort size;             /* cache capacity in blocks (order of 2) */
        public ushort mask;             /* size expressed in a bitmask */
        public ushort roving;           /* round robin roving pointer */

        public diskcache()
        {
            ref_ = Array.Empty<reftable>();
        }
    }
}