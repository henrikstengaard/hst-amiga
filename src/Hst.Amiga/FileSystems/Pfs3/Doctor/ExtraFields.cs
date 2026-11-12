namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Directory entry extra fields (struct extrafields in pfs3.h).
    /// </summary>
    public class ExtraFields
    {
        /// <summary>
        /// Size of struct extrafields in words (sizeof(struct extrafields) / 2).
        /// </summary>
        public const int SizeOfInWords = 11;

        /// <summary>
        /// link anodenr
        /// </summary>
        public uint Link { get; set; }

        /// <summary>
        /// user id
        /// </summary>
        public ushort Uid { get; set; }

        /// <summary>
        /// group id
        /// </summary>
        public ushort Gid { get; set; }

        /// <summary>
        /// byte 1-3 of protection
        /// </summary>
        public uint Prot { get; set; }

        /// <summary>
        /// virtual rollover filesize
        /// </summary>
        public uint VirtualSize { get; set; }

        /// <summary>
        /// rollover fileoffset
        /// </summary>
        public uint RollPointer { get; set; }

        /// <summary>
        /// extended bits 32-47 of direntry.fsize
        /// </summary>
        public ushort FSizeX { get; set; }

        /// <summary>
        /// Get extra fields as words (uint16 *extra = (uint16 *)extrafields).
        /// </summary>
        public ushort[] ToWords()
        {
            return new[]
            {
                (ushort)(Link >> 16), (ushort)Link,
                Uid,
                Gid,
                (ushort)(Prot >> 16), (ushort)Prot,
                (ushort)(VirtualSize >> 16), (ushort)VirtualSize,
                (ushort)(RollPointer >> 16), (ushort)RollPointer,
                FSizeX
            };
        }

        /// <summary>
        /// Set extra fields from words.
        /// </summary>
        public void FromWords(ushort[] words)
        {
            Link = ((uint)words[0] << 16) | words[1];
            Uid = words[2];
            Gid = words[3];
            Prot = ((uint)words[4] << 16) | words[5];
            VirtualSize = ((uint)words[6] << 16) | words[7];
            RollPointer = ((uint)words[8] << 16) | words[9];
            FSizeX = words[10];
        }
    }
}
