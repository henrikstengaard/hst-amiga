namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    /// <summary>
    /// Type of issue found by doctor.
    /// </summary>
    public enum IssueType
    {
        /// <summary>
        /// Dos type is not a Fast File System dos type.
        /// </summary>
        NotDosDisk,

        /// <summary>
        /// Boot block dos type is invalid or doesn't match partition dos type.
        /// </summary>
        BootBlockDosType,

        /// <summary>
        /// Root block is invalid, e.g. wrong type, secondary type or hash table size.
        /// </summary>
        RootBlockInvalid,

        /// <summary>
        /// Root block has checksum error, but structure is valid.
        /// </summary>
        RootBlockChecksum,

        /// <summary>
        /// Volume is not validated, bitmap flag in root block is not valid.
        /// </summary>
        NotValidated,

        /// <summary>
        /// Pointer in hash chain is outside volume.
        /// </summary>
        HashChainOutOfRange,

        /// <summary>
        /// Hash chain loops or points to an entry already found in another directory.
        /// </summary>
        HashChainLoop,

        /// <summary>
        /// Hash chain points to a block that is not a valid entry block.
        /// </summary>
        InvalidEntryBlock,

        /// <summary>
        /// Entry block has checksum error, but structure is valid.
        /// </summary>
        EntryBlockChecksum,

        /// <summary>
        /// Entry block uses a block already used by another entry.
        /// </summary>
        CrossLinkedBlock,

        /// <summary>
        /// Entry parent pointer doesn't point to the directory it's in.
        /// </summary>
        WrongParent,

        /// <summary>
        /// Entry name is empty, too long or contains invalid characters.
        /// </summary>
        InvalidName,

        /// <summary>
        /// Entry name is already used by another entry in the directory.
        /// </summary>
        DuplicateName,

        /// <summary>
        /// Entry is in a hash chain not matching the hash of its name.
        /// </summary>
        WrongHashChain,

        /// <summary>
        /// Entry date is invalid.
        /// </summary>
        InvalidDate,

        /// <summary>
        /// File data block pointer is outside volume or block is already used.
        /// </summary>
        InvalidDataBlockPointer,

        /// <summary>
        /// OFS data block is invalid, e.g. wrong type, checksum, header key or sequence number.
        /// </summary>
        InvalidDataBlock,

        /// <summary>
        /// OFS data block next data pointer doesn't point to next data block.
        /// </summary>
        InvalidDataBlockChain,

        /// <summary>
        /// File extension block is invalid.
        /// </summary>
        InvalidFileExtensionBlock,

        /// <summary>
        /// File block list is invalid, e.g. high seq is larger than table size.
        /// </summary>
        InvalidFileBlockList,

        /// <summary>
        /// File size doesn't match number of data blocks.
        /// </summary>
        FileSizeMismatch,

        /// <summary>
        /// File first data pointer doesn't point to first data block.
        /// </summary>
        InvalidFirstData,

        /// <summary>
        /// Hard link points to an invalid or missing entry.
        /// </summary>
        DanglingLink,

        /// <summary>
        /// Hard link is not in the link chain of the entry it points to.
        /// </summary>
        LinkNotInChain,

        /// <summary>
        /// Long name file system comment block is invalid.
        /// </summary>
        InvalidCommentBlock,

        /// <summary>
        /// Directory cache blocks are invalid or doesn't match directory entries.
        /// </summary>
        DirCacheInvalid,

        /// <summary>
        /// Bitmap block or bitmap extension block pointer is invalid.
        /// </summary>
        BitmapPointerInvalid,

        /// <summary>
        /// Bitmap block has checksum error.
        /// </summary>
        BitmapChecksum,

        /// <summary>
        /// Blocks are used, but marked free in bitmap.
        /// </summary>
        BlocksUsedMarkedFree,

        /// <summary>
        /// Blocks are not used, but marked used in bitmap.
        /// </summary>
        BlocksFreeMarkedUsed,

        /// <summary>
        /// No free blocks to allocate for repair.
        /// </summary>
        DiskFull
    }
}
