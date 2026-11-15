namespace Hst.Amiga.FileSystems.Pfs3
{
    internal class anode
    {
        public uint clustersize;
        public uint blocknr;
        public uint next;

        /// <summary>
        /// pfsdoctor only
        /// </summary>
        public uint nr;

        public const int Size = Amiga.SizeOf.ULong * 3;
    }
}