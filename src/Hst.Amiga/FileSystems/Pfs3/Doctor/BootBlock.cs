namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Boot block view (bootblock_t in pfs3.h).
    /// </summary>
    public class BootBlock : StructView
    {
        public BootBlock(byte[] data) : base(data, 0)
        {
        }

        /// <summary>
        /// PFS\1
        /// </summary>
        public uint DiskType
        {
            get => GetUInt32(0);
            set => SetUInt32(0, value);
        }
    }
}
