namespace Hst.Amiga.DataTypes.Hunks
{
    public static class HunkIdentifiers
    {
        /// <summary>
        /// Hunk name
        /// </summary>
        public const uint Name = 0x3E8;

        /// <summary>
        /// Hunk header
        /// </summary>
        public const uint Header = 0x3F3;

        /// <summary>
        /// Hunk code
        /// </summary>
        public const uint Code = 0x3E9;

        /// <summary>
        /// Hunk data
        /// </summary>
        public const uint Data = 0x3EA;

        /// <summary>
        /// Hunk bss
        /// </summary>
        public const uint Bss = 0x3EB;

        /// <summary>
        /// Hunk reloc32
        /// </summary>
        public const uint ReLoc32 = 0x3EC;

        /// <summary>
        /// Hunk symbol
        /// </summary>
        public const uint Symbol = 0x3F0;

        /// <summary>
        /// Hunk debug
        /// </summary>
        public const uint Debug = 0x3F1;

        /// <summary>
        /// Hunk end
        /// </summary>
        public const uint End = 0x3F2;

        /// <summary>
        /// Hunk overlay
        /// </summary>
        public const uint Overlay = 0x3F5;

        /// <summary>
        /// Hunk break
        /// </summary>
        public const uint Break = 0x3F6;

        /// <summary>
        /// Hunk drel32, used as reloc32 short in executables (V37+)
        /// </summary>
        public const uint DRel32 = 0x3F7;

        /// <summary>
        /// Hunk reloc32 short
        /// </summary>
        public const uint ReLoc32Short = 0x3FC;

        /// <summary>
        /// Hunk relative reloc32
        /// </summary>
        public const uint RelReLoc32 = 0x3FD;
    }
}
