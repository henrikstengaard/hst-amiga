namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using Core.Converters;

    /// <summary>
    /// Cached block (cachedblock_t, c_dirblock_t, c_indexblock_t, c_extensionblock_t, c_deldirblock_t,
    /// c_anodeblock_t and c_bitmapblock_t in doctor.h). Data contains the raw reserved block,
    /// which can be accessed as the different block types like the reservedblock union in pfs3.h.
    /// Buildblocks (buildblock_t) are also cached blocks.
    /// </summary>
    public class CachedBlock
    {
        public uint BlockNr { get; set; }
        public Mode Mode { get; set; }

        /// <summary>
        /// Raw reserved block data (reservedblock_t *data).
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Id of reserved block (data->id).
        /// </summary>
        public ushort Id
        {
            get => BigEndianConverter.ConvertBytesToUInt16(Data);
            set => BigEndianConverter.ConvertUInt16ToBytes(value, Data, 0);
        }

        public BootBlock BootBlock => new BootBlock(Data);
        public RootBlock RootBlock => new RootBlock(Data);
        public ExtensionBlock ExtensionBlock => new ExtensionBlock(Data);
        public BitmapBlock BitmapBlock => new BitmapBlock(Data);
        public IndexBlock IndexBlock => new IndexBlock(Data);
        public AnodeBlock AnodeBlock => new AnodeBlock(Data);
        public DirBlock DirBlock => new DirBlock(Data);
        public DelDirBlock DelDirBlock => new DelDirBlock(Data);
    }
}
