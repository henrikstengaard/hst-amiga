namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System;

    /// <summary>
    /// Options for StandardScan() (SSF_ flags in doctor.h).
    /// </summary>
    [Flags]
    public enum StandardScanFlags : uint
    {
        None = 0,

        /// <summary>
        /// check consistency? (SSF_CHECK)
        /// </summary>
        Check = 1,

        /// <summary>
        /// fix errors? (SSF_FIX)
        /// </summary>
        Fix = 2,

        /// <summary>
        /// analyse volume, count things etc ? (SSF_ANALYSE)
        /// </summary>
        Analyse = 4,

        /// <summary>
        /// undo fast format (SSF_UNFORMAT)
        /// </summary>
        Unformat = 8,

        /// <summary>
        /// SSF_GEN_RESBITMAP
        /// </summary>
        GenResBitmap = 16,

        /// <summary>
        /// SSF_GEN_ANODEBITMAP
        /// </summary>
        GenAnodeBitmap = 32,

        /// <summary>
        /// SSF_GEN_MAINBITMAP
        /// </summary>
        GenMainBitmap = 64,

        /// <summary>
        /// SSF_GEN_BMMASK
        /// </summary>
        GenBmMask = GenResBitmap | GenAnodeBitmap | GenMainBitmap,

        /// <summary>
        /// SSF_VERBOSE
        /// </summary>
        Verbose = 128,

        /// <summary>
        /// show status information (SSF_INFO)
        /// </summary>
        Info = 256
    }
}
