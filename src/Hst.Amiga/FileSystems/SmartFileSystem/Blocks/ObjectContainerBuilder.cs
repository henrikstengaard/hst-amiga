namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System;
    using System.IO;
    using Core.Converters;

    public static class ObjectContainerBuilder
    {
        public static byte[] Build(ObjectContainer objectContainer, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, objectContainer);

            BigEndianConverter.ConvertUInt32ToBytes(objectContainer.Parent, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(objectContainer.Next, blockBytes, 0x10);
            BigEndianConverter.ConvertUInt32ToBytes(objectContainer.Previous, blockBytes, 0x14);

            var endOffset = blockSize - (objectContainer.RootInfo != null ? Constants.RootInfoSize : 0);

            var offset = Constants.ObjectContainerHeaderSize;
            foreach (var sfsObject in objectContainer.Objects)
            {
                if (offset + sfsObject.GetStoredSize() > endOffset)
                {
                    throw new IOException($"Object container block {objectContainer.OwnBlock} doesn't have space for object '{sfsObject.Name}'");
                }

                offset += BuildObject(sfsObject, blockBytes, offset);
            }

            if (objectContainer.RootInfo != null)
            {
                BuildRootInfo(objectContainer.RootInfo, blockBytes, blockSize - Constants.RootInfoSize);
            }

            return BlockHelper.UpdateChecksum(blockBytes, objectContainer);
        }

        public static int BuildObject(SfsObject sfsObject, byte[] blockBytes, int offset)
        {
            BigEndianConverter.ConvertUInt16ToBytes(sfsObject.OwnerUid, blockBytes, offset);
            BigEndianConverter.ConvertUInt16ToBytes(sfsObject.OwnerGid, blockBytes, offset + 2);
            BigEndianConverter.ConvertUInt32ToBytes(sfsObject.ObjectNode, blockBytes, offset + 4);
            BigEndianConverter.ConvertUInt32ToBytes(sfsObject.Protection, blockBytes, offset + 8);
            BigEndianConverter.ConvertUInt32ToBytes(sfsObject.Data, blockBytes, offset + 12);
            BigEndianConverter.ConvertUInt32ToBytes(sfsObject.Size, blockBytes, offset + 16);
            BigEndianConverter.ConvertUInt32ToBytes(DateHelper.ConvertFromDate(sfsObject.DateModified), blockBytes, offset + 20);
            blockBytes[offset + 24] = sfsObject.Bits;

            var nameBytes = AmigaTextHelper.GetBytes(sfsObject.Name);
            var commentBytes = AmigaTextHelper.GetBytes(sfsObject.Comment ?? string.Empty);

            var nameOffset = offset + Constants.ObjectHeaderSize;
            Array.Copy(nameBytes, 0, blockBytes, nameOffset, nameBytes.Length);
            blockBytes[nameOffset + nameBytes.Length] = 0;

            var commentOffset = nameOffset + nameBytes.Length + 1;
            Array.Copy(commentBytes, 0, blockBytes, commentOffset, commentBytes.Length);
            blockBytes[commentOffset + commentBytes.Length] = 0;

            return sfsObject.GetStoredSize();
        }

        public static void BuildRootInfo(RootInfo rootInfo, byte[] blockBytes, int offset)
        {
            BigEndianConverter.ConvertUInt32ToBytes(rootInfo.DeletedBlocks, blockBytes, offset);
            BigEndianConverter.ConvertUInt32ToBytes(rootInfo.DeletedFiles, blockBytes, offset + 4);
            BigEndianConverter.ConvertUInt32ToBytes(rootInfo.FreeBlocks, blockBytes, offset + 8);
            BigEndianConverter.ConvertUInt32ToBytes(DateHelper.ConvertFromDate(rootInfo.DateCreated), blockBytes, offset + 12);
            BigEndianConverter.ConvertUInt32ToBytes(rootInfo.LastAllocatedBlock, blockBytes, offset + 16);
            BigEndianConverter.ConvertUInt32ToBytes(rootInfo.LastAllocatedAdminSpace, blockBytes, offset + 20);
            BigEndianConverter.ConvertUInt32ToBytes(rootInfo.LastAllocatedExtentNode, blockBytes, offset + 24);
            BigEndianConverter.ConvertUInt32ToBytes(rootInfo.LastAllocatedObjectNode, blockBytes, offset + 28);
            BigEndianConverter.ConvertUInt32ToBytes(rootInfo.RovingPointer, blockBytes, offset + 32);
        }
    }
}
