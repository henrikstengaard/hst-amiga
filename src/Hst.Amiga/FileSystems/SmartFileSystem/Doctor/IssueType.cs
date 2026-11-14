namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    /// <summary>
    /// Type of issue found by doctor.
    /// </summary>
    public enum IssueType
    {
        /// <summary>
        /// Volume is not a smart file system volume or uses an unsupported structure version or dos type (SFS\2).
        /// </summary>
        NotSfsDisk,

        /// <summary>
        /// Root block is invalid: wrong id, checksum, own block, version, block size or total blocks.
        /// Smart file system requires both root blocks to be valid.
        /// </summary>
        RootBlockInvalid,

        /// <summary>
        /// Root blocks at first and last block of partition don't contain the same data.
        /// </summary>
        RootBlockMismatch,

        /// <summary>
        /// Transaction block is invalid, it's neither a transaction ok nor a transaction failure block.
        /// </summary>
        TransactionBlockInvalid,

        /// <summary>
        /// Volume has an unfinished transaction, which is replayed when the volume is mounted.
        /// </summary>
        TransactionPending,

        /// <summary>
        /// Unfinished transaction can't be loaded from transaction storage blocks.
        /// </summary>
        TransactionInvalid,

        /// <summary>
        /// Admin space container block is invalid or chain is broken.
        /// </summary>
        AdminSpaceContainerInvalid,

        /// <summary>
        /// Admin space is outside partition or overlaps another admin space.
        /// </summary>
        AdminSpaceInvalid,

        /// <summary>
        /// Admin block is used, but it's not marked used in admin space or not inside any admin space.
        /// </summary>
        AdminBlockNotMarked,

        /// <summary>
        /// Admin block is marked used in admin space, but it's not used.
        /// </summary>
        AdminBlockNotUsed,

        /// <summary>
        /// Node container block in object node tree is invalid.
        /// </summary>
        NodeContainerInvalid,

        /// <summary>
        /// Object node doesn't point to the object container containing the object.
        /// </summary>
        ObjectNodeInvalid,

        /// <summary>
        /// Object node is in use, but no object uses it.
        /// </summary>
        ObjectNodeNotUsed,

        /// <summary>
        /// Object has invalid or duplicate object node number.
        /// </summary>
        ObjectNodeNumberInvalid,

        /// <summary>
        /// Root object container is invalid or doesn't contain the root directory.
        /// </summary>
        RootObjectContainerInvalid,

        /// <summary>
        /// Object container block is invalid, e.g. wrong id, checksum or own block, or used by more than one directory.
        /// </summary>
        ObjectContainerInvalid,

        /// <summary>
        /// Object container has wrong parent, previous or next block, or is empty.
        /// </summary>
        ObjectContainerChain,

        /// <summary>
        /// Object in object container is corrupt and can't be read.
        /// </summary>
        ObjectCorrupt,

        /// <summary>
        /// Object name is invalid: empty, too long or contains ':' or '/'.
        /// </summary>
        InvalidName,

        /// <summary>
        /// Object comment is too long.
        /// </summary>
        InvalidComment,

        /// <summary>
        /// Directory contains more than one object with the same name.
        /// </summary>
        DuplicateName,

        /// <summary>
        /// Hash table block is invalid.
        /// </summary>
        HashTableInvalid,

        /// <summary>
        /// Hash chains don't match objects in directory.
        /// </summary>
        HashChainInvalid,

        /// <summary>
        /// Soft link block is invalid.
        /// </summary>
        SoftLinkInvalid,

        /// <summary>
        /// Object type isn't supported, e.g. hard link.
        /// </summary>
        UnsupportedObject,

        /// <summary>
        /// B-tree node container block in extent B-tree is invalid or keys are not sorted.
        /// </summary>
        BNodeContainerInvalid,

        /// <summary>
        /// Extent of file doesn't exist, is outside partition, has zero blocks or chain loops.
        /// </summary>
        ExtentInvalid,

        /// <summary>
        /// Extent next or previous doesn't match extent chain of file.
        /// </summary>
        ExtentChain,

        /// <summary>
        /// Extent is in extent B-tree, but not used by any file.
        /// </summary>
        ExtentNotUsed,

        /// <summary>
        /// Extent uses blocks used by administration blocks, root blocks, bitmap or reserved blocks.
        /// </summary>
        ExtentOverlapsMetadata,

        /// <summary>
        /// Extents of two files use the same blocks.
        /// </summary>
        CrossLinkedExtent,

        /// <summary>
        /// File size doesn't match number of blocks in extents.
        /// </summary>
        FileSizeMismatch,

        /// <summary>
        /// Bitmap block is invalid.
        /// </summary>
        BitmapBlockInvalid,

        /// <summary>
        /// Blocks used are marked free in bitmap.
        /// </summary>
        BlocksUsedMarkedFree,

        /// <summary>
        /// Blocks not used are marked used in bitmap.
        /// </summary>
        BlocksFreeMarkedUsed,

        /// <summary>
        /// Bits after last block of partition are not cleared in last bitmap block.
        /// </summary>
        BitmapPadding,

        /// <summary>
        /// Root info free blocks, deleted files or deleted blocks doesn't match volume.
        /// </summary>
        RootInfoMismatch,

        /// <summary>
        /// Repair needs to allocate an administration block, but there is no free space.
        /// </summary>
        DiskFull
    }
}
