namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class AdminSpaceContainerParser
    {
        public static AdminSpaceContainer Parse(byte[] blockBytes)
        {
            var adminSpaceContainer = new AdminSpaceContainer(blockBytes.Length);
            BlockHelper.ReadHeader(blockBytes, adminSpaceContainer, Constants.AdminSpaceContainerId);

            adminSpaceContainer.Next = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
            adminSpaceContainer.Previous = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
            adminSpaceContainer.Bits = blockBytes[0x14];

            for (var i = 0; i < adminSpaceContainer.AdminSpaces.Length; i++)
            {
                var offset = Constants.AdminSpaceContainerHeaderSize + i * Constants.AdminSpaceSize;
                adminSpaceContainer.AdminSpaces[i].Space = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset);
                adminSpaceContainer.AdminSpaces[i].Bits = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset + 4);
            }

            return adminSpaceContainer;
        }
    }
}
