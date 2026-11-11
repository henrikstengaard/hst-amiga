namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class RootBlockBuilder
    {
        public static byte[] Build(RootBlock rootBlock, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, rootBlock);

            BigEndianConverter.ConvertUInt16ToBytes(rootBlock.Version, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt16ToBytes(rootBlock.SequenceNumber, blockBytes, 0xe);
            BigEndianConverter.ConvertUInt32ToBytes(DateHelper.ConvertFromDate(rootBlock.DateCreated), blockBytes, 0x10);
            blockBytes[0x14] = rootBlock.Bits;
            BigEndianConverter.ConvertUInt32ToBytes((uint)(rootBlock.FirstByte >> 32), blockBytes, 0x20);
            BigEndianConverter.ConvertUInt32ToBytes((uint)(rootBlock.FirstByte & 0xffffffff), blockBytes, 0x24);
            BigEndianConverter.ConvertUInt32ToBytes((uint)(rootBlock.LastByte >> 32), blockBytes, 0x28);
            BigEndianConverter.ConvertUInt32ToBytes((uint)(rootBlock.LastByte & 0xffffffff), blockBytes, 0x2c);
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.TotalBlocks, blockBytes, 0x30);
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.BlockSize, blockBytes, 0x34);
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.BitmapBase, blockBytes, 0x60);
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.AdminSpaceContainer, blockBytes, 0x64);
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.RootObjectContainer, blockBytes, 0x68);
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.ExtentBNodeRoot, blockBytes, 0x6c);
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.ObjectNodeRoot, blockBytes, 0x70);

            return BlockHelper.UpdateChecksum(blockBytes, rootBlock);
        }
    }
}
