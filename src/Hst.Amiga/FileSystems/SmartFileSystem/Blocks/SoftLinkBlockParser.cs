namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using Core.Converters;

    public static class SoftLinkBlockParser
    {
        public static SoftLinkBlock Parse(byte[] blockBytes)
        {
            var softLinkBlock = new SoftLinkBlock();
            BlockHelper.ReadHeader(blockBytes, softLinkBlock, Constants.SoftLinkId);

            softLinkBlock.Parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
            softLinkBlock.Next = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
            softLinkBlock.Previous = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x14);
            softLinkBlock.Path = BlockHelper.ReadNullTerminatedString(blockBytes, Constants.SoftLinkHeaderSize, out _);

            return softLinkBlock;
        }
    }
}
