namespace Hst.Amiga.FileSystems.Pfs3
{
    public class anode
    {
        public uint clustersize;
        public uint blocknr;
        public uint next;

        // pfsdoctor only
        public uint nr;

        public const int Size = Amiga.SizeOf.ULong * 3;
    }
}