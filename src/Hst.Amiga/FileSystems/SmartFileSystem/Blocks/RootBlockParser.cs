namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class RootBlockParser
    {
        public static RootBlock Parse(byte[] blockBytes)
        {
            var rootBlock = new RootBlock();
            BlockHelper.ReadHeader(blockBytes, rootBlock);

            rootBlock.Version = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0xc);
            rootBlock.SequenceNumber = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0xe);
            rootBlock.DateCreated = DateHelper.ConvertToDate(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10));
            rootBlock.Bits = blockBytes[0x14];
            rootBlock.FirstByte = ((ulong)BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x20) << 32) |
                                  BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x24);
            rootBlock.LastByte = ((ulong)BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x28) << 32) |
                                 BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x2c);
            rootBlock.TotalBlocks = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x30);
            rootBlock.BlockSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x34);
            rootBlock.BitmapBase = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x60);
            rootBlock.AdminSpaceContainer = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x64);
            rootBlock.RootObjectContainer = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x68);
            rootBlock.ExtentBNodeRoot = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x6c);
            rootBlock.ObjectNodeRoot = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x70);

            return rootBlock;
        }
    }
}
