namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Statistics (struct stats in doctor.h).
    /// </summary>
    public class Stats
    {
        public uint BlockNr { get; set; }
        public uint PrevBlkNr { get; set; }
        public int Pass { get; set; }
        public int BlocksChecked { get; set; }
        public int NumErrors { get; set; }
        public int ErrorsFixed { get; set; }
        public int NumFiles { get; set; }
        public int NumDirs { get; set; }
        public int NumSoftLink { get; set; }
        public int NumHardLink { get; set; }
        public int NumRollover { get; set; }
        public int FragmentedFiles { get; set; }
        public int AnodesUsed { get; set; }
    }
}
