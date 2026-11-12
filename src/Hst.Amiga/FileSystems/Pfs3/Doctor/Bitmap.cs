namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Generated bitmap (bitmap_t in doctor.h).
    /// </summary>
    public class Bitmap
    {
        /// <summary>
        /// fix only possible if valid
        /// </summary>
        public bool Valid { get; set; }

        public uint ErrorsFound { get; set; }
        public uint ErrorsFixed { get; set; }
        public uint Start { get; set; }
        public uint Stop { get; set; }
        public uint Step { get; set; }

        /// <summary>
        /// size in longwords
        /// </summary>
        public uint LwSize { get; set; }

        public uint[] Map { get; set; }
    }
}
