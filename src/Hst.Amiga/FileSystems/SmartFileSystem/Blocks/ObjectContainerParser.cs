namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class ObjectContainerParser
    {
        /// <summary>
        /// Parse object container block.
        /// </summary>
        /// <param name="blockBytes">Block bytes.</param>
        /// <param name="hasRootInfo">True if object container is root object container with root info.</param>
        public static ObjectContainer Parse(byte[] blockBytes, bool hasRootInfo = false)
        {
            var objectContainer = new ObjectContainer();
            BlockHelper.ReadHeader(blockBytes, objectContainer, Constants.ObjectContainerId);

            objectContainer.Parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
            objectContainer.Next = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
            objectContainer.Previous = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x14);

            var endOffset = blockBytes.Length - (hasRootInfo ? Constants.RootInfoSize : 0);

            // objects are present while offset is less than block size - sizeof(struct fsObject) - 2
            // and first character of name is not zero. 68k sizeof(struct fsObject) = 26 is used here to read
            // objects written by both 68k and non-68k smart file system.
            var objectsEndOffset = blockBytes.Length - 26 - 2;

            var offset = Constants.ObjectContainerHeaderSize;
            while (offset < objectsEndOffset && offset + Constants.ObjectHeaderSize < endOffset &&
                   blockBytes[offset + Constants.ObjectHeaderSize] != 0)
            {
                var sfsObject = ParseObject(blockBytes, offset, out var size);
                objectContainer.Objects.Add(sfsObject);
                offset += size;
            }

            if (hasRootInfo)
            {
                objectContainer.RootInfo = ParseRootInfo(blockBytes, blockBytes.Length - Constants.RootInfoSize);
            }

            return objectContainer;
        }

        public static SfsObject ParseObject(byte[] blockBytes, int offset, out int size)
        {
            var sfsObject = new SfsObject
            {
                OwnerUid = BigEndianConverter.ConvertBytesToUInt16(blockBytes, offset),
                OwnerGid = BigEndianConverter.ConvertBytesToUInt16(blockBytes, offset + 2),
                ObjectNode = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 4),
                Protection = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 8),
                Data = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 12),
                Size = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 16),
                DateModified = DateHelper.ConvertToDate(BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 20)),
                Bits = blockBytes[offset + 24]
            };

            var nameOffset = offset + Constants.ObjectHeaderSize;
            sfsObject.Name = BlockHelper.ReadNullTerminatedString(blockBytes, nameOffset, out var nameBytesRead);
            var commentOffset = nameOffset + nameBytesRead;
            sfsObject.Comment = commentOffset < blockBytes.Length
                ? BlockHelper.ReadNullTerminatedString(blockBytes, commentOffset, out var commentBytesRead)
                : string.Empty;

            size = sfsObject.GetStoredSize();
            return sfsObject;
        }

        public static RootInfo ParseRootInfo(byte[] blockBytes, int offset)
        {
            return new RootInfo
            {
                DeletedBlocks = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset),
                DeletedFiles = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 4),
                FreeBlocks = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 8),
                DateCreated = DateHelper.ConvertToDate(BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 12)),
                LastAllocatedBlock = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 16),
                LastAllocatedAdminSpace = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 20),
                LastAllocatedExtentNode = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 24),
                LastAllocatedObjectNode = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 28),
                RovingPointer = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 32)
            };
        }
    }
}
