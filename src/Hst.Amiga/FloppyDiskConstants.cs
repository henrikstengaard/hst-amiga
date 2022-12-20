namespace Hst.Amiga
{
    public static class FloppyDiskConstants
    {
        public const int BlockSize = 512;
        public const int FileSystemBlockSize = 512;

        public static class DoubleDensity
        {
            public const int Size = 901120;
            public const int Cylinders = 80; // cylinders = size / heads * sectors * block size;
            public const int LowCyl = 0; // start cylinder = 0
            public const int HighCyl = 79; // end cylinder = cylinders - 1
            public const int Heads = 2;
            public const int Sectors = 11;
            public const int ReservedBlocks = 2; // 2 blocks reserved for boot blocks in the beginning of the floppy
        }

        public static class HighDensity
        {
            public const int Size = 1802240;
            public const int Cylinders = 80; // cylinders = size / heads * sectors * block size;
            public const int LowCyl = 0; // start cylinder = 0
            public const int HighCyl = 79; // end cylinder = cylinders - 1
            public const int Heads = 2;
            public const int Sectors = 22;
            public const int ReservedBlocks = 2; // 2 blocks reserved for boot blocks in the beginning of the floppy
        }

        /// <summary>
        /// Bootable boot blocks present after DOS type when making bootable floppy with command 'install df0:'
        /// </summary>
        public static byte[] BootableBootBlockBytes =
        {
            0x43, 0xfa, 0x00, 0x3e, 0x70, 0x25, 0x4e, 0xae, 0xfd, 0xd8, 0x4a, 0x80, 0x67, 0x0c, 0x22, 0x40, 0x08, 0xe9,
            0x00, 0x06, 0x00, 0x22, 0x4e, 0xae, 0xfe, 0x62, 0x43, 0xfa, 0x00, 0x18, 0x4e, 0xae, 0xff, 0xa0, 0x4a, 0x80,
            0x67, 0x0a, 0x20, 0x40, 0x20, 0x68, 0x00, 0x16, 0x70, 0x00, 0x4e, 0x75, 0x70, 0xff, 0x4e, 0x75, 0x64, 0x6f,
            0x73, 0x2e, 0x6c, 0x69, 0x62, 0x72, 0x61, 0x72, 0x79, 0x00, 0x65, 0x78, 0x70, 0x61, 0x6e, 0x73, 0x69, 0x6f,
            0x6e, 0x2e, 0x6c, 0x69, 0x62, 0x72, 0x61, 0x72, 0x79
        };
    }
}