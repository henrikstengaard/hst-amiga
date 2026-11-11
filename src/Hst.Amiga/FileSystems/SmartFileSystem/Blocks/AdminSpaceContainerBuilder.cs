namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class AdminSpaceContainerBuilder
    {
        public static byte[] Build(AdminSpaceContainer adminSpaceContainer, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, adminSpaceContainer);

            BigEndianConverter.ConvertUInt32ToBytes(adminSpaceContainer.Next, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(adminSpaceContainer.Previous, blockBytes, 0x10);
            blockBytes[0x14] = adminSpaceContainer.Bits;

            var capacity = AdminSpaceContainer.Capacity(blockSize);
            for (var i = 0; i < capacity && i < adminSpaceContainer.AdminSpaces.Length; i++)
            {
                var offset = Constants.AdminSpaceContainerHeaderSize + i * Constants.AdminSpaceSize;
                BigEndianConverter.ConvertUInt32ToBytes(adminSpaceContainer.AdminSpaces[i].Space, blockBytes, offset);
                BigEndianConverter.ConvertUInt32ToBytes(adminSpaceContainer.AdminSpaces[i].Bits, blockBytes, offset + 4);
            }

            return BlockHelper.UpdateChecksum(blockBytes, adminSpaceContainer);
        }
    }
}
