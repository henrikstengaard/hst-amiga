namespace Hst.Amiga.FileSystems.Pfs3
{
    public class anode
    {
        public uint clustersize;
        public uint blocknr;
        public uint next;

        public const int Size = Amiga.SizeOf.ULong * 3;
    }
}