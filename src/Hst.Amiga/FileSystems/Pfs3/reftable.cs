namespace Hst.Amiga.FileSystems.Pfs3
{
    internal class reftable
    {
        public uint blocknr;          /* blocknr of cached block; 0 = empty slot */
        public bool dirty;            /* dirty flag (TRUE/FALSE) */
        public byte pad;
    };
}