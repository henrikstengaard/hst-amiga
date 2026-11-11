namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    /// <summary>
    /// Constants used by smart file system (SFS), see fs/blockstructure.h, fs/objects.h, fs/nodes.h,
    /// fs/btreenodes.h, fs/bitmap.h, fs/adminspaces.h and fs/transactions.h in smart file system source.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Dos type "SFS\0" for smart file system.
        /// </summary>
        public const uint DosTypeSfs0 = 0x53465300;

        /// <summary>
        /// Dos type "SFS\2" for smart file system with large file support (closed source and not supported).
        /// </summary>
        public const uint DosTypeSfs2 = 0x53465302;

        // block ids
        public const uint RootBlockId = DosTypeSfs0; // "SFS\0"
        public const uint AdminSpaceContainerId = 0x41444D43; // "ADMC"
        public const uint BitmapId = 0x42544D50; // "BTMP"
        public const uint ObjectContainerId = 0x4F424A43; // "OBJC"
        public const uint HashTableId = 0x48544142; // "HTAB"
        public const uint SoftLinkId = 0x534C4E4B; // "SLNK"
        public const uint NodeContainerId = 0x4E444320; // "NDC "
        public const uint BNodeContainerId = 0x424E4443; // "BNDC"
        public const uint TransactionStorageId = 0x54525354; // "TRST"
        public const uint TransactionFailureId = 0x54524641; // "TRFA"
        public const uint TransactionOkId = 0x54524F4B; // "TROK"

        /// <summary>
        /// Version of on-disk structures supported.
        /// </summary>
        public const ushort StructureVersion = 3;

        // object nodes
        public const uint RootNode = 1;
        public const uint RecycledNode = 2;
        public const uint ReservedNode = 0xFFFFFFFF;

        // root block bits
        public const byte RootBitsCaseSensitive = 0x80;
        public const byte RootBitsRecycled = 0x40;

        // object bits
        public const byte OTypeHidden = 1;
        public const byte OTypeUndeletable = 2;
        public const byte OTypeQuickDir = 4;
        public const byte OTypeRingList = 8;
        public const byte OTypeHardLink = 32;
        public const byte OTypeLink = 64;
        public const byte OTypeDir = 128;

        // limits
        public const int MaxNameLength = 100;
        public const int MaxCommentLength = 79;
        public const int MaxVolumeNameLength = 30;
        public const ushort MaxExtentBlocks = 65535;
        public const long MaxFileSize = uint.MaxValue - 1;

        /// <summary>
        /// Number of blocks in an admin space.
        /// </summary>
        public const int BlocksAdmin = 32;

        /// <summary>
        /// Shift used to convert block numbers to 32 byte offsets in node index containers.
        /// </summary>
        public const int BlckFAccuracy = 5;

        public const string RecycledName = ".recycled";

        /// <summary>
        /// Flag set in extent prev field, if extent is the first extent of a file.
        /// Lower bits contains the object node of the file.
        /// </summary>
        public const uint ExtentPrevObjectNodeFlag = 0x80000000;

        // sizes of structures
        public const int BlockHeaderSize = 12;
        public const int RootBlockStructureSize = 128;
        public const int RootInfoSize = 36;
        public const int ObjectContainerHeaderSize = 24;
        public const int ObjectHeaderSize = 25;

        /// <summary>
        /// Size of fsObject structure used when calculating space needed for an object.
        /// sizeof(struct fsObject) is 26 on 68k and 28 on AROS non-68k builds, 28 is used to be compatible with both.
        /// </summary>
        public const int ObjectStructureSize = 28;

        public const int HashTableHeaderSize = 16;
        public const int SoftLinkHeaderSize = 24;
        public const int NodeContainerHeaderSize = 20;
        public const int ObjectNodeSize = 10;
        public const int BNodeContainerHeaderSize = 16;
        public const int BNodeSize = 8;
        public const int ExtentBNodeSize = 14;
        public const int AdminSpaceContainerHeaderSize = 24;
        public const int AdminSpaceSize = 8;
        public const int BitmapHeaderSize = 12;
        public const int TransactionStorageHeaderSize = 16;

        // transaction operation bits
        public const byte OperationEmpty = 1;
        public const byte OperationDelete = 2;
    }
}
