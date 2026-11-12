namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Constants from doctor.h and pfs3.h (pfsdoctor) not present in Pfs3 Constants.
    /// </summary>
    public static class DoctorConstants
    {
        /// <summary>
        /// last two bytes used for extended file size
        /// </summary>
        public const int DELENTRYFNSIZE = 16;

        public const uint BOOTBLOCK = 0;
        public const uint ROOTBLOCK = 2;
        public const int FNSIZE = 108;
        public const int PATHSIZE = 256;
        public const int FILENAMESIZE = 32;
        public const int DNSIZE = 32;
        public const int CMSIZE = 80;

        /// <summary>
        /// sizeof(struct direntry)
        /// </summary>
        public const int SIZEOF_DIRENTRY = 20;

        /// <summary>
        /// MAX_ENTRYSIZE (sizeof(struct direntry) + FILENAMESIZE + CMSIZE + 32)
        /// </summary>
        public const int MAX_ENTRYSIZE = SIZEOF_DIRENTRY + FILENAMESIZE + CMSIZE + 32;

        /// <summary>
        /// sizeof(rootblock_t)
        /// </summary>
        public const int SIZEOF_ROOTBLOCK = 512;

        /// <summary>
        /// blocknr has to be initialised on a never used blocknr
        /// to prevent it to be considered a loaded cacheline.
        /// </summary>
        public const uint CL_UNUSED = 1;

        /// <summary>
        /// maxima
        /// </summary>
        public const int MAX_PASS = 7;

        /// <summary>
        /// ceiling on the computed anode chain bound, so the multiplication that
        /// derives it cannot overflow on a very large reserved area
        /// </summary>
        public const uint MAX_ANODE_CHAIN = 0x01000000;

        public const int MAXRESBLOCKSIZE = 4096;
        public const int MAXDELDIRSEQNR = 31;

        public const uint MAXSMALLDISK = 5U * 253 * 253 * 32;
        public const uint MAXDISKSIZE1K = 104U * 253 * 253 * 32;
        public const uint MAXDISKSIZE2K = 104U * 509 * 509 * 32;
        public const uint MAXDISKSIZE4K = 104U * 1021 * 1021 * 32;

        public const uint MODE_STORED_GEOM = 4096;
        public const uint MODE_MASK = 8192 - 1;

        /// <summary>
        /// seperator used for deldirentries
        /// </summary>
        public const char DELENTRY_SEP = '@';
    }
}
